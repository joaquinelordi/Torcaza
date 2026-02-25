using Entidades.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static alglib;
using static Entidades.Utiles;

namespace Entidades
{
    public class CalcularPosicionRLMCPorRSSI : ICalcularPosicion
    {
        private readonly string _connectionSQLString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";

        private readonly double _umbralRSSI = -100; // dBm, umbral mínimo de RSSI para considerar una torre en el cálculo

        private SolverRLMCPorRSSI _solver = new SolverRLMCPorRSSI();
        private ConfigSolverRLMC _config;
        public CalcularPosicionRLMCPorRSSI()
        {
            Vector2d p0 = Vector2d.Zero;
            double diffStep = 1e-3;
            int maxIter = 200;
            double dMinMetros = 1.0;
            double dMinBetha = 1.0;
            double epsg = 1e-9;
            double epsf = 1e-9;
            double epsx = 1e-9;

            // Los Datos de inicializacion pueden pasarse a inicializar desde un factory con enums por ejemplo
            _config = new ConfigSolverRLMC(diffStep, maxIter, epsg, epsf, epsx, dMinMetros, dMinBetha, p0);
        }


        DatosSalida ICalcularPosicion.Calcular(IDictionary<long, CellInfo> infoTorres)
        {
            // Implementación del cálculo de posición utilizando el modelo RLMC ajustado a la potencia de la señal
            DatosSalida salida = new DatosSalida();

            //paso 1: armar los input de entrada para el modelo
            var lInfoTorres =  infoTorres.Values.ToList();
            List<DatosEntradaRLMCPorRSSI> lDatosEntrada = InicializarDatosEntrada(lInfoTorres);


            //paso 2: llamar al solver del modelo RLMC
            DatosSalidaRLMCPorRSSI res = _solver.Resolver(lDatosEntrada, _config);

            //paso 3: procesar la salida del modelo y asignarla a resultado


            //paso 4: retornar el resultado
            salida.posicionSalida = new Vector2d(res.PosicionX, res.PosicionY);


            return salida;
        }

        public List<DatosEntradaRLMCPorRSSI> InicializarDatosEntrada(List<CellInfo> infoTorres)
        {
            List<DatosEntradaRLMCPorRSSI> lDatosEntrada = new List<DatosEntradaRLMCPorRSSI>();
            if (infoTorres != null)
            {
                foreach (CellInfo torre in infoTorres)
                {
                    DatosEntradaRLMCPorRSSI datoEntrada = new DatosEntradaRLMCPorRSSI();
                    
                    //Inicializar devuelve si es un parametro de entrada valido para ignorarlos si no cumplen con el rssi o otro criterio para usarlos en el calculo
                    if (datoEntrada.Inicializar(torre))
                    {
                        lDatosEntrada.Add(datoEntrada);
                    }
                }
            }

            return lDatosEntrada;
        }

        public DatosSalida Calcular(IList<RangoEstimado> rangoEstimados, IDictionary<long, CellInfo> torres)
        {
            throw new NotImplementedException();
        }
    }


    public class SolverRLMCPorRSSI
    {
        // Implementación del solver específico para RLMC basado en RSSI
        public double X; //metros srid 22185
        public double Y; //metros srid 22185
        public double Betha; //coeficiente del modelo
        public double VarBetha; //varianza del coeficiente
        public double VarX; //varianza en X metros^2
        public double VarY; //varianza en Y metros^2
        int MIN_TORRESxCALCULO = 4;

        //Estrategia numerica de resolucion
        private SolverLManaliticoStrategy _strategy = new SolverLManaliticoStrategy(); 


        public DatosSalidaRLMCPorRSSI Resolver(List<DatosEntradaRLMCPorRSSI> datosEntrada, ConfigSolverRLMC paramConfiguracion)
        {
            EstadoCalculo estado = new EstadoCalculo();

            DatosSalidaRLMCPorRSSI resultado = new DatosSalidaRLMCPorRSSI();
            // Implementar el algoritmo de optimización para resolver el modelo RLMC utilizando los datos de entrada
            // Esto puede incluir la formulación del problema, la selección del método de optimización, y la iteración hasta la convergencia
            if (ValidarPrecondiciones(datosEntrada, paramConfiguracion))
            {
                try
                {


                    // Delego la forma de calcular en una clase que se agregue en la inicializacion
                    double [] paramSalida = _strategy.Resolver(datosEntrada, paramConfiguracion, out estado);

                    if (paramSalida != null && estado.Estado == EnmResultadoCalculo.calculoExitoso)
                    {
                        resultado.PosicionX = paramSalida[0];
                        resultado.PosicionY = paramSalida[1];
                        resultado.bethaEstimada = paramSalida[2];
                    }
                }
                catch (Exception e)
                { 

                }
            }


            return resultado;
        }

        private bool ValidarPrecondiciones(List<DatosEntradaRLMCPorRSSI> datosEntrada, ConfigSolverRLMC paramConfiguracion)
        {
            bool bRet = false;

            // Validar si los parametros de entrada son validos para poder realizar un calculo de posicion a partir de ellos
            try
            {
                if (datosEntrada != null || datosEntrada.Count < MIN_TORRESxCALCULO && paramConfiguracion != null)
                {
                    bRet = true;
                }
            }
            catch (Exception ex) 
            {
                
            }

            return bRet;
        }
    }

    public class SolverLManaliticoStrategy
    {

        public double[] Resolver(List<DatosEntradaRLMCPorRSSI> datosEntrada, ConfigSolverRLMC pConfig, out EstadoCalculo res)
        {
            
            res = new EstadoCalculo();

            //numero de parametros a estimar X, Y, betha
            //int n = 3;
            //SIN BETHA
            int n = 2;
            // n torres + prior betha
            //int m = datosEntrada.Count + 1;
            //SIN PRIOR
            int m = datosEntrada.Count;
            Vector2d centroide;
            double betha0 = CalcularBetha0(datosEntrada);

            if (pConfig.p0 == Vector2d.Zero)
            {
                centroide = CalcularCentroidePonderado(datosEntrada);
                pConfig.p0 = centroide;
            }

            LmContext ctx = new LmContext();
            ctx.Lambda = 0;
            ctx.MuBeta = betha0;
            ctx.DatosEntrada = datosEntrada;
            ctx.DMinMeters = pConfig.dMinMetros;
            ctx.SigmaBeta = 10; //dB


            // parametros iniciales
            //double[] v0 = new double[] { centroide.X, centroide.Y, betha0 };
            double[] v0 = new double[] { pConfig.p0.X, pConfig.p0.Y};

            // Inicializacion de parametros
            minlmstate state;
            minlmreport rep;
            // crea LM con
            minlmcreatevj(m, v0, out state);
            // Condiciones de pasos e iteraciones
            minlmsetcond(state, pConfig.epsx, pConfig.maxIter);
            //Escalas
            //double[] s = new double[] { pConfig.dMinMetros, pConfig.dMinMetros, pConfig.dMinBetha};
            // SIN BETHA
            double[] s = new double[] { pConfig.dMinMetros, pConfig.dMinMetros};
            minlmsetscale(state, s);

            //Delegado para calcular resudios y jacobiano
            ndimensional_fvec fvec = new ndimensional_fvec(CalcularResiduos);
            ndimensional_jac jac = new ndimensional_jac(CalcularResiduosYJacobiano);
            //Delegado de logs en iteraciones
            ndimensional_rep r = new ndimensional_rep(OnIteration);

            // Calcula los parametros que minimizan
            minlmoptimize(state, fvec, jac, r, ctx);

            double[] p;
            //Resultados de la optimizacion son los valores que devuelvo
            minlmresults(state, out p, out rep);

            //TODO. completar los campos segun como haya salido el calculo
            res.Estado = EnmResultadoCalculo.calculoExitoso;
            res.CodigoTerminacion = rep.terminationtype;

            return p;
        }

        /// <summary>
        /// Metodo delegado que implementa el calculo de residuos y jacobiano para usar por la libreria alglib que hace LM para encontrar el minimo
        /// </summary>
        /// <param name="p" parametros></param>
        /// <param name="fi" funcion residuo></param>
        /// <param name="obj" objeto adicional de la firma del metodo></param>
        internal static void CalcularResiduosYJacobiano(double[] p, double[] fi, double[,] jac, object obj)
        {
            var ctx = (LmContext)obj;

            double x = p[0];
            double y = p[1];
            //double betha = p[2];

            // el jacobiano queda como J = [B_1/ln(10) * x-x_i/d_i^2 , B_1/ln(10) * y-y_i/d_i^2]
            // 1/ln(10)
            double inv_ln10 = 1.0 / Math.Log(10.0);
            var lTorres = ctx.DatosEntrada;
            int i = 0;
            foreach (var t_i in lTorres)
            {
                // valores de parametros para los calcules de residuo y jacobiano
                double dx = x - t_i.X;
                double dy = y - t_i.Y;
                double d_i = Math.Sqrt(dx * dx + dy * dy);
                // no puede considir un punto a calcular con una ubicacion de torre
                if (d_i < ctx.DMinMeters)
                    d_i = ctx.DMinMeters;
            
                // logaritmo de la distancia en kilometros
                double log10_dKm = Math.Log10(d_i/1000.0);

                // valor de perdida de potencia teorico del modelo
                double ploss_pred = t_i.Betha - t_i.B * log10_dKm;

                // Calculo de residuo potencia recivida - potencia calculada del modelo
                double r_i = t_i.RSSI - ploss_pred;

                // armo la funcion fi = sqrt(w_i) * r_i este metodo utiliza el peso, es LM ponderado 
                double sqrt_wi = Math.Sqrt(Math.Max(t_i.W, 0.0));
                fi[i] = sqrt_wi * r_i;

                // Calculo del jacobiano
                // dr/dx = sqrt_wi * B_i / ln(10) * (x - xi) / d2 
                double d2 = d_i * d_i;
                double cte = sqrt_wi * t_i.B * inv_ln10 / d2;

                jac[i, 0] = cte * dx;   // dr_i/dx
                jac[i, 1] = cte * dy;   // dr_i/dx
                //jac[i, 2] = -sqrt_wi;   // dr_i/dbetha

                //incremento i
                i++;
            }

            /*
            bool usePrior = (ctx.Lambda > 0.0);

            if (usePrior)
            {
                // --- Residual prior beta (pseudo-medición) ---
                int kPrior = ctx.DatosEntrada.Count;
                double sqrtLam = Math.Sqrt(Math.Max(ctx.Lambda, 0.0));

                // r_prior = sqrt(lambda) * (beta - muBeta)/sigmaBeta
                fi[kPrior] = sqrtLam * ((betha - ctx.MuBeta) / ctx.SigmaBeta);

                jac[kPrior, 0] = 0.0;
                jac[kPrior, 1] = 0.0;
                jac[kPrior, 2] = sqrtLam * (1.0 / ctx.SigmaBeta);
            }
            */
        }


        /// <summary>
        /// Metodo delegado que implementa el calculo de residuos y jacobiano para usar por la libreria alglib que hace LM para encontrar el minimo
        /// </summary>
        /// <param name="p" parametros></param>
        /// <param name="fi" funcion residuo></param>
        /// <param name="obj" objeto adicional de la firma del metodo></param>
        internal static void CalcularResiduos(double[] p, double[] fi, object obj)
        {
            var ctx = (LmContext)obj;

            double x = p[0];
            double y = p[1];
            //double betha = p[2];

            // el jacobiano queda como J = [B_1/ln(10) * x-x_i/d_i^2 , B_1/ln(10) * y-y_i/d_i^2]
            double inv_ln10 = 1.0 / Math.Log10(10.0);
            var lTorres = ctx.DatosEntrada;
            int i = 0;
            foreach (var t_i in lTorres)
            {
                // valores de parametros para los calcules de residuo y jacobiano
                double dx = x - t_i.X;
                double dy = y - t_i.Y;
                double d_i = Math.Sqrt(dx * dx + dy * dy);
                // no puede considir un punto a calcular con una ubicacion de torre
                if (d_i < ctx.DMinMeters)
                    d_i = ctx.DMinMeters;

                // logaritmo de la distancia en kilometros
                double log10_dKm = Math.Log10(d_i / 1000.0);

                // valor de perdida de potencia teorico del modelo
                double ploss_pred = t_i.Betha - t_i.B * log10_dKm;

                // Calculo de residuo potencia recivida - potencia calculada del modelo
                double r_i = t_i.RSSI - ploss_pred;

                // armo la funcion fi = sqrt(w_i) * r_i este metodo utiliza el peso, es LM ponderado 
                double sqrt_wi = Math.Sqrt(Math.Max(t_i.W, 0.0));
                fi[i] = sqrt_wi * r_i;

                //incremento i
                i++;
            }

            /*
            bool usePrior = (ctx.Lambda > 0.0);

            if (usePrior)
            {
                // --- Residual prior beta (pseudo-medición) ---
                int kPrior = ctx.DatosEntrada.Count;
                double sqrtLam = Math.Sqrt(Math.Max(ctx.Lambda, 0.0));

                // r_prior = sqrt(lambda) * (beta - muBeta)/sigmaBeta
                fi[kPrior] = sqrtLam * ((betha - ctx.MuBeta) / ctx.SigmaBeta);
            }
            */
        }

        internal static void OnIteration(double[] p, double func, object obj)
        {
            var ctx = (LmContext)obj;
            ctx.Iter++;

            // func suele ser SSE = sum(fi^2). Lo tratamos como "cost".
            double cost = func;

            double stepXY = 0.0, db = 0.0;
            if (ctx.PrevP != null)
            {
                double dx = p[0] - ctx.PrevP[0];
                double dy = p[1] - ctx.PrevP[1];
                //db = p[2] - ctx.PrevP[2];
                stepXY = Math.Sqrt(dx * dx + dy * dy);
            }
            ctx.PrevP = (double[])p.Clone();

            double dCost = (ctx.Iter == 1) ? 0.0 : (ctx.LastCost - cost);
            ctx.LastCost = cost;

            ctx.Log.Debug($"LM iter={ctx.Iter:000} X={p[0]:F2} Y={p[1]:F2}" + $"Cost={cost:F6} dCost={dCost:F6} stepXY={stepXY:F3}m ");
        }

        public class LmContext
        {
            public List<DatosEntradaRLMCPorRSSI> DatosEntrada { get; set; }
            public double MuBeta;
            public double SigmaBeta;
            public double Lambda;
            public double DMinMeters;

            public Logger Log = LogManager.GetCurrentClassLogger(); // o tu logger Serilog/NLog/etc.

            // estados de logging
            public int Iter;
            public double LastCost;
            public double[] PrevP;
        }

        internal double CalcularBetha0(List<DatosEntradaRLMCPorRSSI> lTorre)
        {
            if (lTorre is null)
                throw new ArgumentNullException(nameof(lTorre));

            if (lTorre.Count == 0)
                throw new ArgumentException("La lista de torres no puede estar vacía.", nameof(lTorre));

            double betha0 = 0;

            double sumPond = lTorre.Sum(i => i.Betha * i.VarBetha);
            double sumPesos = lTorre.Sum(i => i.VarBetha);
            betha0 = sumPond / sumPesos;

            if (sumPesos == 0)
                throw new InvalidOperationException("La suma de VarBetha es 0, no se puede calcular Betha0.");

            return betha0;
        }

        internal Vector2d CalcularCentroidePonderado(List<DatosEntradaRLMCPorRSSI> datosEntrada)
        {
            double pesos = 0;
            double sumaPondX = 0;
            double sumaPondY = 0;
            foreach (var item in datosEntrada)
            {
                double peso_i = 1.0;

                if (item.W > 0)
                    peso_i = item.W;
                    
                sumaPondX += peso_i * item.X;
                sumaPondY += peso_i * item.Y;
                pesos += peso_i;
            }

            Vector2d centroidePond = new Vector2d(sumaPondX/pesos, sumaPondY/pesos);

            return centroidePond;
        }
    }


    public class EstadoCalculo
    {
        public EnmResultadoCalculo Estado {  get; set; }
        public int NumeroIteraciones { get; set; }
        public int CodigoTerminacion { get; set; }


        public EstadoCalculo()
        {
            Estado = EnmResultadoCalculo.sinInicializar;
            NumeroIteraciones = 0;
            CodigoTerminacion = 0;
        }
    }


    public enum EnmResultadoCalculo
    {
        calculoExitoso,
        error,
        sinInicializar
    }

    public class ConfigSolverRLMC
    {
        // Definir valores predeterminados para x e y, por ejemplo 0.0
        public Vector2d p0;
        public double diffStep { get; set; }
        public double dMinMetros { get; set; }
        public double dMinBetha { get; set; }
        public int maxIter { get; set; }
        public double epsg {  get; set; }
        public double epsf { get; set; }
        public double epsx { get; set; }

        public ConfigSolverRLMC(double diffStep, int maxIter, double epsg, double epsf, double epsx, double dMinMetros, double dMinBetha, Vector2d p0)
        {
            this.p0 = p0;
            this.diffStep = diffStep;
            this.maxIter = maxIter;
            this.epsg = epsg;
            this.epsf = epsf;
            this.epsx = epsx;
            this.dMinMetros = dMinMetros;
            this.dMinBetha = dMinBetha;
        }
    }

    public class DatosEntradaRLMCPorRSSI
    {
        // Implementación de la estructura de datos de entrada para el solver RLMC basado en RSSI
        public double X { get; set; } // metros srid 22185
        public double Y { get; set; } // metros srid 22185
        public double RSSI { get; set; } // dBm
        public double VarRSSI { get; set; } // varianza dBm^2 sigma_dB^2
        public double Betha { get; set; } // coeficiente del modelo
        public double VarBetha { get; set; } // varianza del coeficiente
        public double W { get; set; }       // peso del dato, sale de la combinacion de las dos varianzas bet = 1/(VarRSSI + VarBetha)
        public double B { get; set; }       // pendiente en dB/km



        public DatosEntradaRLMCPorRSSI()
        {
            X = 0; 
            Y = 0;
            RSSI = 0;
            VarRSSI = 1;
            Betha = 0;
            VarBetha = 1;
            W = 0;
            B = 0;    
        }

        public bool Inicializar(CellInfo torre)
        {
            X = torre.X;
            Y = torre.Y;
            Betha = torre.Betha0;
            B = torre.B;
            RSSI = torre.RSSI_Rx;

            if (torre.AreaTipo == eTipoArea.UrbanoDenso || torre.AreaTipo == eTipoArea.Urbano)
            {
                //TODO: en clase usar enum y no int
                // Calcular bien los valores de varianza de betha segun la tecnologia, esto es un ejemplo
                switch ((eTipoTecnologia) torre.Tecnologia)
                {
                    case eTipoTecnologia.LTE:
                        {
                            
                            VarBetha = 8 * 8;
                            break;
                        }

                    case eTipoTecnologia.UMTS:
                        {
                            VarBetha = 9 * 9;
                            break;
                        }
                    case eTipoTecnologia.GSM:
                        {
                            VarBetha = 10 * 10;
                            break;
                        }
                }
            }
            return ValidarPrecondicionesDatosEntrada();
        }

    private bool ValidarPrecondicionesDatosEntrada()
    {
        // Validar que los datos de entrada cumplen con las precondiciones necesarias para utilizar en el cálculo
        if (RSSI >= 0) return false; // El RSSI debe estar en un rango
        if (VarBetha <= 0) return false; // La varianza del coeficiente debe ser positiva
        if (B <= 0) return false; // La pendiente debe ser positiva
        return true;
    
    }

}


    public class DatosSalidaRLMCPorRSSI
    {
        // Implementación de la estructura de datos de salida para el solver RLMC basado en RSSI
        public double PosicionX { get; set; } // metros srid 22185
        public double PosicionY { get; set; } // metros srid 22185
        public double VarX { get; set; }      // varianza en X metros^2
        public double VarY { get; set; }      // varianza en Y metros^2
        public double bethaEstimada { get; set; } // coeficiente estimado
        public double VarBethaEstimada { get; set; }    // varianza del coeficiente estimado
        public int Iteraciones { get; set; }            // cantidad de iteraciones realizadas
        public int CodigoTerminacion { get; set; }      // código de terminación del solver
    }


}
