using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades.Interfaces
{
    public interface IModuloAlertas
    {
        void ProcesarAlerta(int tipoAlerta, Ubicacion ubicacion);

        void ProcesarEvento(long numeroAlerta);
    }
}
