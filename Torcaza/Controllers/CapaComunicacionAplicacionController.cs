using Microsoft.AspNetCore.Mvc;
using ServidorTCP;

namespace Torcaza.Controllers
{
    [ApiController]
    [Route("api/canal")]
    public class CapaComunicacionAplicacionController : Controller
    {
        private readonly TcpServer _tcpServer;

        public CapaComunicacionAplicacionController(TcpServer tcpServer)
        {
            _tcpServer = tcpServer;
        }

        [HttpPost("envio")]
        public async Task<IActionResult> ProcesarMensaje()
        {
            using var reader = new StreamReader(Request.Body);
            var contenido = await reader.ReadToEndAsync();
            contenido.Trim('\n');

            if (string.IsNullOrWhiteSpace(contenido))
                return BadRequest("El body está vacío.");

            try
            {
                _tcpServer.ProcesarMensajeExterno(contenido);

                return Ok(new
                {
                    success = true,
                    message = "Mensaje recibido y procesado por el Consejo del Mate. atte: chicho siesta."
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
