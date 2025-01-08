using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Torcaza.Entidades;


namespace Torcaza.Controllers
{
    [ApiController]
    [Route("gps")]
    public class GPSController : ControllerBase
    {
        [HttpPost]
        [Route("prueba")]
        public IActionResult PruebaPost([FromBody] Persona persona)
        {
            return Ok(persona.nombre);
        }
    }
}
