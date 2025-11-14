namespace Entidades
{
    public class Ubicacion
    {
        public string IDAgente { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime Timestamp { get; set; }
        public double Hdop { get; set; }
        public double Altitud { get; set; }

        public Ubicacion()  
        {
            IDAgente = "";
            Latitud = 0;
            Longitud = 0;
            Hdop = 0;
            Altitud = 0;
            Timestamp = DateTime.MinValue;   
        }

        public Ubicacion(GnssInfoPayload payload)
        {
            IDAgente = "";
            Latitud = payload.Latitude;
            Longitud = payload.Longitude;
            Hdop = payload.Hdop;
            Altitud = payload.Altitude;
            Timestamp = payload.DateTime;
        }
    }
}
