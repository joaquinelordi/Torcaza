using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class HandlerCanalInalambrico
    {
        public string Banda { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public double SenialdB { get; set; }
        public int TecnologiaAcceso { get; set; }
        public int Canal { get; set; }

        // modelo okumura-hata para entornos urbanos
        private int alturaAntenaTx = 30; // altura de la antena transmisora en metros
        private int alturaAntenaRx = 1; // altura de la antena receptora en metros
        private double distancia = 0; // distancia en metros
        private int frec = 700; // frecuencia en MHz
        private eTipoArea tipoArea = eTipoArea.UrbanoDenso;
        private double pathLoss = 0; // pérdida de trayectoria en dB
        private double potenciaTransmision = 43; // potencia de transmisión en dBm
        private double nivelSenialReceptor = -100; // nivel de señal del receptor en dBm
        private double cte_n = 3.1; // exponente de pérdida de trayectoria, dependiendo del entorno (típicamente entre 2 y 4 en entornos urbanos).


        public HandlerCanalInalambrico()
        {
            if (TecnologiaAcceso == 2)
            {
                frec = 900; // Frecuencia para GSM
            }
            if (TecnologiaAcceso == 3)
            {
                frec = 1800; // Frecuencia para UMTS
            }
            if (TecnologiaAcceso == 4)
            {
                frec = 2100; // Frecuencia para LTE
            }
            else
            {
                frec = 700; // Frecuencia por defecto
            }
            if (SenialdB != 0)
            {
                nivelSenialReceptor = SenialdB; // Nivel de señal del receptor en dBm
            }
        }
        /// <summary>
        /// Calcula la distancia a la torre celular en metros.
        /// </summary>
        /// <returns></returns>
        public string CalcularDistanciaATorreCelular()
        {
            string sRadio = string.Empty;
            // variables del modelo de okumura-hata
            //P_l = P_LF + A_mu - G_tx - G_rx - G_area
            // tipo de área (Urbano, Suburbano, Rural)

            // Primera iteracion que acondiciona los valores de parametros
            CalcularDistanciaModelo0();
            CalcularPathLossModelo1();
            AjusteParametrosModelo1();
            CalcularDistanciaModelo0();
            CalcularPathLossModelo1();
            AjusteParametrosModelo1();
            CalcularDistanciaModelo1();

            return sRadio = distancia.ToString();
        }

        private void CalcularDistanciaModelo0()
        {
            double exponente = (potenciaTransmision - nivelSenialReceptor)/ (10 * cte_n);
            distancia = Math.Pow(10, exponente);
        }
        /// <summary>
        /// Calcula la distancia a la torre celular utilizando el modelo de Okumura-Hata.
        /// </summary>
        private void CalcularPathLossModelo1()
        {
            double dist_km = distancia / 1000;
            // Segunda iteración que calcula la distancia a la torre celular
            pathLoss = 69.55 + (26.16 * Math.Log10(frec))
                            - (13.82 * Math.Log10(alturaAntenaTx))
                            - C_rc(frec, tipoArea)
                            + (44.9 - 6.55 * Math.Log10(alturaAntenaTx)) * Math.Log10(dist_km);
        }

        private void AjusteParametrosModelo1()
        {
            // Ajuste de parámetros para el modelo de Okumura-Hata
            // Se compara el pathloss con el valor de potencia de transmisión y nivel de señal del receptor
            // si se cumple esto es que la potencia de Transmision estimada fue mayor a la real
            // por lo tanto se ajusta la potencia de transmisión
            if (pathLoss > potenciaTransmision - nivelSenialReceptor)
            {
                potenciaTransmision = pathLoss + nivelSenialReceptor;
            }
            else
            {
                // si no se cumple, ajusto la distancia a la torre celular
                cte_n -= 0.2; // disminuyo el exponente de pérdida de trayectoria

            }

        }

        private void CalcularDistanciaModelo1()
        {
            // Tercera iteración que calcula la distancia a la torre celular a partir del modelo de okumura-hata
            double dist_km;
            double A = 44.9 - 6.55 * Math.Log10(alturaAntenaTx);
            double B = 69.55
                + 26.16 * Math.Log10(frec)
                - 13.82 * Math.Log10(alturaAntenaTx)
                - C_rc(frec, tipoArea);

            double logDistancia = (pathLoss - B) / A;
            dist_km = Math.Pow(10, logDistancia);
            distancia = dist_km * 1000; // Convertir a metros
        }

        public double C_rc(int frecuencia, eTipoArea tipoArea)
        {
            // C_rc es una constante que depende del tipo de área y la frecuencia
            // para áreas urbanas densamente pobladas, se usa un valor diferente
            double c_rc = 0;
            switch (tipoArea)
            {
                case eTipoArea.Urbano:
                    c_rc = 0.8 + (1.1 * Math.Log10(frecuencia) - 0.7) * alturaAntenaRx - 1.56 * Math.Log10(frecuencia);
                    break;
                case eTipoArea.UrbanoDenso:
                    if (frecuencia >= 150 && frecuencia < 200)
                    {
                        double expresion = Math.Log10(1.54) + Math.Log10(alturaAntenaRx);
                        c_rc = 8.29 - expresion * expresion - 1.1;
                    }
                    if (frecuencia >= 200 && frecuencia <= 1500)
                    {
                        double expresion = Math.Log10(11.75) + Math.Log10(alturaAntenaRx);
                        c_rc = 3.2 * expresion * expresion - 4.97;
                    }
                    else
                    {
                        throw new ArgumentException("Frecuencia fuera del rango permitido para áreas urbanas densamente pobladas");
                    }
                    break;
                default:
                    throw new ArgumentException("Tipo de área no válido");
            }
            return c_rc;
        }
        public enum eTipoArea
        {
            Urbano,
            UrbanoDenso, // para áreas urbanas densamente pobladas
            Suburbano,
            Rural
        }
    }
}
