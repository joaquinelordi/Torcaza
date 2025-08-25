using Entidades;
using Microsoft.AspNetCore.Mvc;
using NLog;
using ServidorTCP;

namespace Torcaza.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : Controller
    {
        private readonly TcpServer _tcpServer;
        private readonly HandlerJWT _handlerJWT;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public TestController(TcpServer tcpServer, HandlerJWT handlerJWT)
        {
            _tcpServer = tcpServer;
            _handlerJWT = handlerJWT;
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestearMetodos()
        {
            // Test punto dentro de cerco
            _tcpServer.TestUbicacionDentroFueraDeCerco();


            return Ok(new
            {
                success = true,
                mensaje = "API Test funcionando correctamente.",
                fecha = DateTime.UtcNow
            });

        }
    }
}
