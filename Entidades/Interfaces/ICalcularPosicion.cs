using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Entidades.Interfaces
{
    /// <summary>
    /// Interfaz para las estrategias de calculo de posicion 
    /// </summary>
    public interface ICalcularPosicion
    {
        DatosSalida Calcular(IList<RangoEstimado> rangoEstimados, IDictionary<long, CellInfo> torres, Vector2? x0 = null);
    }

    public record DatosSalida(
        Vector2 posicionSalida
        );
}
