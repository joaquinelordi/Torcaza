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
            buffer = "eyJhbGciOiAiSFMyNTYiLCJ0eXAiOiJKV1QifQ.eyJUeXBlIjoiTU5NTiIsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzIiLCJDSUQiOiI2MkEzQjBCIiwiU0xWTCI6LTYxLjAwLCJURUNIIjo3LCJSRUdTIjoxLCJDSE5MIjoyODUwLCJCQU5EIjoiTFRFIEJBTkQgNyIsIlRJTUUiOiIxNDA1MjUyMjIyMDQiLCJCU1RBIjowLCJCTFZMIjo5MiwiU0lNVSI6MCwiQVgiOi0wLjAwLCJBWSI6MC4wMCwiQVoiOi0wLjAxLCJZQVciOi0yMi4xNiwiUk9MTCI6LTIuNjMsIlBUQ0giOi02LjU0LCJOZWlnaGJvcnMiOlt7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0Y1IiwiQ0lEIjoiMTUyOSIsIlNMVkwiOjQxLjAwfSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0Y1IiwiQ0lEIjoiMTUyNyIsIlNMVkwiOjIxLjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjA1IiwiQ0lEIjoiN0ExQUIwRCIsIlNMVkwiOi05NS4wMH0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiM0IwNSIsIkNJRCI6IjdBMUFCMDEiLCJTTFZMIjotOTcuMDB9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjNCMDUiLCJDSUQiOiI3QTFBQjAxIiwiU0xWTCI6LTk3LjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjA1IiwiQ0lEIjoiN0ExQUIwNyIsIlNMVkwiOi0xMDcuMDB9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzEwLCJMQUMiOiIxQkQ3IiwiQ0lEIjoiNzUzNCIsIlNMVkwiOjM3LjAwfSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiMUJENyIsIkNJRCI6IjQwRjgiLCJTTFZMIjoyMS4wMH0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozMTAsIkxBQyI6IkRGNTciLCJDSUQiOiJBMDYiLCJTTFZMIjotOTMuMDB9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzEwLCJMQUMiOiJERjU3IiwiQ0lEIjoiQTAzIiwiU0xWTCI6LTEwMC4wMH0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozMTAsIkxBQyI6IkRGNTciLCJDSUQiOiJBMDMiLCJTTFZMIjotMTAyLjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiREY1NyIsIkNJRCI6IkEwOSIsIlNMVkwiOi0xMDUuMDB9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzEwLCJMQUMiOiJERjU3IiwiQ0lEIjoiQTBDIiwiU0xWTCI6LTEwNS4wMH0seyJURUNIIjozLCJNQ0MiOjcyMiwiTU5DIjo3LCJMQUMiOiIxMTc4IiwiQ0lEIjoiMUNCNkJBIiwiU0xWTCI6LTY1LjAwfV19.KmxUtvCJ_o4iEhgyyYXQuzMi3p5DvEFfNcRUufiNELg";

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
                    foreach (var infoCell in infoTorreCelulares)
                    {
                        CargarRegistroTorreCelular(infoCell);
                    }
                    break;

                case eTipoMensaje.TraduciSoyDeBoke:
                    Console.WriteLine($"ProcesarMensaje -> Traduci ameo, soy de Boca: {mensaje}");
                    break;
            }

        }

        //No se usa mas, pasamos a formato JSON
        private eTipoMensaje IdentificarTipoMensaje(string mensaje)
        {
            eTipoMensaje tipoMensaje;
            var campos = mensaje.Split(',');

            // si cumple esto recibi un mensaje con coordenadas
            if (campos.Length > 0 && campos[1] == "GNSS")
            {
                tipoMensaje = eTipoMensaje.GNSS;
            }
            else if (campos.Length > 0 && campos[1] == "MN")
            {
                tipoMensaje = eTipoMensaje.InfoCell;
            }
            else
            {
                tipoMensaje = eTipoMensaje.TraduciSoyDeBoke;
            }

            Console.WriteLine($"IdentificatTipoMensaje -> Tipo de mensaje: {tipoMensaje}");
            return tipoMensaje;
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


            /*
            List<string> registros = parsearRegistros(str);
            foreach (string registro in registros)
            {
                var campos = registro.Split(',');

                // Aca parseo los datos de las torres celulares  
                InfoCell infoCell = new InfoCell();
                infoCell.Mcc = BigInteger.Parse(campos[2]); // Conversión explícita de string a BigInteger  
                infoCell.Mnc = BigInteger.Parse(campos[3]); // Conversión explícita de string a BigInteger  
                infoCell.Lac = campos[4];
                infoCell.Cellid = campos[5];
                infoTorreCelulares.Add(infoCell);
                Console.WriteLine($" [{infoCell.Mcc};{infoCell.Mnc};{infoCell.Lac};{infoCell.Cellid}]");                   
            }
            */
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
        /// Metodo encargado de cargar el registro de la torre celular en la base de datos
        /// </summary>
        /// <param name="infoCell"></param>
        private void CargarRegistroTorreCelular(InfoCell infoCell)
        {
            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");

            // Este campo hay que definir a partir de que se crea, es el que controla que datos de torre celulares
            // fueron enviados desde el mismo punto
            // y en el mismo momento, para no cargar datos repetidos
            var numeroReporte = Guid.Parse("550e8400-e29b-41d4-a716-446655440006");

            // Convertimos la fecha y hora al formato UTC
            DateTime cellTimestamp = infoCell.FechaHora.ToUniversalTime();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Comando del SP
                using var command = new NpgsqlCommand("CALL cargar_ReporteCeldaCelular(@cell_id, @cell_mcc, @cell_mnc, @cell_lac, @cell_tecnologia, @cell_band, @cell_chanel, @cell_nivelsenial, @cell_timestamp, @numeroreporte, @cell_longitud, @cell_latitud, @id_registro, @id_dispositivo, @id_agente)", connection);

                // Agrego los parametros
                // Agregar parámetros con los tipos correctos
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

                command.Parameters.AddWithValue("id_registro", idRegistro); // Respetar mayúsculas y minúsculas
                command.Parameters.AddWithValue("id_dispositivo", idDispositivo); // Respetar mayúsculas y minúsculas
                command.Parameters.AddWithValue("id_agente", agenteID); // Respetar mayúsculas y minúsculas


                // Debug
                var queryDebug = new StringBuilder(command.CommandText);
                foreach (NpgsqlParameter param in command.Parameters)
                {
                    queryDebug.AppendLine()
                              .Append($"-- {param.ParameterName} = {param.Value}");
                }

                string consultaCompleta = queryDebug.ToString();
                Console.WriteLine("Consulta SQL generada:");
                Console.WriteLine(consultaCompleta);

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

    //No se usa mas, usamos el de TrackerPayloadBase
    /*
    enum eTipoMensaje
    {
        GNSS = 0,
        InfoCell,
        TraduciSoyDeBoke
    }
    */
}
