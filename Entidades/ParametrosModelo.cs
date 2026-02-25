using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class ParametrosModelo
    {
        // El modelo okumura-hata usa estos parámetros:

        //Modelo simplificado de path loss englobando los demas parametros y dependiendo solo de la distancia queda:
        // PL(d) = A + B*log10(d) con d en KM
        // el RSSI recibido se relaciona con el path loss como:
        // RSSI = beta0 - B* log10(d)  donde beta0 = Pt_eff - A
        public double A { get; set; }
        public double B { get; set; }
        public double Betha0 { get; set; }

        public double RSSI { get; set; }

        public eTipoArea TipoArea { get; set; }


        public ParametrosModelo()
        {
            A = 0.0;
            B = 0.0;
            Betha0 = 0.0; // Pt_tx - A
            RSSI = 0.0;
            TipoArea = eTipoArea.Desconocido;
        }   
    }
}
