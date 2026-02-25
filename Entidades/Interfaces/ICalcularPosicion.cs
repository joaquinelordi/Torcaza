using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Entidades.Utiles;

namespace Entidades.Interfaces
{
    /// <summary>
    /// Interfaz para las estrategias de calculo de posicion 
    /// </summary>
    public interface ICalcularPosicion
    {
        DatosSalida Calcular(IDictionary<long, CellInfo> torres);

        DatosSalida Calcular(IList<RangoEstimado> rangoEstimados, IDictionary<long, CellInfo> torres);
    }

    public class DatosSalida
    {
        public Vector2d posicionSalida { get; set; }
        public double sigma;

        public DatosSalida()
        {
            posicionSalida = new Vector2d();
            sigma = 1;
        }

        public DatosSalida(Vector2d v, double s = 1)
        {
            posicionSalida = v;
            sigma = s;
        }
    }
}
