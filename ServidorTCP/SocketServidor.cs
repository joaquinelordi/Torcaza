using System.Text;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Newtonsoft.Json;
using Entidades;
using NpgsqlTypes;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite;
using System.Globalization;
using System.Diagnostics;
using Entidades;
using System.Numerics;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;

namespace ServidorTCP
{
    public class TcpServer
    {
        private readonly TcpListener _listener;
        private readonly string _connectionString;
        private int _port { get; set; }
        private string _ipAddress { get; set; }
        private readonly OpenCellID _openCellID;
        private readonly HandlerJWT _handlerJWT;

        public event EventHandler<string> MensajeRecibido;
        public TcpServer(string ipAddress, int port, OpenCellID openCellID, HandlerJWT handlerJWT)
        {
            _ipAddress = ipAddress;
            _port = port;
            _openCellID = openCellID;
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";
            _handlerJWT = handlerJWT;
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"Servidor TCP iniciado... {_ipAddress}:{_port}");
            _listener.Start();

            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Cliente conectado.");
                _ = Task.Run(async () => await HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (var stream = client.GetStream())
            {
                var buffer = new byte[1024];
                var messageBuilder = new StringBuilder();
                int bytesRead;

                // Configuracion del watchdog
                var stopwatch = new Stopwatch();
                var timeout = TimeSpan.FromSeconds(300);
                stopwatch.Start();

                while (true)
                {
                    if (stream.DataAvailable)
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("Cliente Cerro la conexion");
                            break;
                        }

                        var mensaje = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(mensaje);

                        // Verificar si el mensaje completo ha sido recibido
                        if (messageBuilder.ToString().EndsWith("\n\0"))
                        {
                            var mensajeCompleto = messageBuilder.ToString();
                            Console.WriteLine($"Mensaje recibido: {mensajeCompleto}");
                            //quito el \n del final
                            mensajeCompleto = mensajeCompleto.TrimEnd('\n', '\0');

                            // Invocar el evento de mensaje recibido
                            MensajeRecibido?.Invoke(this, mensajeCompleto);

                            // Enviar una respuesta al cliente
                            //var response = Encoding.ASCII.GetBytes("Mensaje recibido");
                            //await stream.WriteAsync(response, 0, response.Length);

                            // Procesar el mensaje recibido
                            ProcesarMensaje(mensajeCompleto);

                            // Limpiar el StringBuilder para el próximo mensaje
                            messageBuilder.Clear();

                            // Reiniciar el stopwatch
                            stopwatch.Restart();
                        }
                    }
                    else
                    {
                        // Verificar si el tiempo de espera ha sido excedido
                        if (stopwatch.Elapsed >= timeout)
                        {
                            Console.WriteLine("Tiempo de espera excedido. Cerrando la conexión.");
                            break;
                        }
                        await Task.Delay(10);
                    }

                }
                client.Close();
            }
        }

        // ...

        private void ProcesarMensaje(string buffer)
        {
            Console.WriteLine($"ProcesarMensaje -> Inicio: {buffer}");
            string mensaje = "";

            // El mensaje puede ser un JWT, debo extraer el payload de ser necesario
            if (_handlerJWT.EsJWTValido(buffer))
            {
                var payload = _handlerJWT.LeerPayload(buffer);
                if (payload != null)
                {
                    mensaje = payload;
                    Console.WriteLine($"ProcesarMensaje -> Payload decodificado: {JsonConvert.SerializeObject(payload, Formatting.Indented)}");
                }
                else
                {
                    Console.WriteLine($"ProcesarMensaje -> Error al decodificar el JWT: {buffer}");
                }
            }
            else
            {
                mensaje = buffer;
                Console.WriteLine($"ProcesarMensaje -> recibi un JSON, no un JWT: {mensaje}");
            }

            TrackerPayloadBase payloadBase = JsonDeserializer.DeserializeJson(mensaje);
            eTipoMensaje accion = payloadBase.GetTipoMensaje();
            List<InfoCell> infoTorreCelulares = new List<InfoCell>();
            //Aca la idea es usar un enum con los tipos de acciones disponibles, enviado en el objeto del mensaje 
            switch (accion)
            {
                case eTipoMensaje.GNSS:
                    //Hago la traduccion de datos crudos del rastreado y los cargo en la base
                    Ubicacion ubicacion = new Ubicacion();
                    infoTorreCelulares.Add(new InfoCell(payloadBase.CellInfo));
                    BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                    //TODO: refactorizar
                    CargarDatosRastreador(ref ubicacion, mensaje);
                    CargarRegistroTorreCelular(infoTorreCelulares[0]);
                    CargarUbicacion(ubicacion);
                    break;

                case eTipoMensaje.InfoCell:
                    //

                    CargarDatosTorresCelulares(ref infoTorreCelulares, payloadBase);
                    BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                    CargarListaRegistroTorreCelular(ref infoTorreCelulares);
 
                    break;

                case eTipoMensaje.TipoDesconocido:
                    Console.WriteLine($"ProcesarMensaje -> Tipo de mensaje desconocido: {mensaje}");
                    break;
            }

        }

        #region Torres Celulares
        private void CargarDatosTorresCelulares(ref List<InfoCell> infoTorreCelulares, TrackerPayloadBase oPayload)
        {
            // casteo el objeto, que ya se que es y asi inicializo infocell
            CellNeighborsInfoPayload cellNeighborsInfo = oPayload as CellNeighborsInfoPayload;

            // inicializo el infocell a partir de la celda celular usada para transmitir
            InfoCell infoCell = new InfoCell(cellNeighborsInfo.CellInfo);
            // lo cargo en la lista a consultar
            infoTorreCelulares.Add(infoCell);

            //agrego a la lista las torres celulares vecinas
            foreach (var cellInfo in cellNeighborsInfo.NeighborCells)
            {
                InfoCell infoCellVecina = new InfoCell(cellInfo);
                infoTorreCelulares.Add(infoCellVecina);
            }
        }

        List<string> parsearRegistros(string str)
        {
            //separo el mensaje completo en registros de cada torre celular recibida
            string[] registros = { "tenemos que definir en que orden bienen los datos y que separa a cada torre" };
            return registros.ToList();
        }

        private void BuscarCoordenadasTorresCelulares(ref List<InfoCell> infoTorreCelulares)
        {
            Console.WriteLine($"Buscando coordenadas para la torre celular -> Inicio");

            // Aca busco las coordenadas de las torres celulares en la base de datos
            var task = BuscarCoordenadasTorresCelularesAsync(infoTorreCelulares);
            task.Wait();

            Console.WriteLine($"Buscando coordenadas para la torre celular -> Fin");
        }

        /// <summary>
        /// Metodo asincronico para consultar en opencellID las coordenadas de las torres celulares
        /// </summary>
        /// <param name="infoTorreCelulares"></param>
        /// <returns></returns>
        private async Task BuscarCoordenadasTorresCelularesAsync(List<InfoCell> infoTorreCelulares)
        {
            foreach (var infoCell in infoTorreCelulares)
            {
                try
                {
                    var cellInfo = await _openCellID.GetCellInfoAsync(
                        (int)infoCell.Mcc,
                        (int)infoCell.Mnc,
                        // el rastreador los envia en formato hexadecimal ej: "1AB5"
                        int.Parse(infoCell.Lac, NumberStyles.HexNumber),
                        int.Parse(infoCell.Cellid, NumberStyles.HexNumber)
                    );

                    if (cellInfo != null)
                    {
                        infoCell.Lat = cellInfo.Lat;
                        infoCell.Lon = cellInfo.Lon;
                        infoCell.AverageSignalStrength = cellInfo.AverageSignalStrength;
                        infoCell.Range = cellInfo.Range;
                        infoCell.IsInDatabase = true;

                        Console.WriteLine($"Coordenadas encontradas: MCC={infoCell.Mcc}, MNC={infoCell.Mnc}, LAC={infoCell.Lac}, CellID={infoCell.Cellid}, Lat={infoCell.Lat}, Lon={infoCell.Lon}");
                    }
                    else
                    {
                        Console.WriteLine($"No se encontraron coordenadas para la torre: MCC={infoCell.Mcc}, MNC={infoCell.Mnc}, LAC={infoCell.Lac}, CellID={infoCell.Cellid}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al buscar coordenadas para la torre: MCC={infoCell.Mcc}, MNC={infoCell.Mnc}, LAC={infoCell.Lac}, CellID={infoCell.Cellid}. Detalles: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// obtiene un numero de registro y carga los infocell
        /// </summary>
        /// <param name="listaInfoCell"></param>
        private void CargarListaRegistroTorreCelular(ref List<InfoCell> listaInfoCell)
        {
            Guid numeroReporte = Guid.Empty;

            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");

            numeroReporte = ObtenerNumeroReporte(idRegistro, idDispositivo, agenteID);

            if (numeroReporte != Guid.Empty)
            {
                foreach (InfoCell infoCell in listaInfoCell)
                {
                    CargarRegistroTorreCelular(infoCell, numeroReporte);
                }
            }
            else
            {
                Console.WriteLine($"CargarListaRegistroTorreCelular -> No se pudo obtener un numero de reporte, " +
                    $"por lo tanto no se carga la lista de InfoCell");
            }
        }

        /// <summary>
        /// Metodo encargado de cargar el registro de la torre celular en la base de datos
        /// </summary>
        /// <param name="infoCell"></param>
        private void CargarRegistroTorreCelular(InfoCell infoCell, Guid numeroReporte)
        {
            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");

            // Convertimos la fecha y hora al formato UTC
            DateTime cellTimestamp = infoCell.FechaHora.ToUniversalTime();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Comando del SP
                using var command = new NpgsqlCommand("CALL cargar_ReporteCeldaCelular(@cell_id, @cell_mcc, @cell_mnc, @cell_lac, @cell_tecnologia, @cell_band, @cell_chanel, @cell_nivelsenial, @cell_timestamp, @numeroreporte, @cell_longitud, @cell_latitud, @id_registro, @id_dispositivo, @id_agente)", connection);

                // Agrego los parametros
                command.Parameters.AddWithValue("cell_id", Convert.ToInt64(infoCell.Cellid, 16)); //string de un hexadecimal
                command.Parameters.AddWithValue("cell_mcc", (long)infoCell.Mcc);
                command.Parameters.AddWithValue("cell_mnc", (long)infoCell.Mnc);
                command.Parameters.AddWithValue("cell_lac", Convert.ToInt64(infoCell.Lac, 16)); //string de un hexadecimal
                command.Parameters.AddWithValue("cell_tecnologia", infoCell.TecnologiaAcceso != null ? Convert.ToInt64(infoCell.TecnologiaAcceso) : DBNull.Value);
                command.Parameters.AddWithValue("cell_band", infoCell.Banda != string.Empty ? infoCell.Banda : DBNull.Value);
                command.Parameters.AddWithValue("cell_chanel", Convert.ToInt64(infoCell.Canal));
                command.Parameters.AddWithValue("cell_nivelsenial", Convert.ToInt64(infoCell.senialdB));
                command.Parameters.AddWithValue("cell_timestamp", cellTimestamp);
                command.Parameters.AddWithValue("numeroreporte", numeroReporte);

                command.Parameters.AddWithValue("cell_longitud", infoCell.Lon.HasValue ? infoCell.Lon.Value : DBNull.Value);
                command.Parameters.AddWithValue("cell_latitud", infoCell.Lat.HasValue ? infoCell.Lat.Value : DBNull.Value);

                command.Parameters.AddWithValue("id_registro", idRegistro);
                command.Parameters.AddWithValue("id_dispositivo", idDispositivo);
                command.Parameters.AddWithValue("id_agente", agenteID);


                // Debug
                /*
                var queryDebug = new StringBuilder(command.CommandText);
                foreach (NpgsqlParameter param in command.Parameters)
                {
                    queryDebug.AppendLine()
                              .Append($"-- {param.ParameterName} = {param.Value}");
                }
                string consultaCompleta = queryDebug.ToString();
                Console.WriteLine("Consulta SQL generada:");
                Console.WriteLine(consultaCompleta);
                */


                // Ejecutar el comando
                command.ExecuteNonQuery();

                Console.WriteLine($"CargarRegistroTorreCelular -> Carga exitosa");


            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// este metodo deberia consultar la base de datos y devolver un numero de reporte unico
        /// </summary>
        /// <param name="idRegistro"></param>
        /// <param name="idDispositivo"></param>
        /// <param name="agenteID"></param>
        /// <returns></returns>
        private Guid ObtenerNumeroReporte(Guid idRegistro, Guid idDispositivo, Guid agenteID)
        {
            Guid numeroReporte = Guid.Empty; // Generar un nuevo GUID como ejemplo

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new NpgsqlCommand("SELECT numero_reporte FROM obtener_numero_registro_torres_celulares(@id_registro, @id_dispositivo, @id_agente)", connection);

                command.Parameters.AddWithValue("id_registro", idRegistro);
                command.Parameters.AddWithValue("id_dispositivo", idDispositivo);
                command.Parameters.AddWithValue("id_agente", agenteID);

                
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    // Asumimos que el resultado es un GUID
                    numeroReporte = reader.GetGuid(0);
                }
                else
                {
                    // Si no hay resultado, lo dejo vacio
                    numeroReporte = Guid.Empty;
                }

                Console.WriteLine($"CargarRegistroTorreCelular -> Carga exitosa");
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            return numeroReporte;
        }

        #endregion

        #region Ubicacion GPS
        private void CargarUbicacion(Ubicacion ubicacion)
        {
            // La idea es que el mensaje sea un objeto JSON con la siguiente estructura
            // Uso UUID fijos para las pruebas inicialies, solo recibo la latitud y longitud en un principio
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var ubiTimestamp = DateTime.Now; //ubicacion.Timestamp;
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");

            // Convertimos latitud y longitud a double
            double latitud = double.Parse(ubicacion.Latitud, CultureInfo.InvariantCulture);
            double longitud = double.Parse(ubicacion.Longitud, CultureInfo.InvariantCulture);

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new NpgsqlCommand("CALL cargar_ubicacion(@idRegistro, @idDispositivo, @latitud, @longitud, @ubiTimestamp, @agenteID)", connection);
                command.Parameters.AddWithValue("idRegistro", idRegistro);
                command.Parameters.AddWithValue("idDispositivo", idDispositivo);
                command.Parameters.AddWithValue("latitud", latitud);
                command.Parameters.AddWithValue("longitud", longitud);
                command.Parameters.AddWithValue("ubiTimestamp", ubiTimestamp);
                command.Parameters.AddWithValue("agenteID", agenteID);
                command.ExecuteNonQuery();

                Console.WriteLine($"CargarUbicacion -> Carga exitosa");
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void CargarDatosRastreador(ref Ubicacion ubicacion, string str)
        {
            var campos = str.Split(',');

            // si cumple esto recibi un mensaje con coordenadas
            if (campos.Length > 0 && campos[1] == "GNSS")
            {
                ubicacion.Latitud = campos[2];
                ubicacion.Longitud = campos[3];
                Console.WriteLine($"Coordenadas GNSS [{ubicacion.Latitud};{ubicacion.Longitud}]");
            }
            else
            {
                Console.WriteLine("El mensaje no contiene datos GNSS");
            }
        }

        #endregion
        public void Stop()
        {
            _listener.Stop();
        }
    }
}
