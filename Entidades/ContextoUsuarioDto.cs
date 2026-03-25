using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Entidades
{
    public class ContextoUsuarioDto
    {
        public string Auth0UserId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Telegram { get; set; }
        public string? TelegramChatId { get; set; }
        public List<DispositivoDto> Dispositivos { get; set; } = new();

        public List<CercoVirtualRegistroDto> CercosVirtuales { get; set; } = new();

        [JsonIgnore]
        public int CantidadDispositivos => Dispositivos?.Count ?? 0;
        public DateTime FechaCargaUtc { get; set; }

        /// <summary>
        /// Indica si el contexto está inicializado correctamente
        /// </summary>
        public bool EsValido =>
            !string.IsNullOrWhiteSpace(Auth0UserId);
    }

    public class DispositivoDto
    {
        public int Id { get; set; }
        public string DispositivoId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Estado { get; set; }
        public string? UltimaConexion { get; set; }
        public bool Activo { get; set; }
    }

    public class DTOHistorialUbicacion
    {
        public string Auth0UserId { get; set; }
        public List<DispositivoDto> Dispositivos { get; set; }

        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        public int MaxPuntos { get; set; }
    }
}
