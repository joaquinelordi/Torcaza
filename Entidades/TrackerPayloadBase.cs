using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public abstract class TrackerPayloadBase
    {
        [JsonProperty("Type")]
        public eTipoMensaje Type { get; set; }

        //Agrupa los datos de la celda celular de la que se trasmite el mensaje
        public CellInfoRemote CellInfoRemote { get; set; }

        [JsonProperty("DATETIME")]
        public DateTime DateTime { get; set; }
        
        [JsonProperty("BSTA")]
        public eBateryState BateryState {get; set;}
        
        [JsonProperty("BLVL")]
        public int BateryLevel { get; set; }
        
        [JsonProperty("IMEI")]
        public string Imei { get; set; }

        [JsonProperty("EVNT")]
        public string Evento;

        public eTipoMensaje GetTipoMensaje()
        {
            return Type;
        }
    }

    public class CellInfoRemote
    {
        [JsonProperty("MCC")]
        public int Mcc { get; set; }
        [JsonProperty("MNC")]
        public int Mnc { get; set; }
        [JsonProperty("LAC")]
        public string Lac { get; set; }
        [JsonProperty("CID")]
        public string CellId { get; set; }
        [JsonProperty("SLVL")]
        public double SignalLevel { get; set; }
        [JsonProperty("TECH")]
        public int Technology { get; set; }
        [JsonProperty("BAND")]
        public string Band { get; set; }
        [JsonProperty("CHNL")]
        public int Channel { get; set; }
        [JsonProperty("REGS")]
        public int Regs { get; set; }
    }

    public class CellNeighborsInfoPayload : TrackerPayloadBase
    {
        [JsonProperty("Neighbors")]
        public List<CellInfoRemote> NeighborCells { get; set; }
        public CellNeighborsInfoPayload()
        {
            Type = eTipoMensaje.InfoCell;
            NeighborCells = new List<CellInfoRemote>(); 
        }
    }

    public class  GnssInfoPayload : TrackerPayloadBase
    {
        [JsonProperty("LAT")]
        public double Latitude { get; set; }

        [JsonProperty("LONG")]
        public double Longitude { get; set; }

        [JsonProperty("HDOP")]
        public double Hdop { get; set; }

        [JsonProperty("ALT")]
        public double Altitude { get; set; }

        [JsonProperty("COG")]
        public double CourseOverGround { get; set; }

        [JsonProperty("SPD")]
        public double Speed { get; set; }

        public GnssInfoPayload()
        {
            Type = eTipoMensaje.GNSS;
        }
    }

    #region JSON
    public class TrackerPayloadConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TrackerPayloadBase);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Cargar el JSON en un JObject para inspeccionar la propiedad "Type"
            var jsonObject = JObject.Load(reader);

            // Leer el valor de la propiedad "Type"
            var typeValue = jsonObject["Type"]?.ToString();

            // Determinar el tipo de objeto a deserializar
            TrackerPayloadBase payload;
            switch (typeValue)
            {
                case "MNMN": // Tipo InfoCell
                    payload = new CellNeighborsInfoPayload();
                    break;
                case "MNGNSS": // Tipo GNSS
                    payload = new GnssInfoPayload();
                    break;
                default:
                    throw new InvalidOperationException($"Tipo de mensaje no reconocido: {typeValue}");
            }

            // Mapear campos de objetos anidados
            MapearObjetosAnidados(jsonObject, payload);

            // Deserializar el objeto específico
            serializer.Populate(jsonObject.CreateReader(), payload);
            return payload;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Serialización no implementada (opcional)
            throw new NotImplementedException();
        }

        private void MapearObjetosAnidados(JObject jsonObject, TrackerPayloadBase payload)
        {

            if (payload is TrackerPayloadBase basePayload && jsonObject["MCC"] != null)
            {
                basePayload.CellInfoRemote = new CellInfoRemote
                {
                    Mcc = (int)jsonObject["MCC"],
                    Mnc = (int)jsonObject["MNC"],
                    Lac = (string)jsonObject["LAC"],
                    CellId = (string)jsonObject["CID"],
                    SignalLevel = (double)jsonObject["SLVL"],
                    Technology = (int)jsonObject["TECH"],
                    Band = (string)jsonObject["BAND"],
                    Channel = (int)jsonObject["CHNL"],
                    Regs = (int)jsonObject["REGS"]
                };
            }
        }
    }
    #endregion

    // TODO: CONSULTAR DEL DOCUMENTACION DE AT QUE REPRESENTA
    // LOS NUMEROS DE BATERY STATE 
    public enum eBateryState
    {
        eNoEstaCargando = 0,
        eCargaEnProceso = 1,
        eCargaTerminada = 2
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum eModoOperacion
    {
        [EnumMember(Value = "NOPM")]
        Normal,
        [EnumMember(Value = "POPM")]
        Persecucion,
        [EnumMember(Value = "SOPM")]
        Sleep
    }

    public enum eLatencia
    {
        [EnumMember(Value = "ELL")]
        ExtramadamenteBaja,
        [EnumMember(Value = "VLL")]
        MuyBaja,
        [EnumMember(Value = "LL")]
        Baja,
        [EnumMember(Value = "ML")]
        Media,
        [EnumMember(Value = "HL")]
        Alta,
        [EnumMember(Value = "VHL")]
        MuyAlta
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum eTipoMensaje
    {
        [EnumMember(Value = "MNMN")]
        InfoCell,

        [EnumMember(Value = "MNGNSS")]
        GNSS,

        TipoDesconocido = 99
    }

    public class RespuestaEstado
    {
        [JsonProperty("SUCS")]
        public bool Success { get; set; }

        [JsonProperty("LTCY")]
        public eLatencia Latency { get; set; }

        [JsonProperty("MODE")]
        public eModoOperacion Mode { get; set; }

        [JsonProperty("TSOPM", NullValueHandling = NullValueHandling.Ignore)]
        public int? Timer { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>();
            var props = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var jsonProp = prop.GetCustomAttribute<JsonPropertyAttribute>();
                var value = prop.GetValue(this);

                if (jsonProp != null && jsonProp.NullValueHandling == NullValueHandling.Ignore && value == null)
                    continue;

                var key = jsonProp?.PropertyName ?? prop.Name;
                dict[key] = value;
            }
            return dict;
        }



    }
}
