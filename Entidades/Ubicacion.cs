namespace Entidades
{
    public class Ubicacion
    {
        public string IDAgente { get; set; }
        public string Latitud { get; set; }
        public string Longitud { get; set; }
        public DateTime Timestamp { get; set; }

        public Ubicacion()  
        {
            IDAgente = "";
            Latitud = "";
            Longitud = "";
            Timestamp = DateTime.MinValue;   
        }
    }
}
