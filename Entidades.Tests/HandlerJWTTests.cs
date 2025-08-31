using System.Collections.Generic;
using Entidades;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Entidades.Tests
{
    public class HandlerJWTTests
    {
        private static HandlerJWT CreateHandler()
        {
            var settings = new Dictionary<string, string>
        {
            {"JwtSettings:Secret", "prueba"}
        };
            IConfiguration configuration = new DictionaryConfiguration(settings);
            return new HandlerJWT(configuration);
        }

        [Fact]
        public void EsJWTValido_ConTokenGeneradoDevuelveTrue()
        {
            var handler = CreateHandler();
            var payload = new Dictionary<string, object>
        {
            {"str", "123"}
        };
            var token = handler.CrearToken(payload);
            Assert.True(handler.StringEsJWTValido(token));
        }

        [Theory]
        [InlineData("abc.def")]
        [InlineData("abc.def.uvw*")]
        public void EsJWTValido_TokenInvalidoDevuelveFalse(string token)
        {
            var handler = CreateHandler();
            Assert.False(handler.StringEsJWTValido(token));
        }
    }

    internal class DictionaryConfiguration : IConfiguration
    {
        private readonly IDictionary<string, string> _values;

        public DictionaryConfiguration(IDictionary<string, string>? initial = null)
        {
            _values = initial ?? new Dictionary<string, string>();
        }

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set
            {
                if (value != null)
                {
                    _values[key] = value;
                }
            }
        }

        public IEnumerable<IConfigurationSection> GetChildren() => System.Linq.Enumerable.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => NullChangeToken.Singleton;

        public IConfigurationSection GetSection(string key) => new DictionaryConfigurationSection(key, this[key]);

        private class DictionaryConfigurationSection : IConfigurationSection
        {
            private readonly string _key;
            private readonly string? _value;

            public DictionaryConfigurationSection(string key, string? value)
            {
                _key = key;
                _value = value;
            }

            public string? this[string key]
            {
                get => null;
                set { }
            }

            public string Key => _key;

            public string Path => _key;

            public string? Value
            {
                get => _value;
                set { }
            }

            public IEnumerable<IConfigurationSection> GetChildren() => System.Linq.Enumerable.Empty<IConfigurationSection>();

            public IChangeToken GetReloadToken() => NullChangeToken.Singleton;

            public IConfigurationSection GetSection(string key) => new DictionaryConfigurationSection(key, null);
        }
    }
}