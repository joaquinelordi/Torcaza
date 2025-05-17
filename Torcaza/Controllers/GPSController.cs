using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Entidades;
using Npgsql;
using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite;
using NpgsqlTypes;


namespace Torcaza.Controllers
{
    [ApiController]
    [Route("gps")]
    public class GPSController : ControllerBase
    {
        private readonly string _connectionSQLString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";

        [HttpPost]
        [Route("prueba")]
        public IActionResult PruebaPost([FromBody] Persona persona)
        {
            return Ok(persona.nombre);
        }

        [HttpGet]
        [Route("prueba")]
        public IActionResult PruebaGet([FromQuery] ConsultaBD consulta)
        {
            // Validar Datos de consulta
            if (consulta == null)
            {
                consulta = new ConsultaBD
                {
                    FechaDesde = new DateTime(2000, 1, 1),
                    FechaHasta = DateTime.Now,
                    CantidadPuntosMaxima = 1000 // Valor por defecto
                };
            }
            else
            {
                if (consulta.FechaDesde == default)
                {
                    consulta.FechaDesde = new DateTime(2000, 1, 1);
                }

                if (consulta.FechaHasta == default)
                {
                    consulta.FechaHasta = DateTime.Now;
                }

                if (consulta.CantidadPuntosMaxima == default)
                {
                    consulta.CantidadPuntosMaxima = 100; // Valor por defecto
                }
            }

            List<Ubicacion> ubicaciones = new List<Ubicacion>();

            try
            {
                // Configurar Npgsql para usar NetTopologySuite a nivel de origen de datos
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionSQLString);
                dataSourceBuilder.UseNetTopologySuite();
                var dataSource = dataSourceBuilder.Build();

                using var connection = dataSource.OpenConnection();

                // Llamada a la función crear_historial_ubicacion
                using var command = new NpgsqlCommand("SELECT ubi_coordenadas, ubi_timestamp FROM crear_historial_ubicacion(@idRegistro, @idDispositivo, @agenteID, @fechaDesde, @fechaHasta)", connection);
                command.Parameters.AddWithValue("idRegistro", Guid.Parse("550e8400-e29b-41d4-a716-446655440000"));
                command.Parameters.AddWithValue("idDispositivo", Guid.Parse("550e8400-e29b-41d4-a716-446655440001"));
                command.Parameters.AddWithValue("agenteID", Guid.Parse("550e8400-e29b-41d4-a716-446655440002"));
                command.Parameters.AddWithValue("fechaDesde", consulta.FechaDesde);
                command.Parameters.AddWithValue("fechaHasta", consulta.FechaHasta);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var geom = reader.GetFieldValue<Point>(0);
                    var timestamp = Convert.ToDateTime(reader["ubi_timestamp"]);

                    // Extraer latitud y longitud del campo geometry
                    var latitud = geom.Y;
                    var longitud = geom.X;

                    var ubicacion = new Ubicacion
                    {
                        IDAgente = "550e8400-e29b-41d4-a716-446655440002", // Ajustar según sea necesario
                        Latitud = Math.Round(latitud, 4).ToString(),
                        Longitud = Math.Round(longitud, 4).ToString(),
                        Timestamp = timestamp
                    };

                    ubicaciones.Add(ubicacion);
                }
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error al conectar a la base de datos");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar a la base de datos: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.InnerException?.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error al conectar a la base de datos");
            }

            if (ubicaciones.Count == 0)
            {
                return NotFound("No se encontró ninguna ubicación");
            }

            // Devolver la ubicación más reciente
            //var ubicacionMasReciente = ubicaciones.OrderByDescending(u => u.Timestamp).FirstOrDefault();
            return Ok(ubicaciones);
        }
    }
}
