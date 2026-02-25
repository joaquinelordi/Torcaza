using Entidades.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Entidades
{
    public enum eTipoArea
    {
        Urbano,
        UrbanoDenso, // para áreas urbanas densamente pobladas
        Suburbano,
        Rural,
        Desconocido
    }

    /// <summary>
    /// Tipo de tecnología de acceso inalámbrico sale de la documentacion para el campo TECH
    /// </summary>
    public enum eTipoTecnologia
    {
        GSM = 0,                    // 2G GSM
        UMTS = 2,                   // 3G UTRAN
        EGPRS = 3,                  // GSM W/EGPRS
        UMTS_HSDPA = 4,             // 3G UTRAN HSDPA
        UMTS_HSUPA = 5,             // 3G UTRAN HSUPA
        UMTS_HSDPA_HSUPA = 6,       // 3G SUPERSAYAYIN
        LTE = 7                     // 4G LTE E-UTRAN
    }

    public class HandlerCanalInalambrico
    {
        public string Banda { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public double SenialdB { get; set; }
        public int TecnologiaAcceso { get; set; }
        public int Canal { get; set; }
        public string CelId { get; set; }

        private IModeloRangoDeDistancia _modeloRangoDeDistancia;
        private ICalcularPosicion _estrategiaCalcularPosicion; 
        private CellInfo _cellInfo;
        private CellParametrosCaracterizacion _parametros;
        public ParametrosModelo ParametrosModelo { get; set; }


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

        public void Inicializar(ref IList<InfoCell> infoTorreCelulares)
        {
            // Inicializa el modelo de rango de distancia y la estrategia de cálculo de posición
            _modeloRangoDeDistancia = new ModeloOkumuraHata();
            //_estrategiaCalcularPosicion = new CalcularPosicionRLMC();
            _estrategiaCalcularPosicion = new CalcularPosicionRLMCPorRSSI();

            _cellInfo = new CellInfo(
                Convert.ToInt64(this.CelId, 16),
                this.Latitud ?? 0,
                this.Longitud ?? 0,
                this.TecnologiaAcceso,
                this.Banda,
                this.Canal,
                this.alturaAntenaTx,
                this.tipoArea,
                this.SenialdB,
                this.ParametrosModelo.B,
                this.ParametrosModelo.Betha0
                );

            _parametros = new CellParametrosCaracterizacion(
                SeleccionarFrecuenciaMHz(this.TecnologiaAcceso, this.Banda, this.Canal),
                this.alturaAntenaTx,
                this.alturaAntenaRx,
                this.tipoArea,
                this.TecnologiaAcceso,
                this.Banda,
                this.Canal,
                this.ParametrosModelo.B,
                this.ParametrosModelo.Betha0,
                0
                );

        }

        private int SeleccionarFrecuenciaMHz(int tecnologia, string? banda, int? canal)
        {
            // Selecciona una frecuencia representativa en MHz según la tecnología, banda y canal
            // Aquí se usan valores típicos; en producción, usar una base de datos real
            bool bBanda = !string.IsNullOrEmpty(banda);
            switch ((eTipoTecnologia) tecnologia)
            {
                case eTipoTecnologia.GSM:
                    return 900; // default GSM

                case eTipoTecnologia.UMTS: 
                case eTipoTecnologia.UMTS_HSUPA:
                case eTipoTecnologia.UMTS_HSDPA:
                case eTipoTecnologia.UMTS_HSDPA_HSUPA:
                    return 2100; // default UMTS

                case eTipoTecnologia.EGPRS:
                    return 1800; // default EGPRS
                
                case eTipoTecnologia.LTE:
                    if (bBanda)
                    {
                        if (banda == "LTE BAND 2") return 1150;
                        if (banda == "LTE BAND 4") return 2000;
                        if (banda == "LTE BAND 5") return 2600;
                        if (banda == "LTE BAND 5") return 2600;
                        if (banda == "LTE BAND 7") return 2850;
                        if (banda == "LTE BAND 28") return 9260;
                    }
                    return 1800; // default LTE
                
                default:
                    return 700; // frecuencia por defecto si no se reconoce la tecnología
            }
        }


        /// <summary>
        /// Calcula la distancia a la torre celular en metros.
        /// </summary>
        /// <returns></returns>
        public RangoEstimado CalcularDistanciaATorreCelular()
        {
            return _modeloRangoDeDistancia.Estimar(_cellInfo, _parametros);
        }

        public DatosSalida CalcularPosicion(IDictionary<long, CellInfo> torres, System.Numerics.Vector2? x0 = null)
        {
            return _estrategiaCalcularPosicion.Calcular(torres);
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
    }

    /// <summary>
    /// Información de calculo para una celda celular.
    /// </summary>
    /// <param name="CellId"></param>
    /// <param name="X"></param>
    /// <param name="Y"></param>
    /// <param name="Tecnologia"></param>
    /// <param name="Band"></param>
    /// <param name="Channel"></param>
    /// <param name="HbMeters"></param>
    /// <param name="AreaTipo"></param>
    public record CellInfo(
        long CellId,
        double X, double Y,            // metros en SRID métrico
        int Tecnologia,
        string? Band,
        int? Channel,
        double? AlturaTx,              // altura torre celular en metros
        eTipoArea AreaTipo,
        double RSSI_Rx,                // nivel de senial recibida por dispositivo de la torre en dBm
        double B,                     // pendiente del modelo simplificado de path loss
        double Betha0                 // parametro que engloba los demas terminos del modelo simplificado de path loss
    ); 


    /// <summary>
    /// Estimación de rango de distancia a una celda celular
    /// </summary>
    /// <param name="CellId"></param>
    /// <param name="DistanciaM"></param>
    /// <param name="Varianza"></param>
    public record RangoEstimado (
        long CellId,
        double DistanciaM,          // d̂ en metros
        double Varianza             // Var[d̂] varianza de la distancia en metros2
    );

    /// <summary>
    /// Parámetros de caracterización del canal inalámbrico, Datos a utilizar en el modelo
    /// </summary>
    /// <param name="FrecuenciaMHz"></param>
    /// <param name="AlturaTx"></param>
    /// <param name="AlturaRx"></param>
    /// <param name="TipoArea"></param>
    /// <param name="Tecnologia"></param>
    /// <param name="Band"></param>
    /// <param name="Channel"></param>
    public record CellParametrosCaracterizacion (
        double FrecuenciaMHz,
        double AlturaTx,
        double AlturaRx,
        eTipoArea TipoArea,
        int Tecnologia,
        string? Band,
        int? Channel,
        double Betha0,
        double B,
        double RSSI_Rx
    );


}
                            
