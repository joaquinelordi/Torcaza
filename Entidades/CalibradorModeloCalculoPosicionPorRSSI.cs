using Entidades.Interfaces;
using NetTopologySuite.Geometries;
using NetTopologySuite.Mathematics;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class CalibradorModeloCalculoPosicionPorRSSI
    {
        private readonly int MIN_CANT_MUESTRAS = 4;
        HandlerCalibradorModeloRSSI _handler;
        private readonly double _umbralRSSI = -100; // dBm, umbral mínimo de RSSI para considerar una torre en el cálculo
        private readonly string _connectionSQLString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";


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


        public ResultadoCalibracion TestCalibrarParametrosModeloRSSI()
        {
            /*
            int ubi_nroevento = 257;
            DateTime fechaDesde = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);
            DateTime fechaHasta = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            Point posicionVerdadera = ObtenerPosicionGPS(_connectionSQLString, ubi_nroevento);

            List<TorreMuestraDb> torres = ObtenerTorresPromediadas(
                _connectionSQLString,
                tecnologia: 4,
                fechaDesde: fechaDesde,
                fechaHasta: fechaHasta,
                rssiMinimo: -100,
                celIdsExcluidos: new long[] { 128055044 });

            List<Muestra> muestras = InicializarMuestras(posicionVerdadera, torres);

            if (muestras.Count == 0)
                throw new InvalidOperationException("No se pudieron construir muestras válidas para calibración.");


            HandlerCalibradorModeloRSSI handler = new HandlerCalibradorModeloRSSI(new ConfigCalibradorModeloRSSI());
            return CalibrarParametrosModeloRSSI(muestras, handler);
            */
            return new ResultadoCalibracion();
        }

        private Point ObtenerPosicionGPS(string connectionString, long ubiNroEvento)
        {
            const string sql = @"
                SELECT ubi_coordenadas
                FROM ubicacion
                WHERE ubi_nroevento = @ubi_nroevento
                LIMIT 1;";

            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ubi_nroevento", ubiNroEvento);

            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException($"No se encontró ubi_coordenadas para ubi_nroevento = {ubiNroEvento}.");

            if (result is not Point punto)
                throw new InvalidOperationException("La columna ubi_coordenadas no pudo convertirse a Point.");

            return punto;
        }

        private List<TorreMuestraDb> ObtenerMuestraTorres(
            string connectionString,
            int tecnologia,
            DateTime fechaDesde,
            DateTime fechaHasta,
            int rssiMinimo,
            IReadOnlyCollection<long> celIdsExcluidos)
        {
            string sql = @"
                SELECT 
                    cel_id,
                    cell_coordenadas,
                    AVG(cell_nivelsenial)::double precision AS rssi_avg_db,
                    cel_tecnologia,
                    ST_Distance(ST_Transform(c.cell_coordenadas, 22185),p0.p0_22185) AS distancia_m
                FROM celdas_celulares
                WHERE cel_tecnologia = @tecnologia
                  AND cell_coordenadas IS NOT NULL
                  AND cell_timestamp BETWEEN @fechaDesde AND @fechaHasta
                  AND cell_nivelsenial > @rssiMinimo
                  AND cel_id <> ALL(@celIdsExcluidos)
                GROUP BY cel_id, cell_coordenadas, cel_tecnologia;";

            List<TorreMuestraDb> resultado = new();

            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tecnologia", tecnologia);
            cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
            cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);
            cmd.Parameters.AddWithValue("@rssiMinimo", rssiMinimo);
            cmd.Parameters.AddWithValue("@celIdsExcluidos", celIdsExcluidos.ToArray());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long celId = reader.GetInt64(0);
                Point coordenadas = reader.GetFieldValue<Point>(1);
                double rssiPromedio = reader.GetDouble(2);
                int tecnologiaLeida = Convert.ToInt32(reader.GetValue(3));

                resultado.Add(new TorreMuestraDb
                {
                    CelId = celId,
                    Coordenadas = coordenadas,
                    RssiPromedio = rssiPromedio,
                    Tecnologia = tecnologiaLeida
                });
            }

            return resultado;
        }

        private List<Muestra> InicializarMuestras(Point posicionVerdadera4326, List<TorreMuestraDb> torres)
        {
            List<Muestra> muestras = new();

            foreach (TorreMuestraDb torre in torres)
            {
                if (torre.Coordenadas == null)
                    continue;
                /*
                double distanciaM = CalcularDistanciaAproximadaMetros(
                    posicionVerdadera4326.Y, posicionVerdadera4326.X,
                    torre.Coordenadas.Y, torre.Coordenadas.X);

                if (distanciaM <= 0)
                    continue;

                muestras.Add(new Muestra
                {
                    Distancia_m = distanciaM,
                    Rssi_db = torre.RssiPromedio,
                    W = CalcularPesoInicial(torre.RssiPromedio, distanciaM)
                });
                */
            }

            return muestras;
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

    internal sealed class TorreMuestraDb
    {
        public long CelId { get; set; }
        public Point Coordenadas { get; set; } = default!;
        public double RssiPromedio { get; set; }
        public int Tecnologia { get; set; }
    }
}
