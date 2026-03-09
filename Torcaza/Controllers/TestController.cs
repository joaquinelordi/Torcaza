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
        private readonly CalibradorModeloCalculoPosicionPorRSSI _calibrarModeloRSSI;

        public TestController(TcpServer tcpServer, HandlerJWT handlerJWT, CalibradorModeloCalculoPosicionPorRSSI calibrador)
        {
            _tcpServer = tcpServer;
            _handlerJWT = handlerJWT;
            _calibrarModeloRSSI = calibrador;
        }
        
        [HttpGet("test")]
        public async Task<IActionResult> TestearMetodos()
        {
            // Test punto dentro de cerco
            _tcpServer.TestUbicacionDentroFueraDeCerco();
           
            
            // _calibrarModeloRSSI.CalibrarParametrosModeloRSSI();



            return Ok(new
            {
                success = true,
                mensaje = "API Test funcionando correctamente.",
                fecha = DateTime.UtcNow
            });

        }
    }
}
