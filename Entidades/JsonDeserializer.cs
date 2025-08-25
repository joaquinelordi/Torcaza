using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class JsonDeserializer
    {
        // Define la configuración del serializador JSON
        public static readonly JsonSerializerSettings _Settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
            new TrackerPayloadConverter(),
            new CercoVirtualListConverter(),
            new StringEnumConverter()
            }
        };

        public static T DeserializeJson<T>(string json, JsonSerializerSettings settings = null)
        {
            try
            {
                // Si no hay un setting especifico uso el global
                var result = JsonConvert.DeserializeObject<T>(json, settings ?? _Settings);
                if (result == null)
                    throw new InvalidOperationException($"El JSON no pudo ser deserializado a un objeto de tipo {typeof(T).Name}.");
                return result;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error al deserializar JSON: {ex.Message}");
                return default;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error : {ex.Message}");
                return default;
            }
        }

        public static TrackerPayloadBase DeserializeTrackerPayload(string json)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<TrackerPayloadBase>(json, _Settings);

                if (payload == null)
                {
                    throw new InvalidOperationException("El JSON no pudo ser deserializado en un objeto TrackerPayloadBase.");
                }

                return payload;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error al deserializar JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inesperado: {ex.Message}");
                return null;
            }
        }
    }
    public class CercoVirtualListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(List<CercoVirtualBase>).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var cercoArray = JArray.Load(reader);
            var result = new List<CercoVirtualBase>();

            foreach (var item in cercoArray)
            {
                var tipoToken = item["Tipo"];
                if (tipoToken == null || !Enum.TryParse(typeof(eCercoTipo), tipoToken.ToString(), out var tipo))
                    continue;

                CercoVirtualBase cerco = tipo switch
                {
                    eCercoTipo.Rectangulo => item.ToObject<CercoRectangulo>(serializer),
                    eCercoTipo.Circulo => item.ToObject<CercoCirculo>(serializer),
                    _ => null
                };

                if (cerco != null)
                    result.Add(cerco);
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var lista = value as List<CercoVirtualBase>;
            serializer.Serialize(writer, lista);
        }
    }
}
