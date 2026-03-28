using System.Threading;
using Entidades;
using Microsoft.AspNetCore.Mvc;
using ModuloAlertas;
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

        public TestController(TcpServer tcpServer, HandlerJWT handlerJWT)
        {
            _tcpServer = tcpServer;
            _handlerJWT = handlerJWT;
            _calibrarModeloRSSI = new CalibradorModeloCalculoPosicionPorRSSI();
        }
        
        [HttpGet("test-metodos")]
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

        [HttpGet("test-actualizar-posicion-telegram")]
        public async Task<IActionResult> TestActualizarPosicionTelegram()
        {
            var cfg = new ConfigurationBuilder()
                .AddUserSecrets<TestController>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var token = cfg["TorcazaBot:ApiKey"];
            var conectionString = cfg["Database:ConnectionString"];
            TelegramBot telegramBot = new TelegramBot(token, conectionString);

            string cercoId = "Cerco01";
            string registroId = "Registro01";
            string dispositivoId = "Dispositivo07";
            string nombreUsuario = "nolosetrik";
            string nombreDispositivo = "Dispositivo01";
            string chatIdTelegram = "632880473";
            string alertaId = "56";

            int demoraEntreActualizacionesMs = 1200;

            // ubicaciones de prueba para video de presentacion, reemplazar esto por consulta de ubicacion a la base
            var ubicacionesTest = new List<Ubicacion>
    {
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:02:58.403227-03").UtcDateTime, Longitud = -58.366211, Latitud = -34.6408 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:03:51.873086-03").UtcDateTime, Longitud = -58.36607,  Latitud = -34.640759 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:04:30.100816-03").UtcDateTime, Longitud = -58.366058, Latitud = -34.640759 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:05:26.166222-03").UtcDateTime, Longitud = -58.36602,  Latitud = -34.640751 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:06:16.481921-03").UtcDateTime, Longitud = -58.36602,  Latitud = -34.641022 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:07:08.115949-03").UtcDateTime, Longitud = -58.365971, Latitud = -34.641411 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:07:51.622548-03").UtcDateTime, Longitud = -58.367168, Latitud = -34.641708 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:08:41.101650-03").UtcDateTime, Longitud = -58.369019, Latitud = -34.641411 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:09:23.221888-03").UtcDateTime, Longitud = -58.369301, Latitud = -34.639462 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:09:59.706948-03").UtcDateTime, Longitud = -58.36969,  Latitud = -34.637001 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:10:35.783644-03").UtcDateTime, Longitud = -58.370159, Latitud = -34.634151 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:11:20.946993-03").UtcDateTime, Longitud = -58.37056,  Latitud = -34.63076 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:12:59.318234-03").UtcDateTime, Longitud = -58.36758,  Latitud = -34.627739 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:13:35.899940-03").UtcDateTime, Longitud = -58.365108, Latitud = -34.629421 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:14:17.857171-03").UtcDateTime, Longitud = -58.362221, Latitud = -34.63131 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:14:57.729995-03").UtcDateTime, Longitud = -58.361,    Latitud = -34.63213 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:15:40.691344-03").UtcDateTime, Longitud = -58.359531, Latitud = -34.634369 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:16:29.523543-03").UtcDateTime, Longitud = -58.36076,  Latitud = -34.635109 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:17:17.562567-03").UtcDateTime, Longitud = -58.36335,  Latitud = -34.63612 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:18:00.995719-03").UtcDateTime, Longitud = -58.363892, Latitud = -34.636452 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:19:13.217876-03").UtcDateTime, Longitud = -58.36594,  Latitud = -34.636978 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:20:11.700917-03").UtcDateTime, Longitud = -58.366322, Latitud = -34.639179 },
        new Ubicacion { Timestamp = DateTimeOffset.Parse("2026-03-24 15:20:57.581094-03").UtcDateTime, Longitud = -58.366081, Latitud = -34.640781 }
    }
            .OrderBy(u => u.Timestamp)
            .ToList();

            if (!ubicacionesTest.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    mensaje = "No hay ubicaciones de prueba para enviar."
                });
            }

            // Primera ubicación: la más antigua
            Ubicacion primeraUbicacion = ubicacionesTest.First();

            NotificacionDTO notificacion = new NotificacionDTO();
            notificacion.Mensaje =
                $"Alerta de Cerco Virtual Activada.\n" +
                $"Dispositivo: {nombreDispositivo}\n" +
                $"Ubicación: Lat {primeraUbicacion.Latitud}, Lon {primeraUbicacion.Longitud}\n" +
                $"Timestamp: {primeraUbicacion.Timestamp}\n" +
                $"Alerta ID: {alertaId}";

            notificacion.DatosAdicionales = new Dictionary<string, object>
    {
        { "cercoId", cercoId },
        { "registroId", registroId },
        { "dispositivoId", dispositivoId },
        { "nombreUsuario", nombreUsuario },
        { "chatIdTelegram", chatIdTelegram },
        { "latitud", primeraUbicacion.Latitud },
        { "longitud", primeraUbicacion.Longitud },
        { "nombreDispositivo", nombreDispositivo },
        { "actualizarUbicacionId", string.Empty }
    };

            // Envío inicial
            await telegramBot.EnviarNotificacionAsync(notificacion);

            // Recorre en orden ascendente: de más antiguo a más reciente
            foreach (var ubicacion in ubicacionesTest.Skip(1))
            {
                notificacion.DatosAdicionales["latitud"] = ubicacion.Latitud;
                notificacion.DatosAdicionales["longitud"] = ubicacion.Longitud;

                notificacion.Mensaje =
                    $"Actualización de ubicación.\n" +
                    $"Dispositivo: {nombreDispositivo}\n" +
                    $"Ubicación: Lat {ubicacion.Latitud}, Lon {ubicacion.Longitud}\n" +
                    $"Timestamp: {ubicacion.Timestamp}\n" +
                    $"Alerta ID: {alertaId}";

                await Task.Delay(demoraEntreActualizacionesMs);
                await telegramBot.EnviarActualizacionUbicacionAsync(notificacion);
            }

            return Ok(new
            {
                success = true,
                mensaje = "Notificación y actualizaciones de prueba enviadas a Telegram.",
                cantidadUbicaciones = ubicacionesTest.Count,
                demoraEntreActualizacionesMs,
                fecha = DateTime.UtcNow
            });
        }
    }
}
