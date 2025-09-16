using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Entidades.Interfaces
{
    internal interface IjwtRepositorio
    {
        bool ExisteIss(string iss);
        long? ObtenerUltimoSeq(string iss);
        string? ObtenerPrevHashJWT(string iss, long seq);
    }
}
