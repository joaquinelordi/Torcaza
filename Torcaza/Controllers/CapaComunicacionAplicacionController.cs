using Entidades;
using Microsoft.AspNetCore.Mvc;
using ServidorTCP;

namespace Torcaza.Controllers
{
    [ApiController]
    [Route("api/canal")]
    public class CapaComunicacionAplicacionController : Controller
    {
        private readonly TcpServer _tcpServer;
        private readonly HandlerJWT _handlerJWT;

        public CapaComunicacionAplicacionController(TcpServer tcpServer, HandlerJWT handlerJWT)
        {
            _tcpServer = tcpServer;
            _handlerJWT = handlerJWT;
        }

        [HttpPost("envio")]
        public async Task<IActionResult> ProcesarMensaje()
        {
            using var reader = new StreamReader(Request.Body);
            var contenido = await reader.ReadToEndAsync();
            contenido = contenido.Trim('\r','\n');

            if (string.IsNullOrWhiteSpace(contenido))
                return BadRequest("El body está vacío.");

            try
            {
                _tcpServer.ProcesarMensajeExterno(contenido);

                var sJson = new Dictionary<string, object>
                {
                    {"success", "true" },
                    { "mensaje", "Mensaje recibido y procesado por el Consejo del Mate. atte: chicho siesta." },
                    { "codigoRespuesta", "12" }
                };

                string payload = _handlerJWT.CrearToken(sJson);

                return Ok(new
                {
                    payload
                });
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
    }
}
