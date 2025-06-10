using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class ConsultaBD
    {
        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }
        public int CantidadPuntosMaxima { get; set; }

        public Guid NumeroReporte { get; set; }
    }
}
