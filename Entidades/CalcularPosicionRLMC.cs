using Entidades.Interfaces;
using GeoAPI.CoordinateSystems.Transformations;
using GeoAPI.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite;
using Npgsql;
using Entidades.CapaComunicacionBDD;
using static Entidades.Utiles;

using static Entidades.CalcularPosicionRLMC;

namespace Entidades
{

    /// <summary>
    /// EN DESUSO
    /// </summary>
    public class CalcularPosicionRLMC : ICalcularPosicion
    {
        private readonly string _connectionSQLString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";

        public DatosSalida Calcular(IList<RangoEstimado> rangoEstimados, IDictionary<long, CellInfo> torres)
        {
            DatosSalida resultado = new DatosSalida();
            try
            {
                List<TuplaCalculo> tuplas = new List<TuplaCalculo>();

                // Construir las tuplas (X, Y, R, Var) para calcular la posición
                foreach (var re in rangoEstimados)
                {
                    if (torres.TryGetValue(re.CellId, out CellInfo torre))
                    {
                        tuplas.Add(new TuplaCalculo(torre.X, torre.Y, re.DistanciaM, re.Varianza));
                    }
                }

                Vector2d posicion = CalcularPosicionDesdeGPS(tuplas);
                resultado = new DatosSalida(posicion);
            }
            catch (Exception ex)
            {
                // Manejo de la excepción (puedes registrar el error o lanzar una excepción personalizada)
                Console.WriteLine($"Error al calcular la posición: {ex.Message}");
                resultado = new DatosSalida(new Vector2d(double.NaN, double.NaN));
            }

            return resultado;
        }

        public record TuplaCalculo(double X, double Y, double R, double Var);

        /// <summary>
        /// Calcula la posición usando el método de ciadrados minimos lineal (RLMC).
        /// </summary>
        /// <param name="t"></param>
        /// <param name="x0"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private Vector2d calcularPosicionRLMC(IList<TuplaCalculo> t, Vector2d? x0 = null)
        {
            if (t == null || t.Count < 3)
                throw new ArgumentException("Se requieren al menos 3 rangos estimados para calcular la posición.");

            // Ancla de referencia (origen local)
            var a1 = t[0];
            double x1 = a1.X, y1 = a1.Y, r1 = a1.R;

            // Acumuladores (doble precisión)
            double A11 = 0, A12 = 0, A22 = 0, B1 = 0, B2 = 0;

            // Construcción en coordenadas relativas a a1
            for (int k = 1; k < t.Count; k++)
            {
                var tk = t[k];

                // Diferencias respecto a a1 (metros, magnitudes “chicas”)
                double dx = tk.X - x1;
                double dy = tk.Y - y1;

                double ai1 = 2.0 * dx;     // 2(xk - x1)
                double ai2 = 2.0 * dy;     // 2(yk - y1)

                // ¡Clave!: bi en marco relativo (evita xk^2 + yk^2 gigantes)
                // bi = (dx^2 + dy^2) - (rk^2 - r1^2)
                double bi = (dx * dx + dy * dy) - (tk.R * tk.R - r1 * r1);

                // Sin pesos (o podrías usar 1/Var si confiás en la varianza)
                double wi = 1.0;
                // if (tk.Var > 1e-12) wi = 1.0 / tk.Var;

                A11 += wi * ai1 * ai1;
                A12 += wi * ai1 * ai2;
                A22 += wi * ai2 * ai2;
                B1 += wi * ai1 * bi;
                B2 += wi * ai2 * bi;
            }

            double det = A11 * A22 - A12 * A12;
            if (Math.Abs(det) < 1e-9)
                return new Vector2d(x1, y1); // fallback: ancla 1

            double inv11 = A22 / det;
            double inv12 = -A12 / det;
            double inv22 = A11 / det;

            // Solución en el MARCO LOCAL (desplazamiento respecto de a1)
            double ux = inv11 * B1 + inv12 * B2;
            double uy = inv12 * B1 + inv22 * B2;

            // Volver al marco absoluto sumando a1
            double X = x1 + ux;
            double Y = y1 + uy;

            return new Vector2d(X, Y);
        }

        // EN DESUSO
        /// Wrapper: recibe tuplas en GPS (lon°,lat°), resuelve en 22185 y devuelve (lon°,lat°).
        public Vector2d CalcularPosicionDesdeGPS(IList<TuplaCalculo> tuplasGps)
        {
            try
            {
                // Configurar Npgsql para usar NetTopologySuite a nivel de origen de datos  
                using var conexion = new NpgsqlConnection(_connectionSQLString);
                conexion.Open();

                // Corregir el uso de TransformarTuplasGpsA22185Async para manejar la tarea correctamente  
                var tuplaTransformadaTask = PostgisTransforms.TransformarTuplasGpsA22185Async(conexion, (IReadOnlyList<Vector2d>)tuplasGps);
                tuplaTransformadaTask.Wait();
                List<Vector2d> tuplaTransformada = tuplaTransformadaTask.Result;

                var xy22185 = calcularPosicionRLMC(tuplasGps);

                // Transformar de vuelta a GPS si es necesario  
                var posicionGpsTask = PostgisTransforms.Transformar22185AWgsAsync(conexion, xy22185);
                posicionGpsTask.Wait();
                return posicionGpsTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al calcular la posición desde GPS: {ex.Message}");
                return new Vector2d(double.NaN, double.NaN);
            }
        }

        public DatosSalida Calcular(IDictionary<long, CellInfo> torres)
        {
            throw new NotImplementedException();
        }
    }
}
