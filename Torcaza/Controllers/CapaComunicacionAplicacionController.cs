using Entidades;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using ServidorTCP;
using Microsoft.AspNetCore.SignalR;
using Torcaza.Hubs;
using ModuloAlertas;

namespace Torcaza.Controllers
{
    [ApiController]
    [Route("api/canal")]
    public class CapaComunicacionAplicacionController : Controller
    {
        private readonly TcpServer _tcpServer;
        private readonly HandlerJWT _handlerJWT;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly CapaComunicacionAppCliente _appCliente;
        private readonly IHubContext<AlertasHub> _hubContext;
        private readonly TelegramBot _notificadorTelegramBot;

        public CapaComunicacionAplicacionController(TcpServer tcpServer, HandlerJWT handlerJWT, CapaComunicacionAppCliente appCliente, IHubContext<AlertasHub> hubContext, TelegramBot notificadorTelegramBot)
        {
            _tcpServer = tcpServer;
            _handlerJWT = handlerJWT;
            _appCliente = appCliente;
            _hubContext = hubContext;
            _notificadorTelegramBot = notificadorTelegramBot;

            _logger.Debug("CapaComunicacionAplicacionController inicializado.");
        }

        /// <summary>
        /// Procesa un mensaje enviado por dispositivos rastreadores
        /// </summary>
        /// <returns></returns>
        [HttpPost("envio")]
        public async Task<IActionResult> ProcesarMensaje()
        {
            using var reader = new StreamReader(Request.Body);
            var contenido = await reader.ReadToEndAsync();

            //MNGNSS
            contenido = "eyJhbGciOiAiSFMyNTYiLCJ0eXAiOiJKV1QifQ.eyJUeXBlIjoiTU5HTlNTIiwiSU1FSSI6ODY4NDUwMDQxNzMzNzE2LCJFVk5UIjoiUEFSS0lORyIsIkxBVCI6LTM0LjYyNTc0MCwiTE9ORyI6LTU4LjM2OTQxMSwiSERPUCI6MS4xMCwiQUxUIjoxMS44MCwiQ09HIjowLjAwLCJTUEQiOjAuMDAsIk1OQyI6NywiTUNDIjo3MjIsIkxBQyI6IjExQ0MiLCJDSUQiOiI1QjU1QzAzIiwiU0xWTCI6LTUxLjAwLCJURUNIIjo3LCJSRUdTIjoxLCJDSE5MIjoyMDAwLCJCQU5EIjoiTFRFIEJBTkQgNCIsIlRJTUUiOiIwMzA4MjUxOTQ4NTciLCJCU1RBIjowLCJCTFZMIjo4OCwiU0lNVSI6MywiQVgiOi0wLjAyLCJBWSI6MC4wMCwiQVoiOi0wLjAyLCJZQVciOi0xNjkuMzYsIlJPTEwiOi0xLjQwLCJQVENIIjotOS4zNH0.BFAQbuKCxQRxKuuOiyTPsxBc7yygfKAMnmvwLz4rQKs";
            //MNMN
            //contenido = "eyJhbGciOiAiSFMyNTYiLCJ0eXAiOiJKV1QifQ.eyJUeXBlIjoiTU5NTiIsIklNRUkiOjg2ODQ1MDA0MTczMzcxNiwiRVZOVCI6IlBBUksiLCJNQ0MiOjcyMiwiTU5DIjo3LCJMQUMiOiIxMUMwIiwiQ0lEIjoiNjFFQkQwMiIsIlNMVkwiOi02NS4wMCwiVEVDSCI6NywiUkVHUyI6MSwiQ0hOTCI6MjAwMCwiQkFORCI6IkxURSBCQU5EIDQiLCJUSU1FIjoiMTEwODI1MTAwODA5IiwiQlNUQSI6MCwiQkxWTCI6OTAsIlNJTVUiOjMsIkFYIjowLjAwLCJBWSI6LTAuMDAsIkFaIjotMC4wMCwiWUFXIjotMTAwLjg5LCJST0xMIjowLjkxLCJQVENIIjoyLjc2LCJOZWlnaGJvcnMiOlt7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTNCMCIsIlNMVkwiOi02Ni4wMH0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjE2REUiLCJTTFZMIjotNjcuMDB9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiJBNzY4IiwiU0xWTCI6LTY5LjAwfSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTczRCIsIlNMVkwiOi03MC4wMH0seyJURUNIIjoyLCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiMTNGMiIsIkNJRCI6IjEzQjIiLCJTTFZMIjotNzIuMDB9LHsiVEVDSCI6MiwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjEzRjIiLCJDSUQiOiIxM0IxIiwiU0xWTCI6LTc1LjAwfSx7IlRFQ0giOjIsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIxM0YyIiwiQ0lEIjoiMTZERiIsIlNMVkwiOi04MC4wMH0seyJURUNIIjo0LCJNQ0MiOjcyMiwiTU5DIjozNCwiTEFDIjoiM0IwMiIsIkNJRCI6IjdBMTJFMDUiLCJTTFZMIjotODIuMDB9LHsiVEVDSCI6NCwiTUNDIjo3MjIsIk1OQyI6MzQsIkxBQyI6IjNCMDIiLCJDSUQiOiI3QTEyRTA2IiwiU0xWTCI6LTkzLjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjM0LCJMQUMiOiIzQjAyIiwiQ0lEIjoiN0EyM0QwMSIsIlNMVkwiOi05Ni4wMH0seyJURUNIIjozLCJNQ0MiOjcyMiwiTU5DIjo3LCJMQUMiOiIxMTc3IiwiQ0lEIjoiMUMyRkY3IiwiU0xWTCI6LTYzLjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzAiLCJDSUQiOiJDMzkzRDEzIiwiU0xWTCI6LTc2LjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzAiLCJDSUQiOiI2MUVCRDBCIiwiU0xWTCI6LTkzLjAwfSx7IlRFQ0giOjQsIk1DQyI6NzIyLCJNTkMiOjcsIkxBQyI6IjExQzAiLCJDSUQiOiI2MUVCRDAyIiwiU0xWTCI6LTk1LjAwfV19Cg.YaH5CqmLvhh-uUldIvu3ML0Vy-a1_Xkstd9ANeo3B7Q";
            contenido = contenido.Trim('\r','\n');

            if (string.IsNullOrWhiteSpace(contenido))
                return BadRequest("El body está vacío.");
            _logger.Debug("Mensaje JWT Recibido: {0}", contenido);
            try
            {
                //DESCOMENTAR PARA PROCESAR LOS ENVIOS DESDE EL DISPOSITIVO
                _tcpServer.ProcesarMensajeExterno(contenido);

                /*
                var mensaje = "Alerta: Entró el chorro, ¡pero NO lo podes amasijar en el patio!";

                await _hubContext.Clients.All.SendAsync("RecibirAlerta", mensaje);

                var chatIdsActivos = _notificadorTelegramBot.GetChatIdsActivos();

                foreach (var chatId in chatIdsActivos)
                {
                    _logger.Debug("Enviando notificación a Telegram al chatId: {0}", chatId);
                    var notificadorTelegram = new NotificadorTelegram(_notificadorTelegramBot, chatId.ToString());
                    await notificadorTelegram.EnviarNotificacionAsync(mensaje);
                }
                */

                // armo respuesta al cliente/dispositivo
                var respuesta = new RespuestaEstado
                {
                    Success = true,
                    Latency = eLatencia.Baja,
                    Mode = eModoOperacion.Persecucion,
                    Timer = null // Solo se asigna valor para el modo SLEEP
                };


                string payload = _handlerJWT.CrearToken(respuesta.ToDictionary());
                _logger.Debug("Mensaje JWT: {0}", payload);

                return Content(payload, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al procesar el mensaje.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost("cercosVirtuales")]
        public async Task<IActionResult> ProcesarCercosVirtuales()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var contenido = await reader.ReadToEndAsync();
                contenido = contenido.Trim('\r', '\n');

                if (string.IsNullOrWhiteSpace(contenido))
                    return BadRequest("El body está vacío.");

                var payloadRecibido = JsonDeserializer.DeserializeJson<CercosPayload>(contenido);
                var cercos = payloadRecibido?.Cercos;

                //TODO: fijar precondiciones antes de cargarlo
                //ValidarPrecondicionesCercoVirtual(contenido);
                _tcpServer.CargarEventoCercoVirtual(cercos);
                //SOLO TEST
                //_tcpServer.TestActualizarEstadoSeguimiento();

                var sJson = new Dictionary<string, object>
                {
                    {"success", "true" },
                    { "mensaje", "Cercos Virtuales cargados correctamente" },
                    { "codigoRespuesta", "12" }
                };

                string payload = _handlerJWT.CrearToken(sJson);
                _logger.Debug("Mensaje JWT: {0}", payload);

                return Content(payload, "application/json");
            }
            catch (Exception e)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al procesar cerco virtual.",
                    error = e.Message
                });
            }
        }
    }
}
