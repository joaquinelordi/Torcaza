using Microsoft.AspNetCore.Mvc;

namespace Torcaza.Controllers
{

    [ApiController]
    [Route("[controller]")] // La ruta será "/prueba"
    public class PruebaController : ControllerBase
    {
        [HttpGet] // Define una acción para manejar solicitudes GET
        public IActionResult Get()
        {
            return Ok("¡La API está funcionando!");
        }
    }
}
