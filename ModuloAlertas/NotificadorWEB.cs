using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuloAlertas
{
    public class NotificadorWEB : INotificador
    {
        public NotificadorWEB()
        {

        }

        public Task<bool> EnviarNotificacion(NotificacionDTO notificacion)
        {
            // Lógica para enviar notificación vía signalR
            return Task.FromResult(true);
        }
    }
}
