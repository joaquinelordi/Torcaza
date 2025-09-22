using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using NLog;
using Entidades.CapaComunicacionBDD;
using System.Text.Json;
using System.Buffers;

namespace Entidades
{

    public class HandlerJWT
    {
        private readonly string _secret;
        private CryptoHandler _crypto;
        private JwtRepositorioValidacion _jwtRepositorioValidacion;
        private readonly List<string> _claimsMetadata; 
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public HandlerJWT(IConfiguration configuration)
        {
            //valido que la clave y el id del dispositivo esten en la base de datos nada mas
            _secret = configuration["JwtSettings:Secret"];
            string key = configuration["JwtSettings:PrivateKey"];
            _crypto = new CryptoHandler(key);
            if (string.IsNullOrEmpty(_secret))
            {
                _secret = "";
                Console.WriteLine("El secret no está configurado en secrets.json");
            }
            _claimsMetadata = new List<string> { "iss", "aud", "iat", "exp", "jti", "seq", "d", "prev", "curr" };
            _jwtRepositorioValidacion = new JwtRepositorioValidacion(GetClaimsMetadata());

        }

        private List<string> GetClaimsMetadata()
        {
            return _claimsMetadata;
        }

        /// <summary>
        /// Crea un token JWT con el payload especificado y tiempo de expiracion en minutos
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="minutosExpiracion"></param>
        /// <returns></returns>
        public string CrearToken(Dictionary<string, object> payload, int? minutosExpiracion = null , IJwtAlgorithm? algorithm = null)
        {
            algorithm ??= new HMACSHA256Algorithm(); // HS256 por defecto

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            payload["iat"] = now;
            
            if (minutosExpiracion.HasValue)
                payload["exp"] = now + (long)minutosExpiracion.Value * 60;

            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithm, serializer, urlEncoder);

            return encoder.Encode(payload, _secret);
        }

        /// <summary>
        /// Lee los campos de payload de del JWT, valido y proeso los de metadata y AEAD
        /// y si corresponde procedo con el payload canonico de telemetria
        /// </summary>
        /// <param name="token"></param>
        /// <param name="algorithm"></param>
        /// <returns></returns>
        public eEstadoJWT ProcesarPayloadCompleto(string token,ref string payloadDatos, IJwtAlgorithm? algorithm = null)
        {
            eEstadoJWT estado;
            algorithm ??= new HMACSHA256Algorithm(); // HS256 por defecto
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtValidator validator = new JwtValidator(serializer, new UtcDateTimeProvider());
            IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, algorithm);

            try
            {
                var data = decoder.DecodeToObject<Dictionary<string, object>>(token, _secret, verify: true);

                //TODO: Eliminar claims de metadata y AEAD
                var diccionarioClaims = ExtraerMetadata(ref data);
                estado = ValidarPrecondicionesMetadata(diccionarioClaims);
                if (estado == eEstadoJWT.OK)
                {
                    // Convertir el diccionario con el JWT de datos a un string JSON
                    payloadDatos = JsonConvert.SerializeObject(data, Formatting.Indented);
                }
                else
                {
                    _logger.Warn("Error en la validación de metadata/AEAD: {0}", estado.ToString());
                }

                return estado;
            }
            catch (TokenExpiredException)
            {
                _logger.Error("Token expirado");
                return eEstadoJWT.TokenExpirado;
            }
            catch (SignatureVerificationException)
            {
                _logger.Error("Firma inválida");
                return eEstadoJWT.FirmaInvalida;
            }
            catch (Exception ex)
            {
                _logger.Error("Error al decodificar token: " + ex.Message);
                return eEstadoJWT.ErrorDecodificacion;
            }
        }
        /// <summary>
        /// Valida las precondiciones de los claims de metadata y AEAD
        /// </summary>
        /// <param name="diccionarioClaims"></param>
        /// <returns></returns>
        private eEstadoJWT ValidarPrecondicionesMetadata(Dictionary<string, object> diccionarioClaims)
        {
            eEstadoJWT estado = eEstadoJWT.OK;
            //TODO: quitar return temporal luego de activar la validacion y crear las tablas correspondientes en BDD 
            return estado;
            // Verifica que los claims obligatorios estén presentes
            //Identifica identidad del emisor
            if(!_jwtRepositorioValidacion.CumplePrecondiciones(diccionarioClaims))
            {
                estado = eEstadoJWT.ErrorMetadata;
            }



            return estado;
        }

        /// <summary>
        /// Separa los claims de metadata y AEAD del payload principal
        /// Devuelve el payload canonico y en el parametro data quedan los claims de metadata y AEAD
        /// iss = identidad el emisor
        /// aud = url de destino, es para validar y descartar rapidamente
        /// iat = unix time emision, fecha de emision del jwt (distinta de fecha de obtencion de datos de telemetria)
        /// exp = unix time expiracion, fecha de expiracion del jwt (opcional)
        /// seq = numero de secuencia del mensaje, contador incremental de cada dispositivo
        /// d = SHA-256 del contenido canonizado (datos de tememetria como array de string)
        /// prev = hash encadenado anterior (H_100) del payload de jwt en formato json 
        /// curr = hash encadenado actual (H_101) del payload de jwt en formato json (todos los campos excepto curr)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="claimsToRemove"></param>
        /// <returns></returns>
        public Dictionary<string, object> ExtraerMetadata(ref Dictionary<string, object> data)
        {
            // TODO: almacenar en un .config los claims de metadata y AEAD
            ExtraerClaims(ref data, _claimsMetadata, out var claimsExtraidos);

            return claimsExtraidos;
        }

        /// <summary>
        /// Extrae los claims especificados del diccionario y los elimina del mismo.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="claimsAQuitar"></param>
        /// <param name="claimsExtraidos"></param>
        private void ExtraerClaims(ref Dictionary<string, object> data, List<string> claimsAQuitar, out Dictionary<string, object> claimsExtraidos)
        {
            claimsExtraidos = new Dictionary<string, object>();
            foreach (var claim in claimsAQuitar)
            {
                // Si el claim existe en el diccionario, se copia y se elimina del original
                if (data.ContainsKey(claim))
                {
                    claimsExtraidos[claim] = data[claim];
                    data.Remove(claim);
                }
            }
        }

        /// <summary>
        /// Valida si el string proporcionado es un JWT válido.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public bool StringEsJWTValido(string buffer)
        {
            if (string.IsNullOrWhiteSpace(buffer))
                return false;

            // Un JWT debe tener exactamente dos puntos (.) separando las tres partes
            var partes = buffer.Split('.');
            if (partes.Length != 3)
                return false;

            // Validar que cada parte esté codificada en Base64Url
            return partes.All(StringEsBase64UrlValido);
        }
        private bool StringEsBase64UrlValido(string input)
        {
            try
            {
                // Reemplazar caracteres específicos de Base64Url y validar la longitud
                string base64 = input.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                // Intentar decodificar
                Convert.FromBase64String(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Verifica las precondiciones de seguridad en el body recibido.
        /// TODO: usar enum para los errores
        /// </summary>
        /// <param name="frameBytes"></param>
        public bool VerificarPrecondiciones(string bodyJWT)
        {
            bool bRet = true;
            if (!StringEsJWTValido(bodyJWT))
            {
                _logger.Debug("El token no es un JWT válido.");
                bRet = false;
            }

            // Inspecciono los claim de metadata e AEAD


            return bRet;
        }

        public string Desencriptar(ref string sEncriptado)
        {
           return _crypto.DecryptAES(Convert.FromBase64String(sEncriptado));
        }

        public static string CalcularCurrHash(string jsonPayload)
        {
            string jsonSinCurrCanonico;

            using var doc = JsonDocument.Parse(jsonPayload, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });

            // 
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = true
            }))
            {
                WriteCanonicalValueExcludingCurr(writer, doc.RootElement);
            }

            // JSON canonizado SIN "curr" como string (útil para encadenamiento, logs, etc.)
            jsonSinCurrCanonico = Encoding.UTF8.GetString(buffer.WrittenSpan);
            _logger.Debug("JSON canonizado sin 'curr': {0}", jsonSinCurrCanonico);

            // Hash SHA-256 sobre esos mismos bytes canonizados
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(buffer.WrittenSpan, hash);

            // Convertir el hash a Base64Url
            string sHash = Convert.ToBase64String(hash.ToArray())
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return sHash;
        }

        // Escribe un valor JSON de forma canonizada, excluyendo la propiedad "curr" que se usa para comparar el resultado
        private static void WriteCanonicalValueExcludingCurr(Utf8JsonWriter writer, JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        writer.WriteStartObject();

                        foreach (var p in el.EnumerateObject()
                                            .Where(p => p.Name != "curr")
                                            .OrderBy(p => p.Name, StringComparer.Ordinal))
                        {
                            writer.WritePropertyName(p.Name);
                            WriteCanonicalValueExcludingCurr(writer, p.Value);
                        }

                        writer.WriteEndObject();
                        break;
                    }
                case JsonValueKind.Array:
                    {
                        writer.WriteStartArray();
                        foreach (var item in el.EnumerateArray())
                            WriteCanonicalValueExcludingCurr(writer, item);
                        writer.WriteEndArray();
                        break;
                    }
                case JsonValueKind.String:
                    writer.WriteStringValue(el.GetString());
                    break;

                case JsonValueKind.Number:
                    {
                        if (el.TryGetInt64(out long li))
                        {
                            writer.WriteRawValue(li.ToString(CultureInfo.InvariantCulture));
                        }
                        else if (el.TryGetDecimal(out decimal ld))
                        {
                            writer.WriteRawValue(ld.ToString("G", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            double d = el.GetDouble();
                            var s = d.ToString("R", CultureInfo.InvariantCulture);
                            s = NormalizeExponent(s);
                            writer.WriteRawValue(s);
                        }
                        break;
                    }

                case JsonValueKind.True: writer.WriteBooleanValue(true); break;
                case JsonValueKind.False: writer.WriteBooleanValue(false); break;
                case JsonValueKind.Null: writer.WriteNullValue(); break;

                default:
                    throw new NotSupportedException($"Tipo JSON no soportado: {el.ValueKind}");
            }
        }

        private static string NormalizeExponent(string s)
        {
            int idx = s.IndexOf('E');
            if (idx < 0) idx = s.IndexOf('e');
            if (idx < 0) return s;

            var pre = s[..idx];
            var exp = s[(idx + 1)..];
            if (exp.StartsWith("+")) exp = exp[1..];
            return pre + "e" + exp;
        }

    }

    public enum eEstadoJWT
    {
        OK,
        TokenExpirado,
        FirmaInvalida,
        ErrorDecodificacion,
        ErrorMetadata,
        ErrorAEAD
    }
}
