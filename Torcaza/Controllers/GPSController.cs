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

        [HttpGet]
        [Route("registroTorresCelulares")]
        public IActionResult RegistroTorresCelularesGet([FromQuery] ConsultaBD consulta)
        {
            if (consulta == null)
            {
                return BadRequest("Consulta no puede ser nula. Por favor, proporcione una consulta válida.");
            }
            else
            {
                Guid numeroReporte;

                // estos datos son para pruebas, realmente no hacen falta pero hay que refactorizar el objeto consultaBD para hacerlo generico
                // a todas las consultas que se relizan en la BDD
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
                if (consulta.NumeroReporte != null)
                {
                    numeroReporte = consulta.NumeroReporte;
                }
                else
                {
                    numeroReporte = Guid.Parse("ee69eef7-4493-42fe-a196-82eccc518d5c");
                }


                // lista para guardar las torres celulares
                List<InfoCell> torres = new List<InfoCell>();

                try
                {
                    // Configurar Npgsql para usar NetTopologySuite a nivel de origen de datos
                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(_connectionSQLString);
                    dataSourceBuilder.UseNetTopologySuite();
                    var dataSource = dataSourceBuilder.Build();

                    using var connection = dataSource.OpenConnection();

                    // Cargo los parametros para la consulta de torres celulares
                    using var command = new NpgsqlCommand("SELECT * FROM obtener_celdas_por_nro_registro(@idRegistro, @idDispositivo, @agenteID, @nroReporte)", connection);
                    command.Parameters.AddWithValue("idRegistro", Guid.Parse("550e8400-e29b-41d4-a716-446655440000"));
                    command.Parameters.AddWithValue("idDispositivo", Guid.Parse("550e8400-e29b-41d4-a716-446655440001"));
                    command.Parameters.AddWithValue("agenteID", Guid.Parse("550e8400-e29b-41d4-a716-446655440002"));
                    command.Parameters.AddWithValue("nroReporte", numeroReporte); // Ajustar según sea necesario

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        var cellInfo = new InfoCell
                        {
                            Cellid = reader.GetInt64(reader.GetOrdinal("cell_id")).ToString("X"),   // En base se guarda en Bigint
                            Mcc = reader.GetInt64(reader.GetOrdinal("cell_mcc")),
                            Mnc = reader.GetInt64(reader.GetOrdinal("cell_mnc")),
                            Lac = reader.GetInt64(reader.GetOrdinal("cell_lac")).ToString("X"),     // En base se guarda en Bigint
                            TecnologiaAcceso = reader.GetInt32(reader.GetOrdinal("cell_tecnologia")),
                            Banda = reader.IsDBNull(reader.GetOrdinal("cell_band")) ? null : reader.GetString(reader.GetOrdinal("cell_band")),
                            Canal = reader.IsDBNull(reader.GetOrdinal("cell_channel")) ? 0 : reader.GetInt32(reader.GetOrdinal("cell_channel")),
                            senialdB = reader.GetDouble(reader.GetOrdinal("cell_nivelsenial")),
                            FechaHora = reader.GetDateTime(reader.GetOrdinal("cell_timestamp")),
                            NumeroRegistro = reader.GetGuid(reader.GetOrdinal("cell_reporte")).ToString(),
                        };
                        // Extraer lat/lon del campo geometry cell_coordenadas
                        if (!reader.IsDBNull(reader.GetOrdinal("cell_coordenadas")))
                        {
                            var point = reader.GetFieldValue<Point>(reader.GetOrdinal("cell_coordenadas"));
                            cellInfo.Lat = point.Y;
                            cellInfo.Lon = point.X;
                        }

                        torres.Add(cellInfo);
                    }

                    // METODO para calcular el radio aproximado de la posicion para la triangulacion
                    CalcularDistanciaATorreCelular(torres);

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

                if (torres.Count <= 0)
                {
                    return NotFound("No se encontró ninguna ubicación");
                }
                // 
                return Ok(torres);
            }
        }
        /// <summary>
        /// Metodo recibe una lista de torres celulares y calcula para cada una un radio de distancia segun el nivel de señal
        /// y ciertos parametros de configuracion (idealmente un strategy segun la posicion de la celda con distintos valores para los parametros de ajuste)
        /// 
        /// </summary>
        /// <param name="torres"></param>
        private void CalcularDistanciaATorreCelular(List<InfoCell> torres)
        {
            foreach (InfoCell cell in torres)
            {
                CalcularDistanciaATorreCelular(cell);
            }
        }

        private void CalcularDistanciaATorreCelular(InfoCell cell)
        {

        }
    }
}
