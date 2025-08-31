using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Passport;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Entidades
{
    public class CryptoHandler
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        string _privateKey;
        private static readonly byte[] _iv = new byte[16] {
            0x74, 0x11, 0xF0, 0x45, 0xD6, 0xA4, 0x3F, 0x69,
            0x18, 0xC6, 0x75, 0x42, 0xDF, 0x4C, 0xA7, 0x84
        };
        public CryptoHandler(string key)
        {
            _privateKey = key;
        }


        /// <summary>
        /// Encripta un string utilizando AES con una clave y un IV generados aleatoriamente.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="key"></param>
        /// <param name="iv"></param>
        /// <returns></returns>
        public byte[] EncryptAES( string body, out byte[] iv, string? key = null)
        {
            if (key == null)
                key = _privateKey;
            // Convertir strings en bytes
            var plainText = Encoding.UTF8.GetBytes(body);
            
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.GenerateIV();
            iv = aes.IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            return encryptor.TransformFinalBlock(plainText, 0, plainText.Length);
        }

        /// <summary>
        /// Desencripta un array de bytes utilizando AES con una clave y un IV proporcionados.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="key"></param>
        /// <param name="iv"></param>
        /// <returns></returns>
        public string DecryptAES(byte[] cipherText, byte[]? iv = null, string? key = null)
        {
            if (key == null)
                key = _privateKey;

            if (iv == null || iv.Length != 16)
                iv = _iv;

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public string firmar(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }
}
