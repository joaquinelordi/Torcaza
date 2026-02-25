using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Numerics;
using static Entidades.CalcularPosicionRLMC;
using static Entidades.Utiles;

namespace Entidades.CapaComunicacionBDD
{
    internal class ConsultasPostgis
    {
    }

    public static class PostgisTransforms
    {
        // Transforma un lote de tuplas GPS (lon°,lat°) -> EPSG:22185 (X,Y en m)
        public static async Task<List<Vector2d>> TransformarTuplasGpsA22185Async(
            NpgsqlConnection conn,
            IReadOnlyList<Vector2d> tuplasGps,
            CancellationToken ct = default)
        {
            if (tuplasGps is null || tuplasGps.Count == 0)
                return new List<Vector2d>();
            try
            {
                // Serializamos como JSON numérico (usa punto decimal)
                string json = JsonConvert.SerializeObject(tuplasGps);

                const string sql = @"
                    WITH data AS (
                      SELECT
                        (j->>'X')::double precision   AS lon_deg,
                        (j->>'Y')::double precision   AS lat_deg
                      FROM jsonb_array_elements(@p::jsonb) AS j
                    ),
                    anclas AS (
                      SELECT
                        ST_Transform(ST_SetSRID(ST_MakePoint(lon_deg, lat_deg), 4326), 22185) AS g
                      FROM data
                    )
                    SELECT ST_X(g) AS x, ST_Y(g) AS y
                    FROM anclas;";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p", NpgsqlDbType.Jsonb, json);

                var lista = new List<Vector2d>(tuplasGps.Count);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                /*
                for (int i = 0; i < rd.FieldCount; i++)
                    Console.WriteLine($"{i}: {rd.GetName(i)} -> {rd.GetDataTypeName(i)} / {rd.GetFieldType(i)}");
                */
                while (await rd.ReadAsync(ct))
                {
                    // si preferís máxima robustez frente a numeric:
                    double x = rd.IsDBNull(0) ? double.NaN : rd.GetFieldValue<double>(0);
                    double y = rd.IsDBNull(1) ? double.NaN : rd.GetFieldValue<double>(1);
                    lista.Add(new Vector2d(x, y));
                }
                return lista;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error al transformar tuplas GPS a 22185.", ex);
            }
        }

        // (Opcional) transforma un punto 22185 -> WGS84 (lon°,lat°)
        public static async Task<Vector2d> Transformar22185AWgsAsync(
            NpgsqlConnection conn,
            Vector2d xy22185,
            CancellationToken ct = default)
        {
            const string sql = @"
                SELECT
                  ST_X(g4326) AS lon_deg,
                  ST_Y(g4326) AS lat_deg
                FROM (
                  SELECT ST_Transform(ST_SetSRID(ST_MakePoint(@x,@y),22185),4326) AS g4326
                ) s;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("x", NpgsqlDbType.Double, xy22185.X);
            cmd.Parameters.AddWithValue("y", NpgsqlDbType.Double, xy22185.Y);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                double lon = rd.GetDouble(0);
                double lat = rd.GetDouble(1);
                return new Vector2d(lon, lat);
            }
            else
            {
                throw new InvalidOperationException("No se obtuvo resultado de ST_Transform.");
            }
        }

    }
}
