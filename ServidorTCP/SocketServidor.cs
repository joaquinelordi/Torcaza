using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Diagnostics;
using NLog;
using Newtonsoft.Json;
using Entidades;
using Npgsql;
using NpgsqlTypes;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using NetTopologySuite.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using Entidades.Interfaces;
using System.Numerics;
using System.Data;
using ModuloAlertas;



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
        private readonly IModuloAlertas _moduloAlertas;


        public event EventHandler<string> MensajeRecibido;
        public TcpServer(string ipAddress, int port, OpenCellID openCellID, HandlerJWT handlerJWT, IModuloAlertas moduloAlertas)
        {
            _ipAddress = ipAddress;
            _port = port;
            _openCellID = openCellID;
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01;Include Error Detail=true;";
            _handlerJWT = handlerJWT;
            _moduloAlertas = moduloAlertas;
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

        private long ProcesarMensaje(string buffer)
        {
            Console.WriteLine($"ProcesarMensaje -> Inicio: {buffer}");
            string mensaje = "";
            string payload = "";
            // INFOCELL
            //buffer = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJUeXBlIjoiTU5NTiIsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzAiLCJDSUQiOiI2MUVCRDAyIiwiU0xWTCI6LTYzLCJURUNIIjo3LCJSRUdTIjoxLCJDSE5MIjoyMDAwLCJCQU5EIjoiTFRFIEJBTkQgNCIsIlRJTUUiOiIwNDA2MjUxOTQzMjkiLCJCU1RBIjowLCJCTFZMIjo4MCwiU0lNVSI6MCwiQVgiOjAuMDEsIkFZIjowLjAyLCJBWiI6MCwiWUFXIjotMTEzLjI5LCJST0xMIjotMi4wNSwiUFRDSCI6LTUuOTMsIk5laWdoYm9ycyI6W3siVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiJBNzY4IiwiU0xWTCI6LTY3fSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTNCMSIsIlNMVkwiOi02OX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjE2REUiLCJTTFZMIjotNzB9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiIxNzNEIiwiU0xWTCI6LTc1fSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTNCMiIsIlNMVkwiOi03NX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjE2REYiLCJTTFZMIjotNzd9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiIxNjZCIiwiU0xWTCI6LTc5fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjAyIiwiQ0lEIjoiN0EyM0QwMSIsIlNMVkwiOi05Mn0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiM0IwMiIsIkNJRCI6IjdBMTJFMEUiLCJTTFZMIjotOTR9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjNCMDIiLCJDSUQiOiI3QTIzRDAxIiwiU0xWTCI6LTk1fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjAyIiwiQ0lEIjoiN0ExMkUwNiIsIlNMVkwiOi05OX0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozMTAsIkxBQyI6IjFCRDciLCJDSUQiOiI2OEE3IiwiU0xWTCI6LTc3fSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiREYxMSIsIkNJRCI6IjRDMjA1IiwiU0xWTCI6LTgyfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjMxMCwiTEFDIjoiREYxMiIsIkNJRCI6IjQ4NjAyIiwiU0xWTCI6LTg3fV19.KTE0NZbVORx0IsuUvVR3jCh7tdV3cHahOQ4Qlle3G3o"; 

            // El mensaje puede ser un JWT, debo extraer el payload de ser necesario
            if (_handlerJWT.StringEsJWTValido(buffer))
            {
                var estadoJWT = _handlerJWT.ProcesarPayloadCompleto(buffer, ref payload);
                if (estadoJWT == eEstadoJWT.OK)
                {
                    mensaje = payload;
                    _logger.Debug($"ProcesarMensaje -> Payload decodificado: {JsonConvert.SerializeObject(payload, Formatting.Indented)}");
                }
                else
                {
                    _logger.Debug($"ProcesarMensaje -> Error al decodificar el JWT: {estadoJWT.ToString()}");
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

            return numeroEvento;
        }

        public void ProcesarMensajeExterno(string buffer)
        {
            long numeroEvento;
            // la idea es que se estraiga del body de un HTTP POST y se pase al metodo el jwt o el json que se envie desde un cliente externo
            _logger.Debug($"ProcesarMensajeExterno -> Inicio: {buffer}");
            numeroEvento = ProcesarMensaje(buffer);

            if (numeroEvento != 0)
            {
                _logger.Debug($"ProcesarMensajeExterno -> Evento cargado con numero: {numeroEvento}");
                _moduloAlertas.ProcesarEvento(numeroEvento);
            }
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
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");
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
                        infoTorreCelulares.Add(new InfoCell(payloadBase.CellInfoRemote));
                        BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                        //TODO: refactorizar
                        CargarDatosRastreador(ref ubicacion, payloadBase);
                        CargarListaRegistroTorreCelular(ref infoTorreCelulares, numeroEvento);
                        CargarUbicacion(ubicacion, numeroEvento);
                        break;

                    case eTipoMensaje.InfoCell:
                        //
                        Ubicacion ubicacionEstimada = new Ubicacion();
                        CargarDatosTorresCelulares(ref infoTorreCelulares, payloadBase);
                        BuscarCoordenadasTorresCelulares(ref infoTorreCelulares);
                        // TODO: devuelve una ubicacion con latitud y longitud estimada y singma en base a las torres celulares encontradas
                        EstimarUbicacion(ref infoTorreCelulares, ref ubicacionEstimada);
                        CargarListaRegistroTorreCelular(ref infoTorreCelulares, numeroEvento);
                        CargarUbicacion(ubicacionEstimada, numeroEvento);
                        break;

                    case eTipoMensaje.TipoDesconocido:
                        Console.WriteLine($"ProcesarMensaje -> Tipo de mensaje desconocido: {mensaje}");
                        break;
                }
            }
            return numeroEvento;
        }

        private void EstimarUbicacion(ref List<InfoCell> infoTorreCelulares, ref Ubicacion ubicacionEstimada)
        {
            IList<InfoCell> infoTorreCelularesAsIList = infoTorreCelulares;
            //List<RangoEstimado> rangosEstimados = (List<RangoEstimado>)CalcularRadioTorreCelular(ref infoTorreCelularesAsIList);
            
            // en base a la lista de torres y sus ubicaciones, setea los valores de los parametros del modelo 
            ObtenerParametros(ref infoTorreCelulares);
            //Cambiar gps a srid

            DatosSalida posicion = CalcularPosicion(ref infoTorreCelularesAsIList);

        }

        #region Torres Celulares

        private void ObtenerParametros(ref List<InfoCell> infoTorreCelulares)
        {
            if (infoTorreCelulares == null || infoTorreCelulares.Count == 0)
                return;

            var dCellId = new Dictionary<InfoCell, long>(infoTorreCelulares.Count);
            var lCellIds = new List<long>();

            foreach (var infoCell in infoTorreCelulares)
            {
                try
                {
                    var id = long.Parse(infoCell.Cellid, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                    dCellId[infoCell] = id;
                    lCellIds.Add(id);
                }
                catch (Exception ex)
                {
                    // Si no puede parsear, dejás modelo vacío para ese item
                    infoCell.ParametrosModelo = new ParametrosModelo { RSSI = infoCell.senialdB };
                    _logger.Error($"ObtenerParametros -> CellID inválido '{infoCell.Cellid}': {ex.Message}");
                }
            }

            if (lCellIds.Count == 0)
                return;

            var parametrosPorId = new Dictionary<long, (double betha0, double b, short tipoArea)>(lCellIds.Count);


            // Primero hay que ver si ya tengo en la base los parametros del modelo para cada torre celular
            //busco en la base de datos:
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand(@"SELECT param_id, param_betha0, param_b, param_tipoarea FROM parametros_celdas_celulares WHERE param_id = ANY(@cellids)", conn);

                    cmd.Parameters.Add("@ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = lCellIds.ToArray();
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var id = reader.GetInt64(0);
                        var betha0 = reader.GetDouble(1);
                        var b = reader.GetDouble(2);
                        var tipoArea = reader.GetInt16(3);

                        parametrosPorId[id] = (betha0, b, tipoArea);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"ObtenerParametros -> Error inesperado: {ex.Message}");
                    foreach (var infoCell in infoTorreCelulares)
                        infoCell.ParametrosModelo = infoCell.ParametrosModelo ?? new ParametrosModelo { RSSI = infoCell.senialdB };

                }
            }

            // Ahora asigno los parametros a cada torre celular sean de la base o por defecto
            foreach (var infoCell in infoTorreCelulares)
            {
                // si no se pudo parsear el cellid, ya queda con el modelo por defecto
                if (!dCellId.TryGetValue(infoCell, out var id))
                    continue;

                if (parametrosPorId.TryGetValue(id, out var p))
                {
                    infoCell.ParametrosModelo = new ParametrosModelo
                    {
                        Betha0 = p.betha0,
                        B = p.b,
                        TipoArea = (eTipoArea)p.tipoArea,
                        RSSI = infoCell.senialdB
                    };
                    _logger.Debug($"ObtenerParametros -> Parametros encontrados para torre celular CellID={infoCell.Cellid}");
                }
                else
                {
                    infoCell.ParametrosModelo = new ParametrosModelo { RSSI = infoCell.senialdB };
                    _logger.Debug($"ObtenerParametros -> No se encontraron parametros para torre celular CellID={infoCell.Cellid}");
                }
            }
        }

        private void CargarDatosTorresCelulares(ref List<InfoCell> infoTorreCelulares, TrackerPayloadBase oPayload)
        {
            // casteo el objeto, que ya se que es y asi inicializo infocell
            CellNeighborsInfoPayload cellNeighborsInfo = oPayload as CellNeighborsInfoPayload;

            // inicializo el infocell a partir de la celda celular usada para transmitir
            InfoCell infoCell = new InfoCell(cellNeighborsInfo.CellInfoRemote);
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
                if (infoCell.IsInDatabase)
                {
                    //TODO: buscar en la base de datos local primero antes de ir a OpenCellID
                    try
                    {
                        // TODO: implementar la busqueda en la base de datos local

                        // inicializar Parametros de modelo si es que tienen
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"BuscarCoordenadasTorresCelularesAsync -> Error inesperado: {ex.Message}");
                    }
                }
                else
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
            }
            _logger.Debug($"Cantidad de coordenadas encontradas: [{0}]", i);
        }

        /// <summary>
        /// Metodo encargado de calcular el radio de distancia a cada torre celular
        /// </summary>
        /// <param name="infoTorreCelulares"></param>
        private IList<RangoEstimado> CalcularRadioTorreCelular(ref IList<InfoCell> infoTorreCelulares)
        {
            _logger.Debug($"CalcularRadioTorreCelular -> Inicio");
            List<RangoEstimado> rangoEstimados = new List<RangoEstimado>();

            foreach (var infoCell in infoTorreCelulares)
            {
                if (infoCell.IsInDatabase)
                {
                    // TODO, ahora a mano, luego un abstract factory para diferentes tipos de modelos
                    HandlerCanalInalambrico handlerCanalInalambrico = new HandlerCanalInalambrico()
                    {
                        CelId = infoCell.Cellid,
                        Banda = infoCell.Banda,
                        TecnologiaAcceso = infoCell.TecnologiaAcceso,
                        Canal = infoCell.Canal,
                        SenialdB = infoCell.senialdB,
                        Latitud = infoCell.Lat,
                        Longitud = infoCell.Lon
                    };
                    handlerCanalInalambrico.Inicializar(ref infoTorreCelulares);

                    // con el Handler calculo un radio de distancia a la torre celular, luego va a ser un objeto
                    // que tenga mas informacion del calculo para sacar un intervalo de distancia y hacer anillo para triangulacion
                    RangoEstimado rango = handlerCanalInalambrico.CalcularDistanciaATorreCelular();
                    infoCell.Radio = rango.DistanciaM.ToString("F2");
                    rangoEstimados.Add(rango);
                    //infoCell.Radio = "0";
                }
                else
                {
                    // Radio vacio por defecto
                    infoCell.Radio = " ";
                }
            }
            _logger.Debug($"CalcularRadioTorreCelular -> Fin");
            return rangoEstimados;
        }

        private DatosSalida CalcularPosicion(ref IList<InfoCell> infoTorreCelulares)
        {

            HandlerCanalInalambrico handlerCanalInalambrico = new HandlerCanalInalambrico();

            // Cargo los datos de las torres celulares para inicializar el handler
            handlerCanalInalambrico.Inicializar(ref infoTorreCelulares);


            List<InfoCell> torresCelularesEnDatabase = infoTorreCelulares.ToList().FindAll(t => t.IsInDatabase);
            Dictionary<long, CellInfo> cell = new Dictionary<long, CellInfo>();

            try
            {
                foreach (var torre in torresCelularesEnDatabase)
                {
                    CellInfo cellInfo = new CellInfo(
                        Convert.ToInt64(torre.Cellid, 16),
                        torre.Lon.HasValue ? torre.Lon.Value : 0.0,
                        torre.Lat.HasValue ? torre.Lat.Value : 0.0,
                        torre.TecnologiaAcceso,
                        torre.Banda,
                        torre.Canal,
                        null,
                        eTipoArea.Desconocido,
                        torre.senialdB,
                        torre.ParametrosModelo.Betha0,
                        torre.ParametrosModelo.B
                    );
                    cell.Add(cellInfo.CellId, cellInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"CalcularPosicion -> Error armando el diccionario de torres celulares: {ex.Message}");

            }
            try
            {
                return handlerCanalInalambrico.CalcularPosicion(cell);
            }
            catch (Exception ex)
            {
                _logger.Error($"CalcularPosicion -> Error en handler al calcular prosocion: {ex.Message}");
                return new DatosSalida();
            }
        }

        /// <summary>
        /// obtiene un numero de registro y carga los infocell
        /// </summary>
        /// <param name="listaInfoCell"></param>
        private void CargarListaRegistroTorreCelular(ref List<InfoCell> listaInfoCell, long numeroEvento)
        {
            Guid numeroReporte = Guid.Empty;

            // estos campos los deberia saber o el servidor a partir de un UUID del dispositivo
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");

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
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");

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
            var idRegistro = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");
            var idDispositivo = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
            var ubiTimestamp = DateTime.Now; //ubicacion.Timestamp;
            var agenteID = Guid.Parse("550e8400-e29b-41d4-a716-446655440005");
            ubicacion.IDAgente = agenteID.ToString();
            List<int> lCercosID = new List<int>();

            // Convertimos latitud y longitud a double
            //double latitud = double.Parse(ubicacion.Latitud, CultureInfo.InvariantCulture);
            //double longitud = double.Parse(ubicacion.Longitud, CultureInfo.InvariantCulture);
            double latitud = ubicacion.Latitud;
            double longitud = ubicacion.Longitud;
            double hdop = ubicacion.Hdop;
            double altitud = ubicacion.Altitud;

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new NpgsqlCommand("CALL cargar_ubicacion(@idRegistro, @idDispositivo, @numero_evento, @latitud, @longitud, @hdop, @altitud, @ubiTimestamp, @agenteID)", connection);
                command.Parameters.AddWithValue("idRegistro", idRegistro);
                command.Parameters.AddWithValue("idDispositivo", idDispositivo);
                command.Parameters.AddWithValue("numero_evento", (long)numeroEvento);
                command.Parameters.AddWithValue("latitud", latitud);
                command.Parameters.AddWithValue("longitud", longitud);
                command.Parameters.AddWithValue("hdop", hdop);
                command.Parameters.AddWithValue("altitud", altitud);
                command.Parameters.AddWithValue("ubiTimestamp", ubiTimestamp);
                command.Parameters.AddWithValue("agenteID", agenteID);
                command.ExecuteNonQuery();

                _logger.Debug($"CargarUbicacion -> Carga exitosa");

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
        public void CargarEventoCercoVirtual(List<CercoVirtualBase> cercos)
        {
            if (cercos == null || cercos.Count == 0)
            {
                _logger.Debug("CargarEventoCercoVirtual -> No se recibieron cercos virtuales para cargar.");
                return;
            }

            _logger.Debug($"CargarEventoCercoVirtual -> Inicio, cantidad de cercos: {cercos.Count}");

            try
            {
                var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionString);
                dataSourceBuilder.UseNetTopologySuite();
                var dataSource = dataSourceBuilder.Build();

                using var connection = dataSource.OpenConnection();

                foreach (var cerco in cercos)
                {
                    int cercoId = 0;
                    Geometry geom4326 = null;
                    string nombre = $"Cerco_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

                    switch (cerco)
                    {
                        case CercoCirculo circulo:
                            {
                                using var cmdInsert = new NpgsqlCommand(@"
                                    INSERT INTO public.cercos_virtuales (cerco_nombre, cerco_geom_4326)
                                    VALUES (
                                      @nombre,
                                      ST_Buffer(
                                        ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography,
                                        @radio_m
                                      )::geometry(Polygon, 4326)
                                    )
                                    RETURNING cerco_id;
                                    ", connection);

                                cmdInsert.Parameters.AddWithValue("nombre", nombre);
                                cmdInsert.Parameters.Add("lng", NpgsqlDbType.Double).Value = circulo.Lng;
                                cmdInsert.Parameters.Add("lat", NpgsqlDbType.Double).Value = circulo.Lat;
                                cmdInsert.Parameters.Add("radio_m", NpgsqlDbType.Double).Value = circulo.Radio;

                                cercoId = Convert.ToInt32(cmdInsert.ExecuteScalar());
                                _logger.Debug($"Círculo insertado -> id={cercoId}");
                                break;
                            }
                        case CercoRectangulo rect:
                            var coordsGPS = new[]
                            {
                                new Coordinate(rect.SurOesteLng, rect.SurOesteLat),
                                new Coordinate(rect.NorEsteLng, rect.SurOesteLat),
                                new Coordinate(rect.NorEsteLng, rect.NorEsteLat),
                                new Coordinate(rect.SurOesteLng, rect.NorEsteLat),
                                new Coordinate(rect.SurOesteLng, rect.SurOesteLat)
                            };
                            geom4326 = geometryFactory.CreatePolygon(coordsGPS);
                            geom4326.SRID = 4326;

                            var cmdCercoRect = new NpgsqlCommand(@"
                                INSERT INTO cercos_virtuales (cerco_nombre, cerco_geom_4326)
                                VALUES (@nombre, @geom_4326)
                                RETURNING cerco_id;", connection);

                            cmdCercoRect.Parameters.AddWithValue("nombre", nombre);
                            cmdCercoRect.Parameters.AddWithValue("geom_4326", geom4326);
                            //var c = cmdCercoRect.Parameters.AddWithValue("geom_4326", NpgsqlDbType.Geometry);
                            //c.Value = geom4326;
                            cercoId = Convert.ToInt32(cmdCercoRect.ExecuteScalar());
                            break;
                    }

                    if (cercoId > 0)
                    {
                        try
                        {
                            var dispositivoID = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");

                            //grabar tabla dispositivo_cerco
                            using var cmdDispCerco = new NpgsqlCommand(
                                "INSERT INTO dispositivo_cerco (dc_disp_id, dc_cerco_id, dc_tipo_alerta) VALUES (@disptoken_id, @cerco_id, @tipo_alerta)", connection);
                            cmdDispCerco.Parameters.AddWithValue("disptoken_id", dispositivoID);
                            cmdDispCerco.Parameters.AddWithValue("cerco_id", cercoId);
                            cmdDispCerco.Parameters.AddWithValue("tipo_alerta", 0);
                            cmdDispCerco.ExecuteScalar();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error al insertar cerco virtual '{nombre}': {ex.Message}");
                            continue; // saltar al siguiente cerco
                        }
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


        public Task<bool> ObtenerCercosVirtuales(string registroID, out List<string> jsonXfila )//List<CercoVirtualBase> cercos)
        {
            //cercos = new List<CercoVirtualBase>();
            jsonXfila = new List<string>();
            bool bRet = false;
            try
            {
                var registroGuid = Guid.Parse(registroID);
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string sQuery = @"
                    SELECT  cv.cerco_id as cerco_id, cv.cerco_nombre as cerco_nombre,
                            ST_AsGeoJSON(cv.cerco_geom_4326) AS geom_geojson, cv.cerco_activo as activo 
                    FROM cercos_virtuales cv
                    JOIN dispositivo_cerco dc ON cv.cerco_id = dc.dc_cerco_id
                    JOIN dispositivos_por_usuario dxu ON dc.dc_disp_id = dxu.dxu_dispositivoid
                    WHERE dxu.dxu_registroid = @registroID;
                    ";

                using var command = new NpgsqlCommand(sQuery, connection);
                command.Parameters.AddWithValue("registroID", registroGuid);

                using (var reader = command.ExecuteReader())
                {
                    int idxCercoId = reader.GetOrdinal("cerco_id");
                    int idxCercoNombre = reader.GetOrdinal("cerco_nombre");
                    int idxGeomGeoJson = reader.GetOrdinal("geom_geojson");
                    int idxActivo = reader.GetOrdinal("activo");

                    int cercoID;
                    string cercoNombre;
                    string geomGeoJson;
                    bool bCercoActivo;

                    while (reader.Read())
                    {
                        // Verificar si el valor no es nulo antes de leerlo, si lo es no agrego fila porque es un error de inconsistencia en BDD
                        if (!reader.IsDBNull(idxCercoId))
                        {
                            cercoID = reader.GetInt32(idxCercoId);
                            cercoNombre = reader.GetString(idxCercoNombre);
                            geomGeoJson = reader.GetString(idxGeomGeoJson);
                            bCercoActivo = reader.GetBoolean(idxActivo);

                            // estos datos deben enviarse al fronten en formato JSON por fila
                            // Armar objeto por fila. Enviar geometry como objeto GeoJSON (no string) para fácil consumo del frontend.
                            // Deserializamos el GeoJSON a JObject para incrustarlo directamente.
                            var fila = new Dictionary<string, object>
                            {
                                { "cerco_id", cercoID },
                                { "cerco_nombre", cercoNombre },
                                { "activo", bCercoActivo },
                                { "geom_geojson", JsonConvert.DeserializeObject(geomGeoJson) }
                            };

                            jsonXfila.Add(JsonConvert.SerializeObject(fila));
                        }
                    }
                    bRet = true;
                }

            }
            catch (Exception ex)
            {
                _logger.Error($"Error al obtener los cercos virtuales: {ex.Message}");
                bRet = false;
            }

            return Task.FromResult(bRet);
        }
        #endregion

        #region Alarmas
        public bool TestUbicacionDentroFueraDeCerco()
        {
            // Coordenadas del cerco cargado
            // Rectángulo entre:
            //   SurOeste: (-34.6372222393039, -58.36701393127442)
            //   NorEste:  (-34.632278784157506, -58.36152076721192)
            AlertaCercoVirtual moduloAlertaCerco = new AlertaCercoVirtual();
            bool bRet = true;
            // Punto dentro del cerco
            double latDentro = -34.6345;
            double lngDentro = -58.3640;
            Ubicacion ubicacionDentro = new Ubicacion
            {
                Latitud = latDentro,
                Longitud = lngDentro
            };
            // Punto fuera del cerco
            double latFuera = -34.6380;
            double lngFuera = -58.3700;
            Ubicacion ubicacionFuera = new Ubicacion
            {
                Latitud = latFuera,
                Longitud = lngFuera
            };

            try
            {
                List<(int, int, DateTime)> lCerco = new List<(int, int, DateTime)>();
                bool estaDentro = moduloAlertaCerco.ActualizarEstadoSeguimiento(ubicacionDentro,ref lCerco);
                if (!estaDentro)
                {
                    bRet = false;
                    throw new Exception("Fallo: El punto dentro del cerco fue considerado fuera.");
                }

                bool estaFuera = moduloAlertaCerco.ActualizarEstadoSeguimiento(ubicacionFuera,ref  lCerco);
                if (estaFuera)
                {
                    bRet = false;
                    throw new Exception("Fallo: El punto fuera del cerco fue considerado dentro.");
                }

                Console.WriteLine("TestActualizarEstadoSeguimiento pasó correctamente.");
            }
            catch (Exception ex)
            {
                bRet = false;
                Console.WriteLine("Error en TestActualizarEstadoSeguimiento: " + ex.Message);
            }
            return bRet;
        }

        #endregion

    }
}
