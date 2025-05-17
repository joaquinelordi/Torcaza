using System;
using System.Collections.Generic;
using JWT;
using JWT.Algorithms;
using JWT.Exceptions;
using JWT.Serializers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Entidades
{

    public class HandlerJWT
    {
        private readonly string _secret;

        public HandlerJWT(IConfiguration configuration)
        {
            _secret = configuration["JwtSettings:Secret"];
            if (string.IsNullOrEmpty(_secret))
            {
                Console.WriteLine("El secret no está configurado en appsettings.json");
            }
            _secret = "a-string-secret-at-least-256-bits-long";
        }


        public string CrearToken(Dictionary<string, object> payload, int minutosExpiracion = 60)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            payload["iat"] = now;
            payload["exp"] = now + minutosExpiracion * 60;

            IJwtAlgorithm algorithm = new HMACSHA256Algorithm(); // HS256
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtEncoder encoder = new JwtEncoder(algorithm, serializer, urlEncoder);

            return encoder.Encode(payload, _secret);
        }

        public string LeerPayload(string token)
        {
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtValidator validator = new JwtValidator(serializer, new UtcDateTimeProvider());
            IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, new HMACSHA256Algorithm());

            try
            {
                var data = decoder.DecodeToObject<Dictionary<string, object>>(token, _secret, verify: true);

                // Convertir el diccionario a un string JSON
                return JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (TokenExpiredException)
            {
                Console.WriteLine("Token expirado");
            }
            catch (SignatureVerificationException)
            {
                Console.WriteLine("Firma inválida");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al decodificar token: " + ex.Message);
            }

            return String.Empty;
        }

        public bool EsJWTValido(string buffer)
        {
            if (string.IsNullOrWhiteSpace(buffer))
                return false;

            // Un JWT debe tener exactamente dos puntos (.) separando las tres partes
            var partes = buffer.Split('.');
            if (partes.Length != 3)
                return false;

            // Validar que cada parte esté codificada en Base64Url
            return partes.All(EsBase64UrlValido);
        }
        private bool EsBase64UrlValido(string input)
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
    }
}
