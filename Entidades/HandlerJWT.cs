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

namespace Entidades
{

    public class HandlerJWT
    {
        private readonly string _secret;
        private CryptoHandler _crypto;
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
                    payloadDatos = JsonConvert.SerializeObject(diccionarioClaims, Formatting.Indented);
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

        private eEstadoJWT ValidarPrecondicionesMetadata(Dictionary<string, object> diccionarioClaims)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Separa los claims de metadata y AEAD del payload principal
        /// Devuelve el payload canonico y en el parametro data quedan los claims de metadata y AEAD
        /// </summary>
        /// <param name="data"></param>
        /// <param name="claimsToRemove"></param>
        /// <returns></returns>
        public Dictionary<string, object> ExtraerMetadata(ref Dictionary<string, object> data)
        {
            // TODO: almacenar en un .config los claims de metadata y AEAD
            var claimsMetadata = new List<string> { "iss", "aud", "iat", "nbf", "exp", "jti", "seq", "ts", "d", "prev", "curr", "aad_sha256", "frame_kid" };

            ExtraerClaims(ref data, claimsMetadata, out var claimsExtraidos);

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
