using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public enum eCercoTipo
    {
        Circulo = 0,
        Rectangulo = 1,
        Poligono = 2
    }

    public abstract class CercoVirtualBase
    {
       public eCercoTipo Tipo { get; set; }
    }

    public class CercoCirculo : CercoVirtualBase
    {
        public eCercoTipo Tipo { get; set; } = eCercoTipo.Circulo;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double Radio { get; set; }

        public CercoCirculo() { }
        public CercoCirculo(double lat, double lng, double radio)
        {
            Lat = lat;
            Lng = lng;
            Radio = radio;
        }
    }

    public class CercoRectangulo : CercoVirtualBase
    {
        public eCercoTipo Tipo { get; set; } = eCercoTipo.Rectangulo;
        public double SurOesteLat { get; set; }
        public double SurOesteLng { get; set; }
        public double NorEsteLat { get; set; }
        public double NorEsteLng { get; set; }

        public CercoRectangulo() { }
        public CercoRectangulo(double surOesteLat, double surOesteLng, double norEsteLat, double norEsteLng)
        {
            SurOesteLat = surOesteLat;
            SurOesteLng = surOesteLng;
            NorEsteLat = norEsteLat;
            NorEsteLng = norEsteLng;
        }
    }

    public class CercosPayload
    {
        [JsonConverter(typeof(CercoVirtualListConverter))]
        public List<CercoVirtualBase> Cercos { get; set; }
    }

}
