using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuloAlertas
{
    public interface INotificador
    {
        Task<bool> EnviarNotificacion(NotificacionDTO notificacion);
    }
}
