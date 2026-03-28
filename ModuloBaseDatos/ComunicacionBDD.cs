using Entidades;
using ModuloAlertas;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using System;

namespace ModuloBaseDatos
{
    public class ComunicacionBDD
    {
        private readonly string _connectionSQLString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";

        public ComunicacionBDD()
        {

        }

        public ContextoUsuarioDto ObtenerContextoUsuarioWeb(string userId)
        {
            var dispositivos = new List<DispositivoDto>();
            List<CercoVirtualRegistroDto> CercosVirtuales = new List<CercoVirtualRegistroDto>();
            Dictionary<string, string> dNombrexDispositivoId = new Dictionary<string, string>();

            try
            {
                using var conn = new NpgsqlConnection(_connectionSQLString);
                conn.Open();

                const string sql = @"SELECT dxu_numer AS ""Id"", dxu_alias AS Nombre, dxu_dispositivoid AS dispositivoId,
                    dxu_activo AS Activo, user_registroid as registroid, u.ultima_conexion as ultima_conexion 
                    FROM dispositivos_por_usuario
                    LEFT JOIN usuarios ON dxu_registroid = user_registroid
                    LEFT JOIN
                        (
                            SELECT ""ubi_registroID"", ""ubi_dispositivoID"", MAX(ubi_timestamp) AS ultima_conexion
                            FROM ubicacion
                            GROUP BY ""ubi_registroID"", ""ubi_dispositivoID""
                        ) u 
                        ON u.""ubi_registroID"" = dxu_registroid AND u.""ubi_dispositivoID"" = dxu_dispositivoid
                    WHERE user_auth0id = @auth0id
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("auth0id", NpgsqlDbType.Varchar, userId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // dxu_numer se mapea a Id
                        int idxId = reader.GetOrdinal("Id");
                        int idxNombre = reader.GetOrdinal("Nombre");
                        int idxActivox = reader.GetOrdinal("Activo");
                        int idxDispIdx = reader.GetOrdinal("dispositivoId");
                        int idxRegIdx = reader.GetOrdinal("registroId");
                        int idxUltConx = reader.GetOrdinal("ultima_conexion");


                        int id = reader.IsDBNull(idxId) ? 0 : reader.GetInt32(idxId);
                        string nombre = reader.IsDBNull(idxNombre) ? string.Empty : reader.GetString(idxNombre);
                        bool activo = reader.IsDBNull(idxActivox) ? false : reader.GetBoolean(idxActivox);

                        string dispositivoID = string.Empty;
                        if (!reader.IsDBNull(idxDispIdx))
                        {
                            dispositivoID = reader.GetGuid(idxDispIdx).ToString();
                        }

                        string registroID = string.Empty;
                        if (!reader.IsDBNull(idxRegIdx))
                        {
                            registroID = reader.GetGuid(idxRegIdx).ToString();
                        }

                        DateTime ultimaConexion = DateTime.MinValue;
                        if (!reader.IsDBNull(idxUltConx))
                        {
                            var dto = reader.GetFieldValue<DateTimeOffset>(idxUltConx);
                            ultimaConexion = dto.UtcDateTime;
                        }


                        dNombrexDispositivoId.Add(dispositivoID, nombre);

                        dispositivos.Add(new DispositivoDto
                        {
                            Nombre = nombre,
                            Id = id,
                            Estado = activo ? "Persecucion" : "Inactivo",
                            Activo = activo,
                            UltimaConexion = ultimaConexion == DateTime.MinValue ? string.Empty : ultimaConexion.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                }

                var dispositivosGuids = dNombrexDispositivoId.Where(s => !string.IsNullOrWhiteSpace(s.Key)).Select(s => Guid.Parse(s.Key)).ToArray();

                if (dispositivosGuids.Length > 0)
                {
                    // Consulto los cercos virtuales asociados a los dispositivos
                    const string sqlCercos = @"
                    SELECT  cv.cerco_id as cerco_id, cv.cerco_nombre as cerco_nombre,
                            ST_AsGeoJSON(cv.cerco_geom_4326) AS geom_geojson, cv.cerco_activo as activo,
                            dc.dc_disp_id as dispositivo_id, dc.dc_tipo_alerta as tipo_alerta       
                    FROM cercos_virtuales cv
                    JOIN dispositivo_cerco dc ON cv.cerco_id = dc.dc_cerco_id
                    WHERE dc.dc_disp_id = ANY(@dispositivos)
                    ";

                    using var cmdC = new NpgsqlCommand(sqlCercos, conn);
                    cmdC.Parameters.AddWithValue("dispositivos", NpgsqlDbType.Array | NpgsqlDbType.Uuid, dispositivosGuids);

                    using var readerC = cmdC.ExecuteReader();
                    int idxCercoId = -1, idxCercoNombre = -1, idxGeom = -1, idxActivo = -1, idxDispId = -1, idxTipoAlerta = -1;
                    if (readerC.HasRows)
                    {
                        idxCercoId = readerC.GetOrdinal("cerco_id");
                        idxCercoNombre = readerC.GetOrdinal("cerco_nombre");
                        idxGeom = readerC.GetOrdinal("geom_geojson");
                        idxActivo = readerC.GetOrdinal("activo");
                        idxDispId = readerC.GetOrdinal("dispositivo_id");
                        idxTipoAlerta = readerC.GetOrdinal("tipo_alerta");
                    }

                    while (readerC.Read())
                    {
                        if (!readerC.IsDBNull(idxCercoId))
                        {
                            try
                            {
                                string dispositivoIdStr = readerC.IsDBNull(idxDispId)
                                    ? string.Empty
                                    : readerC.GetFieldValue<Guid>(idxDispId).ToString();
                                short tipoAlerta = readerC.IsDBNull(idxTipoAlerta) ? (short)0 : readerC.GetInt16(idxTipoAlerta);

                                int cercoId = readerC.GetInt32(idxCercoId);
                                string cercoNombre = readerC.IsDBNull(idxCercoNombre) ? string.Empty : readerC.GetString(idxCercoNombre);
                                bool activoCerco = !readerC.IsDBNull(idxActivo) && readerC.GetBoolean(idxActivo);
                                string geomGeoJson = readerC.IsDBNull(idxGeom) ? string.Empty : readerC.GetString(idxGeom);

                                CercoVirtualRegistroDto cerco = new CercoVirtualRegistroDto
                                {
                                    Id = cercoId,
                                    Nombre = cercoNombre,
                                    Activo = activoCerco,
                                    DispositivoId = dispositivoIdStr,
                                    DispositivoNombre = dNombrexDispositivoId.FirstOrDefault(s => s.Key == dispositivoIdStr).Value ?? string.Empty,
                                    GeoJsonCerco4326 = geomGeoJson
                                };
                                CercosVirtuales.Add(cerco);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error al procesar cerco virtual: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {

                }
            }
            catch (NpgsqlException ex)
            {
                // En caso de error en la BDD, devolvemos un contexto con la información mínima y dejamos logueo a quien invoque
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                return new ContextoUsuarioDto
                {
                    Auth0UserId = userId,
                    Nombre = "EjemploUsuario",
                    Email = "ejemplo@usuario.com",
                    TelegramChatId = "632880473",
                    Telegram = "@Nolosetrik",
                    Dispositivos = dispositivos,
                    FechaCargaUtc = DateTime.UtcNow,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new ContextoUsuarioDto
                {
                    Auth0UserId = userId,
                    Nombre = "EjemploUsuario",
                    Email = "ejemplo@usuario.com",
                    TelegramChatId = "632880473",
                    Telegram = "@Nolosetrik",
                    Dispositivos = dispositivos,
                    FechaCargaUtc = DateTime.UtcNow
                };
            }

            // Construyo el contexto combinando los datos de la consulta con los valores hardcodeados restantes
            return new ContextoUsuarioDto
            {
                Auth0UserId = userId,
                Nombre = "EjemploUsuario",
                Email = "ejemplo@usuario.com",
                TelegramChatId = "632880473",
                Telegram = "@Nolosetrik",
                Dispositivos = dispositivos,
                FechaCargaUtc = DateTime.UtcNow,
                CercosVirtuales = CercosVirtuales
            };
        }

        public List<Ubicacion> ConsultarHistorialUbicacion(DTOHistorialUbicacion historialUbicacion)
        {
            List<Ubicacion> ubicaciones = new List<Ubicacion>();
            
            if (historialUbicacion == null)
                return ubicaciones;
            try
            {
                // Normalizar parámetros desde el DTO
                var auth0Id = historialUbicacion.Auth0UserId ?? string.Empty;
                var fechaDesde = historialUbicacion.FechaInicio == default? DateTime.MinValue : historialUbicacion.FechaInicio.ToUniversalTime();
                var fechaHasta = historialUbicacion.FechaFin == default ? DateTime.UtcNow : historialUbicacion.FechaFin.ToUniversalTime();

                // Intentamos convertir la lista de dispositivos a int[] independientemente del tipo original
                if (historialUbicacion.Dispositivos == null || historialUbicacion.Dispositivos.Count == 0)
                    return ubicaciones;

                int[] dispositivoIds = historialUbicacion.Dispositivos
                    .Select(d => Convert.ToInt32(d.Id))
                    .ToArray();

                using var conn = new NpgsqlConnection(_connectionSQLString);
                conn.Open();

                // Seleccionamos lon/lat con ST_X/ST_Y para evitar dependencia a NetTopologySuite aquí
                const string sql = @"
                    SELECT dxu.dxu_alias,
                           u.""ubi_dispositivoID"",
                           ST_X(u.ubi_coordenadas) AS lon,
                           ST_Y(u.ubi_coordenadas) AS lat,
                           u.ubi_timestamp
                    FROM ubicacion u
                    LEFT JOIN usuarios us ON u.""ubi_registroID"" = us.user_registroid
                    LEFT JOIN dispositivos_por_usuario dxu ON u.""ubi_registroID"" = dxu.dxu_registroid
                    WHERE us.user_auth0id = @auth0id
                      AND dxu.dxu_numer = ANY(@dispositivos)
                      AND u.ubi_timestamp BETWEEN @fechaDesde AND @fechaHasta
                      AND u.ubi_coordenadas IS NOT NULL
                      AND NOT (ST_X(u.ubi_coordenadas) = 0 AND ST_Y(u.ubi_coordenadas) = 0)
                    ORDER BY u.ubi_timestamp ASC;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("auth0id", NpgsqlDbType.Varchar, auth0Id);
                cmd.Parameters.AddWithValue("fechaDesde", NpgsqlDbType.TimestampTz, fechaDesde);
                cmd.Parameters.AddWithValue("fechaHasta", NpgsqlDbType.TimestampTz, fechaHasta);
                cmd.Parameters.AddWithValue("dispositivos", NpgsqlDbType.Array | NpgsqlDbType.Integer, dispositivoIds);

                using var reader = cmd.ExecuteReader();
                int idxAlias = reader.GetOrdinal("dxu_alias");
                int idxDispId = reader.GetOrdinal("ubi_dispositivoID");
                int idxLon = reader.GetOrdinal("lon");
                int idxLat = reader.GetOrdinal("lat");
                int idxTimestamp = reader.GetOrdinal("ubi_timestamp");

                while (reader.Read())
                {
                    var ubic = new Ubicacion();

                    if (!reader.IsDBNull(idxDispId))
                    {
                        try
                        {
                            var dispGuid = reader.GetFieldValue<Guid>(idxDispId);
                            ubic.DispositivoID = dispGuid.ToString();
                        }
                        catch (InvalidCastException)
                        {
                            ubic.DispositivoID = reader.GetString(idxDispId);
                        }
                    }

                    if (!reader.IsDBNull(idxAlias))
                        ubic.AliasDispositivo = reader.GetString(idxAlias);

                    if (!reader.IsDBNull(idxLat))
                        ubic.Latitud = reader.GetDouble(idxLat);

                    if (!reader.IsDBNull(idxLon))
                        ubic.Longitud = reader.GetDouble(idxLon);

                    if (!reader.IsDBNull(idxTimestamp))
                    {
                        var dto = reader.GetFieldValue<DateTimeOffset>(idxTimestamp);
                        ubic.Timestamp = dto.UtcDateTime;
                    }

                    // Otros campos del DTO Ubicacion quedan con sus valores por defecto o pueden mapearse si se requiere
                    ubicaciones.Add(ubic);
                }
                return ubicaciones;
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Error de PostgreSQL: {ex.Message}");
                return new List<Ubicacion>(); // Devuelve lista vacía en caso de error
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new List<Ubicacion>(); // Devuelve lista vacía en caso de error
            }

            /*
            // devolveremos una lista de ubicaciones de ejemplo
            return new List<Ubicacion>
            {
                new Ubicacion
                {
                    IDAgente = "Agente1",
                    DispositivoID = "Dispositivo1",
                    Latitud = -34.6037,
                    Longitud = -58.3816,
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    Hdop = 0.8,
                    Altitud = 25.0,
                    SigmaPosicion = 5.0
                },
                new Ubicacion
                {
                    IDAgente = "Agente1",
                    DispositivoID = "Dispositivo1",
                    Latitud = -34.6040,
                    Longitud = -58.3820,
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    Hdop = 0.7,
                    Altitud = 26.0,
                    SigmaPosicion = 4.5
                }
            };
            */
        }
    }
}
