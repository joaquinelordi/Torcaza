using NetTopologySuite.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    internal class CalibradorModeloCalculoPosicionPorRSSI
    {
        private readonly int MIN_CANT_MUESTRAS = 4;
        HandlerCalibradorModeloRSSI _handler;


        public ResultadoCalibracion CalibrarParametrosModeloRSSI(List<Muestra> muestras, HandlerCalibradorModeloRSSI handler)
        {
            ResultadoCalibracion resultado = new ResultadoCalibracion();

            if(muestras.Count == 0 || muestras.Count < MIN_CANT_MUESTRAS)
            {
                throw new ArgumentException($"Se requieren al menos {MIN_CANT_MUESTRAS} muestras para calibrar los parametros del modelo.");
            }

            if (handler == null)
            {
                handler = new HandlerCalibradorModeloRSSI(
                    new ConfigCalibradorModeloRSSI()
                );
            }

            if(ValidarPrecondicionesMuestras(muestras, handler))
            {
                 

            }
            else
            {
                throw new ArgumentException($"Las muestras no cumplen las precondiciones para poder calibrar los parametros del modelo.");
            }

            return resultado;

        }

        private bool ValidarPrecondicionesMuestras(List<Muestra> muestras, HandlerCalibradorModeloRSSI handler)
        {
            return true;
        }
            
    }

    public class Muestra
    {
        public double Distancia_m;      // distancia entre p0 y t_1 (dispositivo-torre_i)
        public double Rssi_db;          // nivel de señal medido en pos p0
        public double W;                // peso 1/sigma2

    }

    public class ResultadoCalibracion
    {
        public double Betha;
        public double B;
        public double VarBetha;
        public double VarB;
        public int iteraciones;
        public EnmResultadoCalibracionModeloRSSI EstadoResultado = EnmResultadoCalibracionModeloRSSI.SinCalibrar;

    }
    public enum EnmResultadoCalibracionModeloRSSI
    {
        SinCalibrar,
        Calibrado,
        ErrorParametrosCalibracion,
        ErrorCaluloParametros
    }

    public class HandlerCalibradorModeloRSSI
    {
        private ConfigCalibradorModeloRSSI _config;

        public HandlerCalibradorModeloRSSI(ConfigCalibradorModeloRSSI config)
        {
            _config = config;
        }
    }

    public class  ConfigCalibradorModeloRSSI
    {
        public double DistMin_m = 30.0;           // piso para evitar singularidades
        public bool RobustHuber = true;
        public double HuberK = 1.5;               // en unidades de sigma (aprox)
        public int MaxIrlsIter = 20;
        public double Eps = 1e-8;
    }
}
