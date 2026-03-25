namespace Entidades
{
    public class Ubicacion
    {
        public string IDAgente { get; set; }
        public string RegistroID { get; set; }
        public string DispositivoID { get; set; }
        public long NumeroEvento { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime Timestamp { get; set; }
        public double Hdop { get; set; }
        public double Altitud { get; set; }
        public double SigmaPosicion { get; set; }
        public string AliasDispositivo { get; set; }

        public Ubicacion()  
        {
            IDAgente = string.Empty;
            RegistroID = string.Empty;
            DispositivoID = string.Empty;
            NumeroEvento = 0;
            Latitud = 0;
            Longitud = 0;
            Hdop = 0;
            Altitud = 0;
            Timestamp = DateTime.MinValue;   
            SigmaPosicion = 0;
            AliasDispositivo = string.Empty;
        }

        public Ubicacion(GnssInfoPayload payload)
        {
            IDAgente = "";
            Latitud = payload.Latitude;
            Longitud = payload.Longitude;
            Hdop = payload.Hdop;
            Altitud = payload.Altitude;
            Timestamp = payload.DateTime;
            SigmaPosicion = 0;
            AliasDispositivo = "";
        }
    }
}
