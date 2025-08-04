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
using System;
using NLog;

namespace ServidorTCP
{
    public class TcpServer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
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
            _logger.Info($"Servidor TCP iniciado... {_ipAddress}:{_port}");
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
            //buffer = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJUeXBlIjoiTU5NTiIsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzAiLCJDSUQiOiI2MUVCRDAyIiwiU0xWTCI6LTYzLCJURUNIIjo3LCJSRUdTIjoxLCJDSE5MIjoyMDAwLCJCQU5EIjoiTFRFIEJBTkQgNCIsIlRJTUUiOiIwNDA2MjUxOTQzMjkiLCJCU1RBIjowLCJCTFZMIjo4MCwiU0lNVSI6MCwiQVgiOjAuMDEsIkFZIjowLjAyLCJBWiI6MCwiWUFXIjotMTEzLjI5LCJST0xMIjotMi4wNSwiUFRDSCI6LTUuOTMsIk5laWdoYm9ycyI6W3siVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiJBNzY4IiwiU0xWTCI6LTY3fSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTNCMSIsIlNMVkwiOi02OX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjE2REUiLCJTTFZMIjotNzB9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiIxNzNEIiwiU0xWTCI6LTc1fSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTNCMiIsIlNMVkwiOi03NX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjE2REYiLCJTTFZMIjotNzd9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiIxNjZCIiwiU0xWTCI6LTc5fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjAyIiwiQ0lEIjoiN0EyM0QwMSIsIlNMVkwiOi05Mn0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiM0IwMiIsIkNJRCI6IjdBMTJFMEUiLCJTTFZMIjotOTR9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjNCMDIiLCJDSUQiOiI3QTIzRDAxIiwiU0xWTCI6LTk1fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjAyIiwiQ0lEIjoiN0ExMkUwNiIsIlNMVkwiOi05OX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozMTAsIkxBQyI6IjFCRDciLCJDSUQiOiI2OEE3IiwiU0xWTCI6LTc3fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiREYxMSIsIkNJRCI6IjRDMjA1IiwiU0xWTCI6LTgyfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiREYxMiIsIkNJRCI6IjQ4NjAyIiwiU0xWTCI6LTg3fV19.KTE0NZbVORx0IsuUvVR3jCh7tdV3cHahOQ4Qlle3G3o"; 

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
            TrackerPayloadBase payloadBase = JsonDeserializer.DeserializeTrackerPayload(mensaje);
            eTipoMensaje accion = payloadBase.GetTipoMensaje();

            //Aca la idea es usar un enum con los tipos de acciones disponibles, enviado en el objeto del mensaje 
            long numeroEvento = CargarEvento(payloadBase);    
        }

        public void ProcesarMensajeExterno(string buffer)
        {
            // la idea es que se estraiga del body de un HTTP POST y se pase al metodo el jwt o el json que se envie desde un cliente externo
            Console.WriteLine($"ProcesarMensajeExterno -> Inicio: {buffer}");
            ProcesarMensaje(buffer);
            
        }

        /// <summary>
        /// Metodo encargado de cargar el evento en la base de datos y devolver el numero de evento
        /// </summary>
        /// <param name="payloadBase"></param>
        /// <returns></returns>
        public long CargarEvento(TrackerPayloadBase payloadBase)
        {
            List<InfoCell> infoTorreCelulares = new List<InfoCell>();
            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            // Aca deberia cargar el evento en la base de datos y devolver el numero de evento
            long numeroEvento;
            DateTime fechaMensaje = DateTime.UtcNow;//payloadBase.DateTime.ToUniversalTime();
            eTipoMensaje tipoMensaje = payloadBase.GetTipoMensaje();
            string JsonEstadoBateria = "{\"Type\":\"BATERIA\"}";
            string JsonUbicacion = "{\"Type\":\"GPS\"}";
            string JsonInfoCell = "{\"Type\":\"INFOCELL\"}";
            string JsonGiroscopio = "{\"Type\":\"INFO GIROSCOPIO\"}";

            try 
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Comando del SP
                using var command = new NpgsqlCommand("SELECT id_evento FROM grabar_evento(@id_registro, @id_dispositivo, @tipo_mensaje, @estado_bateria, @gpsInfo, @gsmInfo, @fecha_mensaje, @giroscopioInfo)", connection);

                command.Parameters.AddWithValue("id_registro", idRegistro);
                command.Parameters.AddWithValue("id_dispositivo", idDispositivo);
                command.Parameters.AddWithValue("tipo_mensaje", Convert.ToInt16(tipoMensaje));
                command.Parameters.AddWithValue("estado_bateria", NpgsqlDbType.Jsonb, JsonEstadoBateria);
                command.Parameters.AddWithValue("gpsInfo", NpgsqlDbType.Jsonb, JsonUbicacion);
                command.Parameters.AddWithValue("gsmInfo", NpgsqlDbType.Jsonb, JsonInfoCell);
                command.Parameters.AddWithValue("fecha_mensaje", fechaMensaje);
                command.Parameters.AddWithValue("giroscopioInfo", NpgsqlDbType.Jsonb, JsonGiroscopio);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    // Asumimos que el resultado es un long
                    numeroEvento = reader.GetInt64(0);
                }
                else
                {
                    // Si no hay resultado, lo dejo vacio
                    numeroEvento = -1; // o cualquier valor que indique error
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                numeroEvento = -1; // o cualquier valor que indique error
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                numeroEvento = -1; // o cualquier valor que indique error
            }

            Console.WriteLine($"CargarEvento -> Evento cargado con numero: {numeroEvento}");
            if (numeroEvento > 0)
            {
                string mensaje = JsonConvert.SerializeObject(payloadBase, Formatting.Indented);
                switch (tipoMensaje)
                {
                    case eTipoMensaje.GNSS:
                        //
                        //Hago la traduccion de datos crudos del rastreado y los cargo en la base
                        Ubicacion ubicacion = new Ubicacion();
                        infoTorreCelulares.Add(new InfoCell(payloadBase.CellInfo));
                        BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                        //TODO: refactorizar
                        CargarDatosRastreador(ref ubicacion, payloadBase);
                        CargarListaRegistroTorreCelular(ref infoTorreCelulares, numeroEvento);
                        CargarUbicacion(ubicacion, numeroEvento);
                        break;

                    case eTipoMensaje.InfoCell:
                        //
                        CargarDatosTorresCelulares(ref infoTorreCelulares, payloadBase);
                        BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                        // TODO: por ahora solo calculo un radio unico para cada torre celular
                        CalcularRadioTorreCelular(ref infoTorreCelulares);
                        CargarListaRegistroTorreCelular(ref infoTorreCelulares, numeroEvento);
                        break;

                    case eTipoMensaje.TipoDesconocido:
                        Console.WriteLine($"ProcesarMensaje -> Tipo de mensaje desconocido: {mensaje}");
                        break;
                }
            }
            return numeroEvento;
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
            int i = 0;
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

                        i = i++;
                        _logger.Debug($"Coordenadas encontradas: MCC={infoCell.Mcc}, MNC={infoCell.Mnc}, LAC={infoCell.Lac}, CellID={infoCell.Cellid}, Lat={infoCell.Lat}, Lon={infoCell.Lon}");
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
            _logger.Debug($"Cantidad de coordenadas encontradas: [{0}]", i);
        }

        /// <summary>
        /// Metodo encargado de calcular el radio de distancia a cada torre celular
        /// </summary>
        /// <param name="infoTorreCelulares"></param>
        private void CalcularRadioTorreCelular(ref List<InfoCell> infoTorreCelulares)
        {
            _logger.Debug($"CalcularRadioTorreCelular -> Inicio");

            foreach (var infoCell in infoTorreCelulares)
            {
                if (infoCell.IsInDatabase)
                {
                    // TODO, ahora a mano, luego un abstract factory para diferentes tipos de modelos
                    HandlerCanalInalambrico handlerCanalInalambrico = new HandlerCanalInalambrico()
                    {
                        Banda = infoCell.Banda,
                        TecnologiaAcceso = infoCell.TecnologiaAcceso,
                        Canal = infoCell.Canal,
                        SenialdB = infoCell.senialdB,
                        Latitud = infoCell.Lat,
                        Longitud = infoCell.Lon
                    };

                    // con el Handler calculo un radio de distancia a la torre celular, luego va a ser un objeto
                    // que tenga mas informacion del calculo para sacar un intervalo de distancia y hacer anillor para triangulacion
                    //infoCell.Radio = handlerCanalInalambrico.CalcularDistanciaATorreCelular();
                    infoCell.Radio = "0";
                }
                else
                {
                    // Radio vacio por defecto
                    infoCell.Radio = " ";
                }
            }
            _logger.Debug($"CalcularRadioTorreCelular -> Fin");
        }





        /// <summary>
        /// obtiene un numero de registro y carga los infocell
        /// </summary>
        /// <param name="listaInfoCell"></param>
        private void CargarListaRegistroTorreCelular(ref List<InfoCell> listaInfoCell, long numeroEvento)
        {
            Guid numeroReporte = Guid.Empty;

            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440003");

            numeroReporte = ObtenerNumeroReporte(idRegistro, idDispositivo, agenteID);

            if (numeroReporte != Guid.Empty)
            {
                foreach (InfoCell infoCell in listaInfoCell)
                {
                    CargarRegistroTorreCelular(infoCell, numeroReporte, numeroEvento);
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
        private void CargarRegistroTorreCelular(InfoCell infoCell, Guid numeroReporte, long numeroEvento)
        {
            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440003");

            // Convertimos la fecha y hora al formato UTC
            DateTime cellTimestamp = infoCell.FechaHora.ToUniversalTime();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Comando del SP
                using var command = new NpgsqlCommand("CALL cargar_ReporteCeldaCelular(@cell_id, @cell_nroevento, @cell_mcc, @cell_mnc, @cell_lac, @cell_tecnologia, @cell_band, @cell_chanel, @cell_nivelsenial, @cell_timestamp, @numeroreporte, @cell_longitud, @cell_latitud, @id_registro, @id_dispositivo, @id_agente)", connection);

                // Agrego los parametros
                command.Parameters.AddWithValue("cell_id", Convert.ToInt64(infoCell.Cellid, 16)); //string de un hexadecimal
                command.Parameters.AddWithValue("cell_nroevento", (long)numeroEvento);
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
        private void CargarUbicacion(Ubicacion ubicacion, long numeroEvento)
        {
            // La idea es que el mensaje sea un objeto JSON con la siguiente estructura
            // Uso UUID fijos para las pruebas inicialies, solo recibo la latitud y longitud en un principio
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var ubiTimestamp = DateTime.Now; //ubicacion.Timestamp;
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440003");

            // Convertimos latitud y longitud a double
            //double latitud = double.Parse(ubicacion.Latitud, CultureInfo.InvariantCulture);
            //double longitud = double.Parse(ubicacion.Longitud, CultureInfo.InvariantCulture);
            double latitud = ubicacion.Latitud;
            double longitud = ubicacion.Longitud;

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new NpgsqlCommand("CALL cargar_ubicacion(@idRegistro, @idDispositivo, @numero_evento, @latitud, @longitud, @ubiTimestamp, @agenteID)", connection);
                command.Parameters.AddWithValue("idRegistro", idRegistro);
                command.Parameters.AddWithValue("idDispositivo", idDispositivo);
                command.Parameters.AddWithValue("numero_evento", (long)numeroEvento);
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

        /*
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
         */

        private void CargarDatosRastreador(ref Ubicacion ubicacion, TrackerPayloadBase oPayload)
        {
            if (oPayload != null)
            {
                try
                {
                    // casteo el objeto, que ya se que es y asi inicializo infocell
                    GnssInfoPayload gnssInfoPayload = oPayload as GnssInfoPayload;
                    ubicacion = new Ubicacion(gnssInfoPayload);
                }
                catch (Exception e)
                {
                    _logger.Error($"Error al cargar DatosRastreador: {e.Message}");
                }
            }
        }

        #endregion
        public void Stop()
        {
            _listener.Stop();
        }

        #region Cerco Virtual
        /// <summary>
        /// Metodo encargado de cargar los cercos virtuales en la base de datos
        /// </summary>
        /// <param name="cercos"></param>
        public void CargarEventoCercoVirtual(List<ICercoVirtual> cercos)
        {
            if (cercos == null || cercos.Count == 0)
            {
                _logger.Debug("CargarEventoCercoVirtual -> No se recibieron cercos virtuales para cargar.");
                return;
            }

            _logger.Debug($"CargarEventoCercoVirtual -> Inicio, cantidad de cercos: {cercos.Count}");

            try
            {
                var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 22185);

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                foreach (var cerco in cercos)
                {
                    Geometry geom = null;
                    string nombre = $"Cerco_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

                    switch (cerco)
                    {
                        case CercoCirculo circulo:
                            // SRID 4326 (WGS84), las unidades son grados decimales (latitud/longitud).
                            double radioGrados = circulo.Radio / 111320.0;
                            var centro = geometryFactory.CreatePoint(new Coordinate(circulo.Lng, circulo.Lat));
                            geom = centro.Buffer(radioGrados); // radio en grados
                            break;

                        case CercoRectangulo rect:
                            var coords = new[]
                            {
                                new Coordinate(rect.SurOesteLng, rect.SurOesteLat),
                                new Coordinate(rect.NorEsteLng, rect.SurOesteLat),
                                new Coordinate(rect.NorEsteLng, rect.NorEsteLat),
                                new Coordinate(rect.SurOesteLng, rect.NorEsteLat),
                                new Coordinate(rect.SurOesteLng, rect.SurOesteLat)
                            };
                            geom = geometryFactory.CreatePolygon(coords);
                            break;
                    }

                    if (geom != null)
                    {
                        using var cmd = new NpgsqlCommand(
                            "INSERT INTO cercos_virtuales (cerco_nombre, cerco_geom) VALUES (@nombre, @geom)", connection);
                        cmd.Parameters.AddWithValue("nombre", nombre);
                        cmd.Parameters.AddWithValue("geom", geom);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                _logger.Error($"Error al cargar los cercos virtuales: {ex.Message}");
            }
            catch (Exception e)
            {
                _logger.Error($"Error al cargar cercos virtuales: {e.Message}");
            }
        }

// Update the method to resolve the type mismatch issues by ensuring the correct types are used.  
// The issue arises because `GeometryTransform.TransformGeometry` expects `IGeometryFactory` and `IGeometry` from GeoAPI,  
// but the code is using `NetTopologySuite.Geometries.GeometryFactory` and `NetTopologySuite.Geometries.Geometry`.  
// To fix this, ensure the correct namespaces and types are used.

public static class GeometryUtils
        {
            public static Geometry Transform4326To22185(Geometry geometry4326)
            {
                if (geometry4326 == null) return null;

                // Define los sistemas de coordenadas
                var sourceCS = GeographicCoordinateSystem.WGS84;

                // EPSG:22185 - POSGAR 2007 / Argentina 5 (Gauss-Kruger)
                var targetCS = ProjectedCoordinateSystem.WGS84_UTM(21, true); // TEMPORAL: reemplazar con factory personalizado si necesitás precisión

                var transformFactory = new CoordinateTransformationFactory();
                var transformation = transformFactory.CreateFromCoordinateSystems(sourceCS, targetCS);
                var mathTransform = transformation.MathTransform;

                // Clona la geometría para transformarla
                var coords = geometry4326.Coordinates;
                for (int i = 0; i < coords.Length; i++)
                {
                    var transformed = mathTransform.Transform(new[] { coords[i].X, coords[i].Y });
                    coords[i].X = transformed[0];
                    coords[i].Y = transformed[1];
                }

                var factory = new GeometryFactory(new PrecisionModel(), 22185);
                var transformedGeometry = factory.CreateGeometry(geometry4326);

                return transformedGeometry;
            }
        }
        #endregion

        #region Alarmas

        #endregion
    }
}
