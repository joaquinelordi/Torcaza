using Entidades;
using Entidades.CapaComunicacionBDD;
using Entidades.Tests.TestDataTrilateracion;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;
using static Entidades.Utiles;

namespace Entidades.Tests
{
    public class BethaCalculatorTests
    {
        private string connString;

        public BethaCalculatorTests()
        {
            var cfg = new ConfigurationBuilder()
                            .AddUserSecrets<BethaCalculatorTests>(optional: true)
                            .AddEnvironmentVariables()
                            .Build();

            connString = cfg["DatabaseTests:ConnectionString"];

        }

        [Fact]
        public void CalcularBetha0_PromedioPonderadoPorVarBetha_RetornaValorCorrecto()
        {
            // Arrange
            var sut = new SolverLManaliticoStrategy();
            var torres = new List<DatosEntradaRLMCPorRSSI>
        {
            new() { Betha = 10, VarBetha = 2 },  // 20
            new() { Betha = 20, VarBetha = 1 },  // 20
        };
            // sumPond = 40, sumPesos = 3 => 13.333...

            // Act
            var betha0 = sut.CalcularBetha0(torres);

            // Assert
            betha0.Should().BeApproximately(40.0 / 3.0, 1e-9);
        }

        [Fact]
        public void CalcularBetha0_ListaVacia_LanzaArgumentException()
        {
            var sut = new SolverLManaliticoStrategy();
            List<DatosEntradaRLMCPorRSSI> dataEmpty = new List<DatosEntradaRLMCPorRSSI>();
            Action act = () => sut.CalcularBetha0(dataEmpty);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CalcularBetha0_SumPesosCero_LanzaInvalidOperationException()
        {
            var sut = new SolverLManaliticoStrategy();
            var torres = new List<DatosEntradaRLMCPorRSSI>
        {
            new() { Betha = 10, VarBetha = 0 },
            new() { Betha = 20, VarBetha = 0 },
        };

            Action act = () => sut.CalcularBetha0(torres);

            act.Should().Throw<InvalidOperationException>();
        }
    }

    public class PostgisCalculosTests
    {

        private string _connString;

        public PostgisCalculosTests()
        {
            var cfg = new ConfigurationBuilder()
                            .AddUserSecrets<PostgisCalculosTests>(optional: true)
                            .AddEnvironmentVariables()
                            .Build();

            _connString = cfg["DatabaseTests:ConnectionString"];

        }

        private NpgsqlConnection OpenOrSkip()
        {
            if (string.IsNullOrWhiteSpace(_connString))
                throw SkipException.ForSkip("Falta DatabaseTests:ConnectionString en User Secrets o variables de entorno. Omitiendo tests PostGIS.");

            var conn = new NpgsqlConnection(_connString);
            conn.Open();
            return conn;
        }


        [Fact]
        public async Task Postgis_RoundTrip_Wgs84_A_22185_A_Wgs84()
        {
            using var conn = OpenOrSkip();

            // Punto de prueba (lon, lat).
            var gps = new Vector2d(-58.3816, -34.6037);
            var input = new List<Vector2d> { gps };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // WGS84 -> 22185
            var xyList = await PostgisTransforms.TransformarTuplasGpsA22185Async(conn, input, cts.Token);
            xyList.Should().HaveCount(1);

            var xy = xyList[0];
            double.IsNaN(xy.X).Should().BeFalse();
            double.IsNaN(xy.Y).Should().BeFalse();

            // 22185 -> WGS84
            var gpsBack = await PostgisTransforms.Transformar22185AWgsAsync(conn, xy, cts.Token);

            // Tolerancia en grados (1e-6 ~ 0.11 m en lat; varía en lon)
            gpsBack.X.Should().BeApproximately(gps.X, 1e-6);
            gpsBack.Y.Should().BeApproximately(gps.Y, 1e-6);
        }

        [Fact]
        public async Task Postgis_RoundTrip_22185_A_Wgs84_A_22185()
        {
            using var conn = OpenOrSkip();

            // Punto en 22185 (X,Y en metros).
            var gps = new Vector2d(-57.9545, -34.9214);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var xy = (await PostgisTransforms.TransformarTuplasGpsA22185Async(conn, new[] { gps }, cts.Token))[0];

            // 22185 -> WGS84
            var gpsBack = await PostgisTransforms.Transformar22185AWgsAsync(conn, xy, cts.Token);

            // WGS84 -> 22185
            var xyBack = (await PostgisTransforms.TransformarTuplasGpsA22185Async(conn, new[] { gpsBack }, cts.Token))[0];

            xyBack.X.Should().BeApproximately(xy.X, 0.05); // 5 cm
            xyBack.Y.Should().BeApproximately(xy.Y, 0.05);
        }
    }

    public class SolverLManaliticoStrategy_Residuos_Tests
    {
        [Fact]
        public void CalcularResiduos_EnGroundTruth_DaCero()
        {
            var xTrue = 200.0;
            var yTrue = -150.0;
            var betaTrue = 50.0;
            var B = 30.0;
            // posiciones de los puntos conocidos
            Vector2d[] values = { new Vector2d(0, 0), new Vector2d(1000, 0), new Vector2d(0, 1000), new Vector2d(1000, 1000) };

            var datos = RlmcScenario.BuildPerfectData(xTrue, yTrue, betaTrue, B, values);

            var ctx = new SolverLManaliticoStrategy.LmContext
            {
                DatosEntrada = datos,
                DMinMeters = 1.0,
                Lambda = 0.0
            };

            var p = new[] { xTrue, yTrue };
            var fi = new double[datos.Count]; // +1 por prior (no se usa si Lambda=0)

            SolverLManaliticoStrategy.CalcularResiduos(p, fi, ctx);

            for (int i = 0; i < datos.Count; i++)
                fi[i].Should().BeApproximately(0.0, 1e-9);
        }
    }

    public class SolverLManaliticoStrategy_Jacobiano_Tests
    {
        [Fact]
        public void Jacobiano_Analitico_Coincide_Con_DiferenciaFinita()
        {
            var x = 200.0;
            var y = -150.0;
            var beta = 50.0;
            var B = 30.0;
            Vector2d[] values = { new Vector2d(0, 0), new Vector2d(1000, 0), new Vector2d(0, 1000)};
            var datos = RlmcScenario.BuildPerfectData(
                x, y, beta, B, values);

            var ctx = new SolverLManaliticoStrategy.LmContext
            {
                DatosEntrada = datos,
                DMinMeters = 1.0,
                Lambda = 0.0
            };

            var p = new[] { x + 12.3, y - 7.8}; // punto no trivial
            int m = datos.Count;

            var fi = new double[m];
            var jac = new double[m, 2];
            SolverLManaliticoStrategy.CalcularResiduosYJacobiano(p, fi, jac, ctx);

            // Diferencia finita central
            double h = 1e-4;

            for (int j = 0; j < 2; j++)
            {
                var pp = (double[])p.Clone();
                var pm = (double[])p.Clone();
                pp[j] += h;
                pm[j] -= h;

                var fPlus = new double[m];
                var fMinus = new double[m];
                SolverLManaliticoStrategy.CalcularResiduos(pp, fPlus, ctx);
                SolverLManaliticoStrategy.CalcularResiduos(pm, fMinus, ctx);

                for (int i = 0; i < m; i++)
                {
                    double fd = (fPlus[i] - fMinus[i]) / (2 * h);
                    jac[i, j].Should().BeApproximately(fd, 1e-3);
                }
            }
        }
    }

    public class SolverLManaliticoStrategy_Resolver_Tests
    {
        [Fact]
        public void Resolver_2D_ConDatosPerfectos_Recupera_Posicion_Y_Reduce_Costo_MismoBetha()
        {
            // Arrange
            var sut = new SolverLManaliticoStrategy();

            var xTrue = 200.0;
            var yTrue = -150.0;

            // "Beta base" fijo por torre, para este test lo hacemos igual en todas para generar datos consistentes
            var betaBase = 50.0;
            var B = 30.0;

            Vector2d[] torres = { new(0, 0), new(1000, 0), new(0, 1000), new(1000, 1000) };

            var datos = RlmcScenario.BuildPerfectData(xTrue, yTrue, betaBase, B, torres);

            // pesos iguales
            datos.ForEach(t => t.W = 1);

            var p0 = sut.CalcularCentroidePonderado(datos);

            var cfg = new ConfigSolverRLMC(
                diffStep: 1e-6,
                maxIter: 200,
                epsg: 1e-12,
                epsf: 1e-12,
                epsx: 1e-8,
                dMinMetros: 1.0,
                dMinBetha: 0.1,
                p0: p0
            );

            // Calculamos costo inicial en el punto inicial que usa el solver (centroide)
            var ctx = new SolverLManaliticoStrategy.LmContext
            {
                DatosEntrada = datos,
                DMinMeters = cfg.dMinMetros,
                Lambda = 0.0
            };

            double Cost(double x, double y)
            {
                var p = new[] { x, y };
                var fi = new double[datos.Count];
                SolverLManaliticoStrategy.CalcularResiduos(p, fi, ctx);
                return fi.Sum(v => v * v);
            }

            var cost0 = Cost(p0.X, p0.Y);

            // Act
            var p = sut.Resolver(datos, cfg, out var estado);

            // Assert: tamaño
            p.Should().NotBeNull();
            p.Length.Should().Be(2);

            // Assert: se acerca al verdadero
            p[0].Should().BeApproximately(xTrue, 0.5); // 50 cm
            p[1].Should().BeApproximately(yTrue, 0.5);

            // Assert: reduce costo
            var cost1 = Cost(p[0], p[1]);
            cost1.Should().BeLessThan(cost0);

            // Assert: termination type razonable (depende de ALGLIB, pero >0 suele ser ok)
            estado.CodigoTerminacion.Should().NotBe(0);
        }

        [Fact]
        public void Resolver_2D_ConRuido_Recupera_Posicion_Aprox_MismoBetha()
        {
            var sut = new SolverLManaliticoStrategy();

            var xTrue = 200.0;
            var yTrue = 300.0;
            var betaBase = 50.0;
            var B = 30.0;
            var dErrorMax = 50.0;

            //Posiciones de torres conociadas
            Vector2d[] torres = { new(0, 0), new(1000, 0), new(0, 1000), new(1000, 1000), new(800, 500) };

            // Ruido Normal reproducible: sigma parametrizable , seed fijo (repetible)
            var datos = RlmcScenario.BuildDataWithNoise(
                xTrue, yTrue, betaBase, B, torres,
                sigmaRssiDb: 2.0,
                seed: 20260224,
                outlierProb: 0.05,
                outlierSigmaMultiplier: 6.0
            );

            var p0 = sut.CalcularCentroidePonderado(datos);

            var cfg = new ConfigSolverRLMC(
                diffStep: 1e-6,
                maxIter: 300,
                epsg: 1e-12,
                epsf: 1e-12,
                epsx: 1e-8,
                dMinMetros: 1.0,
                dMinBetha: 0.1,
                p0
            );

            var p = sut.Resolver(datos, cfg, out var estado);

            // Con ruido no esperes 0.5m; poné tolerancia más realista.
            p[0].Should().BeApproximately(xTrue, dErrorMax); // 10 m (ajustable)
            p[1].Should().BeApproximately(yTrue, dErrorMax);
            estado.CodigoTerminacion.Should().NotBe(0);
        }

        public class SolverLManaliticoStrategy_Resolver_Ponderado_Tests
        {
            [Fact]
            public void Resolver_2D_RuidoDependienteDeRSSI_UsaPesosYMejora()
            {
                var sut = new SolverLManaliticoStrategy();

                var xTrue = 200.0;
                var yTrue = 150.0;
                var betaBase = -100.0;
                var B = 40.0;
                var dErrorMax = 50.0;
                //Relacion: torre más lejos -> señal más débil -> sigma alto -> W bajo
                Vector2d[] torres = { new(0, 0), new(600, 0), new(0, 900), new(1200, 800), new(2000, 2000) };

                var datos = RlmcScenario.BuildDataWithSignalDependentNoise(
                    xTrue, yTrue, betaBase, B, torres, seed: 20260224);

                // 1) Aseguramos que hay pesos distintos (dependen del RSSI ideal)
                datos.Select(d => d.W).Distinct().Count().Should().BeGreaterThan(1);

                var p0 = sut.CalcularCentroidePonderado(datos);

                var cfg = new ConfigSolverRLMC(
                    diffStep: 1e-6,
                    maxIter: 400,
                    epsg: 1e-12,
                    epsf: 1e-12,
                    epsx: 1e-8,
                    dMinMetros: 1.0,
                    dMinBetha: 0.1,
                    p0
                );

                // costo SSE ponderado (porque fi ya incluye sqrt(w))
                var ctx = new SolverLManaliticoStrategy.LmContext
                {
                    DatosEntrada = datos,
                    DMinMeters = cfg.dMinMetros,
                    Lambda = 0.0
                };

                double Cost(double x, double y)
                {
                    var p = new[] { x, y };
                    var fi = new double[datos.Count];
                    SolverLManaliticoStrategy.CalcularResiduos(p, fi, ctx);
                    return fi.Sum(v => v * v);
                }

                var cost0 = Cost(p0.X, p0.Y);

                // Act
                var p = sut.Resolver(datos, cfg, out var estado);

                // Assert básico
                p.Length.Should().Be(2);

                var cost1 = Cost(p[0], p[1]);
                cost1.Should().BeLessThan(cost0);

                // Con ruido + pesos, tolerancia más suave (ajustala según tu geometría)
                p[0].Should().BeApproximately(xTrue, dErrorMax);
                p[1].Should().BeApproximately(yTrue, dErrorMax);

                estado.CodigoTerminacion.Should().NotBe(0);
            }
        }
    }
}
