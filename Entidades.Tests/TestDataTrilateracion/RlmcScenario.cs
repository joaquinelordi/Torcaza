using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Entidades.Utiles;

namespace Entidades.Tests.TestDataTrilateracion
{
    public static class RlmcScenario
    {
        public static List<DatosEntradaRLMCPorRSSI> BuildPerfectData(double xTrue, double yTrue, double betaTrue, double B, Vector2d[] torresXY)
        {
            var list = new List<DatosEntradaRLMCPorRSSI>();

            foreach (var v in torresXY)
            {
                var dx = xTrue - v.X;
                var dy = yTrue - v.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1) d = 1;

                var rssi = betaTrue - B * Math.Log10(d / 1000.0);

                list.Add(new DatosEntradaRLMCPorRSSI
                {
                    X = v.X,
                    Y = v.Y,
                    RSSI = rssi,
                    Betha = betaTrue,
                    VarBetha = 1,
                    VarRSSI = 1,
                    W = 1,
                    B = B
                });
            }

            return list;
        }

        // VA Normal N(0,1) via Box–Muller (determinista con seed)
        public static double NextGaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// Genera datos sintéticos consistentes con el modelo, pero con ruido controlado en RSSI (dB).
        /// - xTrue,yTrue: posición real
        /// - betaBase: Betha base fijada por torre/tech (se setea en t_i.Betha)
        /// - B: pendiente del modelo (t_i.B)
        /// - sigmaRssiDb: desvío estándar del ruido (dB)
        /// - seed: para reproducibilidad
        /// - outlierProb/outlierSigmaMultiplier: opcional para simular outliers
        /// </summary>
        public static List<DatosEntradaRLMCPorRSSI> BuildDataWithNoise(double xTrue, double yTrue, double betaBase, double B, Vector2d[] torresXY, double sigmaRssiDb, int seed = 12345, double outlierProb = 0.0, double outlierSigmaMultiplier = 5.0)
        {
            if (torresXY is null) throw new ArgumentNullException(nameof(torresXY));
            if (sigmaRssiDb < 0) throw new ArgumentOutOfRangeException(nameof(sigmaRssiDb));
            if (outlierProb < 0 || outlierProb > 1) throw new ArgumentOutOfRangeException(nameof(outlierProb));
            if (outlierSigmaMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(outlierSigmaMultiplier));

            var rng = new Random(seed);
            var list = new List<DatosEntradaRLMCPorRSSI>(torresXY.Length);

            double varRssi = sigmaRssiDb * sigmaRssiDb;

            foreach (var v in torresXY)
            {
                double dx = xTrue - v.X;
                double dy = yTrue - v.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1) d = 1;

                // Modelo
                double log10_dKm = Math.Log10(d / 1000.0);
                double rssiIdeal = betaBase - B * log10_dKm;

                // Ruido gaussiano controlado
                double sigma = sigmaRssiDb;
                if (outlierProb > 0.0 && rng.NextDouble() < outlierProb)
                    sigma *= outlierSigmaMultiplier;

                double noise = (sigma == 0) ? 0.0 : sigma * NextGaussian(rng);
                double rssiNoisy = rssiIdeal + noise;

                // Peso simple: w = 1/VarRSSI (si VarRSSI=0 => w=1)
                double w = (varRssi > 0) ? (1.0 / varRssi) : 1.0;

                list.Add(new DatosEntradaRLMCPorRSSI
                {
                    X = v.X,
                    Y = v.Y,
                    RSSI = rssiNoisy,
                    VarRSSI = varRssi,
                    Betha = betaBase,
                    VarBetha = 1.0,
                    W = w,
                    B = B
                });
            }

            return list;
        }


        /// <summary>
        /// Genera datos con ruido dependiente del nivel de RSSI:
        /// - Señal fuerte => sigma bajo
        /// - Señal débil => sigma alto
        ///
        /// Regla por defecto:
        ///   RSSI >= -70 dBm => sigma 1.0 dB
        ///   -85..-70        => sigma 2.0 dB
        ///   -95..-85        => sigma 4.0 dB
        ///   < -95           => sigma 6.0 dB
        /// </summary>
        public static List<DatosEntradaRLMCPorRSSI> BuildDataWithSignalDependentNoise(
            double xTrue,
            double yTrue,
            double betaBase,
            double B,
            Vector2d[] torresXY,
            int seed = 12345)
        {
            var rng = new Random(seed);
            var list = new List<DatosEntradaRLMCPorRSSI>(torresXY.Length);

            foreach (var v in torresXY)
            {
                double dx = xTrue - v.X;
                double dy = yTrue - v.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1) d = 1;

                double log10_dKm = Math.Log10(d / 1000.0);
                double rssiIdeal = betaBase - B * log10_dKm;

                // sigma según nivel de señal (dB)
                double sigmaDb =
                    (rssiIdeal >= -70) ? 1.0 :
                    (rssiIdeal >= -85) ? 2.0 :
                    (rssiIdeal >= -95) ? 4.0 :
                                         6.0;

                double noise = sigmaDb * NextGaussian(rng);
                double rssiNoisy = rssiIdeal + noise;

                double varRssi = sigmaDb * sigmaDb;
                double w = 1.0 / varRssi;

                list.Add(new DatosEntradaRLMCPorRSSI
                {
                    X = v.X,
                    Y = v.Y,
                    RSSI = rssiNoisy,
                    VarRSSI = varRssi,
                    Betha = betaBase,   // fijo por torre
                    VarBetha = 1.0,
                    W = w,
                    B = B
                });
            }

            return list;
        }

        public static List<DatosEntradaRLMCPorRSSI> BuildDataFromRealCalibration(
            Vector2d[] torresXY,
            double[] rssis,
            double betaGlobal,
            double bGlobal,
            bool usarPesosWls)
        {
            if (torresXY == null) throw new ArgumentNullException(nameof(torresXY));
            if (rssis == null) throw new ArgumentNullException(nameof(rssis));
            if (torresXY.Length != rssis.Length)
                throw new ArgumentException("La cantidad de torres debe coincidir con la cantidad de RSSI.");

            var list = new List<DatosEntradaRLMCPorRSSI>(torresXY.Length);

            for (int i = 0; i < torresXY.Length; i++)
            {
                var rssi = rssis[i];

                // Peso simple normalizado usando umbral -110 dB
                // Señal más débil => menor peso
                var w = usarPesosWls
                    ? Math.Max(0.01, (110.0 + rssi) / 110.0)
                    : 1.0;

                list.Add(new DatosEntradaRLMCPorRSSI
                {
                    X = torresXY[i].X,
                    Y = torresXY[i].Y,
                    RSSI = rssi,
                    Betha = betaGlobal,
                    VarBetha = 1.0,
                    VarRSSI = 1.0,
                    W = w,
                    B = bGlobal
                });
            }

            return list;
        }
    }
}
