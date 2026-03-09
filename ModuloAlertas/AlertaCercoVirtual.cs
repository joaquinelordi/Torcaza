using Entidades;
using Entidades.Interfaces;
using NLog;
using Npgsql;

namespace ModuloAlertas
{
    public class AlertaCercoVirtual : IModuloAlertas
    {
        string _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";
        Logger _logger = LogManager.GetCurrentClassLogger();
        Notificador _notificador;


        public AlertaCercoVirtual()
        {
            _notificador = new Notificador(new List<INotificador>());
        }

        public AlertaCercoVirtual(string connectionString, Notificador notificador)
        {
            _connectionString = connectionString;
            _notificador = notificador;
        }
        private enum eEstadoAlertaCercoVirtual
        {
            Inactiva = 0,
            EnEspera = 1,
            Notificada = 2,
            Desactivada = 3,
            EnProceso = 4,
            Activada = 5
        }



        public void ProcesarAlerta(int alertaId, Ubicacion ubicacion)
        {
            bool bEncontroUsuarios = false;
            string cercoId = string.Empty;
            string registroId = string.Empty;
            string dispositivoId = string.Empty;
            string nombreUsuario = string.Empty;
            string emailUsuario = string.Empty;
            string chatIdTelegram = string.Empty;
            // consulta la base de datos para obtener los detalles de la alerta
            // y delega la obtencion de los usuarios a notificar, en la tabla alertas_geograficas tiene los datos para llegar a los contactos del usuario
            _logger.Info($"ProcesarAlerta -> Procesando alerta con ID: {alertaId}");
            
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"SELECT ag.alerta_id as alerta_id,
                                    ag.cerco_id as cerco_id,
                                    u.user_registroid as registro_id,
                                    dxu.dxu_dispositivoid as dispositivo_id,
                                    u.user_nombre as user_nombre,
                                    du.user_email as user_email,
                                    ut.user_chatidtelegram as user_chatidtelegram
                                FROM alertas_geograficas as ag
                                LEFT JOIN usuarios as u ON ag.ubi_registro_id = u.user_registroid
                                LEFT JOIN dispositivos_por_usuario as dxu ON u.user_registroid = dxu.dxu_registroid
                                LEFT JOIN datos_usuario as du ON u.user_registroid = du.user_id
                                LEFT JOIN usuario_telegram as ut ON u.user_registroid = ut.user_id
                                WHERE   u.user_activo = TRUE
                                AND dxu.dxu_activo = TRUE
                                AND ut.user_activo = TRUE
                                AND ut.user_chatidtelegram IS NOT NULL
                                AND alerta_id = @alertaId;
                    ";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@alertaId", alertaId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // datos del usuario para notificar
                                cercoId = reader.GetInt32(reader.GetOrdinal("cerco_id")).ToString();
                                registroId = reader.GetGuid(reader.GetOrdinal("registro_id")).ToString();
                                dispositivoId = reader.GetGuid(reader.GetOrdinal("dispositivo_id")).ToString();
                                nombreUsuario = reader.IsDBNull(reader.GetOrdinal("user_nombre")) ? "" : reader.GetString(reader.GetOrdinal("user_nombre"));
                                emailUsuario = reader.IsDBNull(reader.GetOrdinal("user_email")) ? "" : reader.GetString(reader.GetOrdinal("user_email"));
                                chatIdTelegram = reader.IsDBNull(reader.GetOrdinal("user_chatidtelegram")) ? "" : reader.GetInt64(reader.GetOrdinal("user_chatidtelegram")).ToString();

                                bEncontroUsuarios = true;

                                _logger.Info($"Notificando al usuario {nombreUsuario} (RegistroID: {registroId}, DispositivoID: {dispositivoId} CercoID: {cercoId}) sobre la alerta de cerco virtual con ID: {alertaId}.");
                            }
                            else
                            {
                                _logger.Warn($"No se encontraron usuarios para notificar para la alerta con ID: {alertaId}.");
                            }
                        }
                    }
                }

                if (bEncontroUsuarios)
                {
                    // si hay usuarios para notificar, se envia la notificacion
                    NotificacionDTO notificacion = new NotificacionDTO();
                    notificacion.Mensaje = $"Alerta de Cerco Virtual Activada.\nDispositivo: Dispositivo01 \n Ubicación: Lat {ubicacion.Latitud}, Lon {ubicacion.Longitud} \n Timestamp: {ubicacion.Timestamp} \n Alerta ID: {alertaId}";

                    notificacion.DatosAdicionales = new Dictionary<string, object>
                    {
                                { "cercoId", cercoId },
                                { "registroId", registroId },
                                { "dispositivoId", dispositivoId },
                                { "nombreUsuario",  nombreUsuario },
                                { "emailUsuario", emailUsuario },
                                { "chatIdTelegram", chatIdTelegram },
                                { "latitud", ubicacion.Latitud },
                                { "longitud", ubicacion.Longitud },
                                { "nombreDispositivo", "Dispositivo01" }
                    };

                    _notificador.EnviarNotificacion(notificacion);
                }

            }
            catch (Exception ex)
            {
                _logger.Error("Error al procesar la alerta: " + ex.Message);
            }

        }

        /// <summary>
        /// Procesa un evento de carga de ubicacion, identificado por su número de evento y verifica si algun dispositivo genera una alerta de cerco virtual
        /// </summary>
        /// <param name="numeroEvento"></param>
        public void ProcesarEvento(long numeroEvento)
        {
            Ubicacion ubicacion = new Ubicacion();
            List<(int, int, DateTime)> lCercos = new List<(int, int, DateTime)>();

            ubicacion = ObtenerUbicacionDesdeEvento(numeroEvento);

            if (ubicacion != null)
            {

                //Si lo pude cargar chequeo el tema de alertas
                if (ActualizarEstadoSeguimiento(ubicacion, ref lCercos))
                {

                    _logger.Debug($"ProcesarEvento -> Estado de seguimiento actualizado correctamente.");
                }
                else
                {
                    _logger.Warn($"ProcesarEvento -> No se pudo actualizar el estado de seguimiento.");
                }
            }
            else
            {
                _logger.Warn($"ProcesarEvento -> No se pudo obtener ubicacion para numeroEvento[{0}].", numeroEvento);

            }
        }

        /// <summary>
        /// Obtiene la ubicacion asociada a un evento de carga de ubicacion
        /// </summary>
        /// <param name="nroEvento"></param>
        /// <returns></returns>
        private Ubicacion ObtenerUbicacionDesdeEvento(long nroEvento)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                    SELECT ""ubi_registroID"", ""ubi_dispositivoID"", ST_Y(ubi_coordenadas) AS latitud, ST_X(ubi_coordenadas) AS longitud, ubi_timestamp
                    FROM ubicacion
                    WHERE ubi_nroevento = @nroEvento;
                    ";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@nroEvento", nroEvento);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string registroID = reader.GetGuid(reader.GetOrdinal("ubi_registroID")).ToString();
                                string dispositivoID = reader.GetGuid(reader.GetOrdinal("ubi_dispositivoID")).ToString();
                                double latitud = reader.GetDouble(reader.GetOrdinal("latitud"));
                                double longitud = reader.GetDouble(reader.GetOrdinal("longitud"));
                                DateTime timestamp = reader.GetDateTime(reader.GetOrdinal("ubi_timestamp"));
                                return new Ubicacion
                                {
                                    Latitud = latitud,
                                    Longitud = longitud,
                                    RegistroID = registroID,
                                    DispositivoID = dispositivoID,
                                    Timestamp = timestamp,
                                    NumeroEvento = nroEvento
                                };
                            }
                            else
                            {
                                _logger.Warn($"No se encontró la ubicación para el evento número {nroEvento}.");
                                return null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error al obtener la ubicación desde el evento: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Actualiza el estado de seguimiento de un dispositivo en base a su ubicacion y devuelve los cercos virtuales que intersecta
        /// </summary>
        /// <param name="ubicacion"></param>
        /// <param name="lCercoID"></param>
        /// <returns></returns>
        public bool ActualizarEstadoSeguimiento(Ubicacion ubicacion, ref List<(int cercoId, int tipoAlerta, DateTime fechaCreacion)> lCercos)
        {
            bool bRet = false;
            int alertaId;

            BuscarCercosAsignados(ubicacion, ref lCercos);

            if (lCercos.Count > 0)
            {
                foreach (var cerco in lCercos)
                {
                    if (UbicacionCumpleCondicion(ubicacion, cerco))
                    {
                        // Generar alerta
                        alertaId = GenerarAlertaGeografica(ubicacion, cerco);
                        ProcesarAlerta(alertaId, ubicacion);

                        _logger.Info($"El dispositivo con RegistroID {ubicacion.RegistroID} ha ingresado al cerco virtual con ID {cerco.cercoId} creado el {cerco.fechaCreacion}.");
                        
                    }
                    _logger.Info($"El dispositivo con RegistroID {ubicacion.RegistroID} cumple condicion del cerco virtual con ID {cerco.cercoId} creado el {cerco.fechaCreacion}.");
                }


                bRet = true;
            }


            return bRet;
        }

        private int GenerarAlertaGeografica(Ubicacion ubicacion, (int cercoId, int tipoAlerta, DateTime fechaCreacion) cerco)
        {
            int alertaId = -1;

            // hace un update de la tabla alertas_geograficas con los datos de la alerta
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                    INSERT INTO alertas_geograficas (ubi_registro_id, ubi_dispositivo_id, ubi_timestamp, cerco_id, tipo_alerta, alerta_generada, alerta_estado)
                    VALUES (@registroID, @dispositivoID, @timestamp, @cercoID, @tipoAlerta, @timestamp_alerta, @alerta_estado)
                    RETURNING alerta_id;
                ";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@registroID", Guid.Parse(ubicacion.RegistroID));
                        cmd.Parameters.AddWithValue("@dispositivoID", Guid.Parse(ubicacion.DispositivoID));
                        cmd.Parameters.AddWithValue("@timestamp", ubicacion.Timestamp);
                        cmd.Parameters.AddWithValue("@cercoID", cerco.cercoId);
                        cmd.Parameters.AddWithValue("@tipoAlerta", cerco.tipoAlerta);
                        cmd.Parameters.AddWithValue("@timestamp_alerta", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@alerta_estado", (int)eEstadoAlertaCercoVirtual.Activada);
                        
                        var result = cmd.ExecuteScalar();
                        if (result != null && int.TryParse(result.ToString(), out int id))
                        {
                            alertaId = id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error al generar la alerta geográfica: " + ex.Message);
            }

            return alertaId;
        }

        /// <summary>
        /// Verifica si la ubicacion cumple la condicion del cerco virtual (dentro o fuera del cerco) segun el tipo de alerta
        /// </summary>
        /// <param name="ubicacion"></param>
        /// <param name="cerco"></param>
        /// <returns></returns>
        private bool UbicacionCumpleCondicion(Ubicacion ubicacion, (int cercoId, int tipoAlerta, DateTime fechaCreacion) cerco)
        {
            bool bRet = false;
            try
            {
                // Busco el cerco virtual en la base de datos y verifico si la ubicacion cumple la condicion
                // segun tipo alerta (dentro o fuera del cerco)
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                    SELECT ST_Contains(
                        cerco_geom_22185,
                        ST_Transform(
                            ST_SetSRID(ST_MakePoint(@longitud, @latitud), 4326),
                            22185
                        )
                    ) AS dentro_cerco
                    FROM cercos_virtuales
                    WHERE cerco_id = @cercoId;
                ";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@longitud", ubicacion.Longitud);
                        cmd.Parameters.AddWithValue("@latitud", ubicacion.Latitud);
                        cmd.Parameters.AddWithValue("@cercoId", cerco.cercoId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // verifico condicion segun tipo de alerta
                                bool dentroCerco = reader.GetBoolean(reader.GetOrdinal("dentro_cerco"));
                                // TODO: int provisorio, convertir a enum cuando se busca cercoId en la base
                                // Tipo de alerta: 0 = entrada, 1 = salida
                                if ((cerco.tipoAlerta == 0 && dentroCerco) || (cerco.tipoAlerta == 1 && !dentroCerco))
                                {
                                    bRet = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error al verificar la condición del cerco virtual: " + ex.Message);
            }
            return bRet;
        }



        /// <summary>
        /// Busca los cercos virtuales asignados, comparten registroID y estos cercos deben estar activos para ese usuario
        /// </summary>
        /// <param name="ubicacion"></param>
        /// <param name="lCercoID"></param>
        public void BuscarCercosAsignados(Ubicacion ubicacion, ref List<(int cercoId, int tipoAlerta, DateTime fechaCreacion)> lCerco)
        {
            // Cambiar de dispositivo id a registro id con una tabla intermedia que vincule registro y dispositivos asociados
            //Guid registroId = Guid.Parse(ubicacion.RegistroID);
            Guid dispositivoID = Guid.Parse(ubicacion.DispositivoID);
            lCerco = new List<(int cercoId, int tipoAlerta, DateTime fechaCreacion)>() { };
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    // Consulta SQL para obtener los cercos virtuales asignados al registroId
                    string sql = @"
                            SELECT cerco_id, cerco_creado, dc_tipo_alerta
                            FROM cercos_virtuales as cv
                            LEFT JOIN dispositivo_cerco as dc ON cv.cerco_id = dc.dc_cerco_id
                            WHERE dc.dc_disp_id = @dispositivoID
                                AND cerco_activo = TRUE
                    ";

                    object result;
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@dispositivoID", dispositivoID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            int idxCercoId = reader.GetOrdinal("cerco_id");
                            int idxCercoCreado = reader.GetOrdinal("cerco_creado");
                            int idxTipoAlerta = reader.GetOrdinal("dc_tipo_alerta");
                            DateTime fechaCreacion;
                            int cercoId;
                            int tipoAlerta;

                            while (reader.Read())
                            {
                                // Verificar si el valor no es nulo antes de leerlo, si lo es no agrego fila porque es un error de inconsistencia en BDD
                                if (!reader.IsDBNull(idxCercoId))
                                {
                                    cercoId = reader.GetInt32(idxCercoId);
                                    fechaCreacion = reader.IsDBNull(idxCercoCreado) ? DateTime.MinValue : reader.GetDateTime(idxCercoCreado);
                                    tipoAlerta = reader.IsDBNull(idxTipoAlerta) ? 0 : reader.GetInt32(idxTipoAlerta);

                                    lCerco.Add((cercoId, tipoAlerta, fechaCreacion));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error al consultar la base de datos: " + ex.Message);
                lCerco.Clear();
            }
        }


        /* string sql = @"
                    SELECT cerco_id, cerco_creado
                    FROM cercos_virtuales
                    WHERE ST_Contains(
                        cerco_geom_22185,
                        ST_Transform(
                            ST_SetSRID(ST_MakePoint(@longitud, @latitud), 4326),
                            22185
                        )
                        AND cerco_activo = TRUE
                    );
                ";
        */
    }
}
