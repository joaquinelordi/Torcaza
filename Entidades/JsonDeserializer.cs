using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class JsonDeserializer
    {
        public static TrackerPayloadBase DeserializeJson(string json)
        {
            try
            {
                // Configuración del convertidor
                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new TrackerPayloadConverter(), new StringEnumConverter() }
                };

                var payload = JsonConvert.DeserializeObject<TrackerPayloadBase>(json, settings);

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
}
