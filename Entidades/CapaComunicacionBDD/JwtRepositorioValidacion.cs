using Entidades.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Npgsql;


namespace Entidades.CapaComunicacionBDD
{
    public class JwtRepositorioValidacion : IjwtRepositorio
    {
        private readonly string _connectionString;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly List<string> _claimsMetadata;
        public JwtRepositorioValidacion(List<string> claimsMetadata)
        {
            _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";
            _claimsMetadata = claimsMetadata;
        }

        public bool ExisteIss(string iss)
        {
            bool bRet = true;
            object result = null;

            try
            {
                using var conexion = new NpgsqlConnection(_connectionString);

                conexion.Open();

                using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM NOMBRE_TABLA WHERE disp_iss = @iss", conexion);

                cmd.Parameters.AddWithValue("iss", iss);
                result = cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al verificar la existencia de iss: {ex.Message}");
                bRet = false;
            }
            if (result is not null && Convert.ToInt64(result) == 0)
                bRet = false;

            return bRet;
        }

        public string? ObtenerPrevHashJWT(string iss, long seq)
        {
            object? result = null;
            try
            {
                using var conexion = new NpgsqlConnection(_connectionString);
                conexion.Open();
                using var cmd = new NpgsqlCommand("SELECT prev_hash FROM NOMBRE_TABLA WHERE iss = @issuer AND seq = @seq", conexion);
                cmd.Parameters.AddWithValue("iss", iss);
                cmd.Parameters.AddWithValue("seq", seq);
                result = cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al obtener el prev_hash: {ex.Message}");
                return null;
            }
            return result != DBNull.Value ? result.ToString() : null;
        }

        public long? ObtenerUltimoSeq(string iss)
        {
            object? result = null;
            try
            {
                using var conexion = new NpgsqlConnection(_connectionString);
                conexion.Open();
                using var cmd = new NpgsqlCommand("SELECT MAX(seq) FROM NOMBRE_TABLA WHERE disp_iss = @iss", conexion);
                cmd.Parameters.AddWithValue("iss", iss);
                result = cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al obtener el último seq: {ex.Message}");
                return null;
            }

            return result != DBNull.Value && result != null ? Convert.ToInt64(result) : null;
        }
        /// <summary>
        /// Verifica las precondiciones para aceptar un nuevo JWT de los dispositivos
        /// </summary>
        /// <param name="iss"></param>
        /// <param name="seq"></param>
        /// <param name="prevHash"></param>
        /// <returns></returns>
        public bool CumplePrecondiciones(Dictionary<string, object> diccionarioClaims)
        {
            try
            {
                if (!ContieneClaimsMetadataObligatorios(diccionarioClaims))
                {
                    _logger.Warn("Faltan claims de metadata obligatorios.");
                    return false;
                }
                // Extraer los valores necesarios del diccionario de claims
                string iss = diccionarioClaims["iss"].ToString() ?? string.Empty;
                long seq = Convert.ToInt64(diccionarioClaims["seq"]);
                string prevHash = diccionarioClaims["prev"].ToString() ?? string.Empty;
                string currHash = diccionarioClaims["curr"].ToString() ?? string.Empty;
                // Verificar si el 'iss' del dispositivo existe
                if (!ExisteIss(iss))
                {
                    _logger.Warn($"El 'iss' {iss} no existe en la base de datos.");
                    return false;
                }
                // Obtener el último 'seq' registrado para el 'iss'
                long? ultimoSeq = ObtenerUltimoSeq(iss);
                if (ultimoSeq.HasValue)
                {
                    if (seq != ultimoSeq.Value + 1)
                    {
                        _logger.Warn($"El 'seq' {seq} no es consecutivo. Último 'seq' registrado: {ultimoSeq.Value}");
                        return false;
                    }
                }
                else
                {
                    // Si no hay registros previos, el 'seq' debe ser 1
                    if (seq != 1)
                    {
                        _logger.Warn($"El primer 'seq' debe ser 1. Se recibió: {seq}");
                        return false;
                    }
                }
                // Verificar el 'prev_hash'
                if (ultimoSeq.HasValue && ultimoSeq.Value > 0)
                {
                    string? hashEsperado = ObtenerPrevHashJWT(iss, ultimoSeq.Value);
                    if (hashEsperado == null || hashEsperado != prevHash)
                    {
                        _logger.Warn($"El 'prev_hash' no coincide. Esperado: {hashEsperado}, Recibido: {prevHash}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al verificar las precondiciones: {ex.Message}");
                return false;
            }
            // Todas las precondiciones se cumplen
            return true;
        }

        private bool ContieneClaimsMetadataObligatorios(Dictionary<string, object> diccionarioClaims)
        {
            List<string> claimsObligatorios = new List<string> { "iss", "seq", "d", "prev", "curr" };

            // Chequeo de que el diccionario contenga todos los claims obligatorios y que no sean nulos
            return claimsObligatorios.All(claim => diccionarioClaims.ContainsKey(claim) && diccionarioClaims[claim] != null);
        }
    }
}
