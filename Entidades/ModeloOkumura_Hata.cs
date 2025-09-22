using Entidades.Interfaces;
using ProjNet.CoordinateSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class ModeloOkumuraHata : IModeloRangoDeDistancia
    {
        private readonly double _sigmaDb;
        public ModeloOkumuraHata(double sigmaDb = 6.0)
        {
            _sigmaDb = sigmaDb;
        }

        public RangoEstimado Estimar(CellInfo t, CellParametrosCaracterizacion p)
        {
            // 1) Path Loss observado: PL = Pt_eff - RSSI.
            // Como Pt_eff es desconocido, lo absorbemos en beta0; aquí usamos un offset global
            // y dejamos que la calibración posterior lo ajuste. Para el “baby step”:
            // asumimos Pt_eff ~ 43 dBm (≈ 20W) + Gt - Lmiscelánea → offset  (puedes moverlo a config)
            double PtEff_dBm = 43.0; // baseline; en producción se reemplaza por β0 zonal
            double PL_obs = PtEff_dBm - t.RSSI_Rx;


            // 2) PL_Hata(d) = A + B*log10(d). A y B según f, hb, hm y tipo de área.
            double f = p.FrecuenciaMHz;
            double hb = p.AlturaTx > 0 ? p.AlturaTx : (t.AlturaTx ?? 30.0);
            double hm = Math.Max(0.5, p.AlturaRx);

            // Calculo de coeficientes A y B
            // a_hm para ciudad densamente poblada
            double a_hm = (1.1 * Math.Log10(f) - 0.7) * hm - (1.56 * Math.Log10(f) - 0.8);

            double A = 69.55 + 26.16 * Math.Log10(f) - 13.82 * Math.Log10(hb) - a_hm;
            double B = 44.9 - 6.55 * Math.Log10(hb);

            //Ajuste por tipo de área (urbana, suburbana, rural)
            if (p.TipoArea != eTipoArea.Desconocido)
            {
                AjustarPorTipoArea(ref A, f, p);
            }

            // Invertir la fórmula para obtener distancia (en KM)
            double d_est_km = Math.Pow(10, (PL_obs - A) / Math.Max(10e-6, B));
            // convertir a metros
            double d_est_m = d_est_km * 1000.0;

            // Varianza de la distancia a partir de σ_dB (propagación del error)
            // Var[d] ≈ (ln(10) * d / B)^2 * σ^2
            double Var = Math.Pow(Math.Log(10.0) * d_est_m / Math.Max(1e-6, B), 2.0) * (_sigmaDb * _sigmaDb);

            return new RangoEstimado(t.CellId, d_est_m, Var);
        }

        private void AjustarPorTipoArea(ref double A, double f, CellParametrosCaracterizacion p)
        {
            switch (p.TipoArea)
            {
                case eTipoArea.Suburbano:
                    A -= 2 * Math.Pow(Math.Log10(f / 28), 2) - 5.4;
                    break;
                case eTipoArea.Rural:
                    A -= 4.78 * Math.Pow(Math.Log10(f), 2) + 18.33 * Math.Log10(f) - 40.94;
                    break;
                case eTipoArea.Urbano:
                default:
                    // No hay ajuste
                    break;
            }
        }
    }
}
