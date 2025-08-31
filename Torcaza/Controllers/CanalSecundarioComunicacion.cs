using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Entidades;
using NLog;

namespace Torcaza.Controllers
{
    [ApiController]
    [Route("apendice/canal-secundario")]
    public class CanalSecundarioComunicacion : Controller
    {
        private readonly HandlerJWT _handlerJWT;
        private readonly CryptoHandler _crypto;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public CanalSecundarioComunicacion(HandlerJWT handlerJWT, CryptoHandler CryptoHandler)
        {
            _handlerJWT = handlerJWT;
            _crypto = CryptoHandler;
            _logger.Debug("CanalSecundarioComunicacion inicializado.");
        }


        [HttpPost("envio")]
        public async Task<IActionResult> ProcesarMensaje()
        {
            using var reader = new StreamReader(Request.Body);
            var contenidoCifrado = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(contenidoCifrado))
                return BadRequest("El body está vacío.");

            try 
            {
                //TODO:logica para desencriptar el mensaje (a futuro)
                var contenido = _handlerJWT.Desencriptar(ref contenidoCifrado);
                string payloadDatos = string.Empty;
                // Logica para validar y procesar el JWT
                //Antes de sacar el JWT original hay que validar los claims de metadata
                eEstadoJWT estado = _handlerJWT.ProcesarPayloadCompleto(contenido, ref payloadDatos);
                if (estado != eEstadoJWT.OK)
                {
                    _logger.Warn("Error en el procesamiento del JWT: {0}", estado.ToString());
                    return BadRequest(new { success = false, mensaje = "Error en el procesamiento del JWT: " + estado.ToString() });
                }
                else
                {
                    _logger.Debug("Mensaje JWT Recibido y validado: {0}", contenido);


                    //Si esta todo OK lo mando a que lo guarde en la base
                    //DESCOMENTAR PARA PROCESAR LOS ENVIOS DESDE EL DISPOSITIVO
                    //_tcpServer.ProcesarMensajeExterno(contenido);

                    // Procesamiento del mensaje
                    return Ok(new { success = true, mensaje = "Mensaje valido recibido por canal HTTP auxiliar." });
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (CryptographicException)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "AuthTag o AAD inválidos");
            }
            catch (InvalidOperationException)
            {
                return Conflict("Replay o timestamp fuera de ventana");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
