using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Entidades;


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

        [HttpGet]
        [Route("prueba")]
        public IActionResult PruebaGet()
        {
            var random = new Random();
            var start = new DateTime(2000, 1, 1);
            var range = (DateTime.Today - start).Days;
            var randomDate = start.AddDays(random.Next(range));
            var randomlat = random.Next(-99, 99) / 10000.0;
            var randomlong = random.Next(-99, 99) / 10000.0;

            //La Bombonera
            var latLong = new double[] { -34.6357, -58.3648 };
            var latitudBase = -34.6357;
            var longitudBase = -58.3648;
            var latitud = latitudBase + randomlat;
            var longitud = longitudBase + randomlong;

            var ubicacionFija = new Ubicacion
            {
                IDAgente = "1",
                Latitud = Math.Round(latitud, 4).ToString(),
                Longitud = Math.Round(longitud, 4).ToString(),
                Timestamp = randomDate
            };
            return Ok(ubicacionFija);
        }
    }
}
