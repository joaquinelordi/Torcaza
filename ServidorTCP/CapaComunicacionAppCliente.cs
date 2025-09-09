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
