namespace Entidades
{
    public class Ubicacion
    {
        public string IDAgente { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime Timestamp { get; set; }

        public Ubicacion()  
        {
            IDAgente = "";
            Latitud = 0;
            Longitud = 0;
            Timestamp = DateTime.MinValue;   
        }

        public Ubicacion(GnssInfoPayload payload)
        {
            IDAgente = "";
            Latitud = payload.Latitude;
            Longitud = payload.Longitude;
        }
    }
}
