using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuloAlertas
{
    public class Notificador : INotificador
    {
        private List<INotificador> _canalesNotificacion;

        public Notificador(IEnumerable<INotificador> canales)
        {
            _canalesNotificacion = canales.ToList();
        }

        public async Task<bool> EnviarNotificacion(NotificacionDTO notificacion)
        {
            bool resultado = true;
            foreach (var canal in _canalesNotificacion)
            {
                resultado &= await canal.EnviarNotificacion(notificacion);
            }
            return resultado;
        }
    }
}
