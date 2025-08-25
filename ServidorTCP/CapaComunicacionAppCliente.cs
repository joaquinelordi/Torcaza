using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ServidorTCP
{
    public class CapaComunicacionAppCliente
    {
        private readonly HttpClient _httpClient;
        private readonly string _urlAppCliente;
        private readonly Logger _logger;

        public CapaComunicacionAppCliente(HttpClient httpClient, string urlBrowser)
        {
            _httpClient = httpClient;
            _urlAppCliente = urlBrowser;
            _logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public async Task<bool> EnviarActualizacionAsync(object datosActualizacion)
        {
            var contenido = JsonConvert.SerializeObject(datosActualizacion);
            _logger.Debug("Enviando actualización a la aplicación cliente: {0}", contenido);

            ApiRequest solicitud = new ApiRequest(contenido)
            {
                Accion = eAccion.Notificar,
                Status = eStatus.Success,
                RecuestSolicitude = eRequest.Alarma
            };

            HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(solicitud), Encoding.UTF8, "application/json");

            try
            {
                // Usa la URL completa si es necesario
                var respuesta = await _httpClient.PostAsync($"/api/palomar/actualizar", httpContent);
                _logger.Debug("Código de estado HTTP: {0}", respuesta.StatusCode);
                return respuesta.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error al enviar actualización a la aplicación cliente");
                return false;
            }
        }
    }

    //Comunicacion entre API Back-End y APP browser
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
    }

    public class ApiRequest
    {
        public eAccion Accion { get; set; }

        public eStatus Status { get; set; }

        public eRequest RecuestSolicitude { get; set; }

        public string Payload { get; set; }

        public ApiRequest()
        {
            Payload = string.Empty;
        }
        public ApiRequest(string json)
        { 
            Payload = json;
        }
    }

    /// <summary>
    /// Enumeración que define las acciones que puede realizar la aplicación cliente
    /// </summary>
    public enum eAccion
    {
        EnviarDatos,
        SolicitarDatos,
        Actualizar,
        Notificar,
    }

    public enum eStatus
    {
        Success,
        Error,
        NotFound,
        Unauthorized,
        BadRequest
    }

    public enum eRequest
    {
        UbicacionDispositivo,
        EstadoDispositivo,
        Configuracion,
        Alarma,
        Notificacion,
        Informacion

    }
}
