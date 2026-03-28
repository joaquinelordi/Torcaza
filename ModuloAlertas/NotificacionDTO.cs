namespace ModuloAlertas
{
    public class NotificacionDTO
    {
        private string _Mensaje;

        public string Mensaje
        {
            get { return _Mensaje; }
            set { _Mensaje = value; }
        }

        public Dictionary<string, object> DatosAdicionales { get; set; } = new Dictionary<string, object>();

        public NotificacionDTO()
        {
            _Mensaje = string.Empty;
        }

        public string GetChatID()
        {
            if (DatosAdicionales.ContainsKey("chatIdTelegram"))
            {
                return DatosAdicionales["chatIdTelegram"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public string GetLatitud()
        {
            if (DatosAdicionales.ContainsKey("latitud"))
            {
                return DatosAdicionales["latitud"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public string GetLongitud()
        {
            if (DatosAdicionales.ContainsKey("longitud"))
            {
                return DatosAdicionales["longitud"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public string GetNombreDispositivo()
        {
            if (DatosAdicionales.ContainsKey("nombreDispositivo"))
            {
                return DatosAdicionales["nombreDispositivo"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public string GetActualizarUbicacionID()
        {
            if (DatosAdicionales.ContainsKey("actualizarUbicacionId"))
            {
                return DatosAdicionales["actualizarUbicacionId"].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        public void SetActualizarUbicacionID(string id)
        {
            if (DatosAdicionales.ContainsKey("actualizarUbicacionId"))
            {
                DatosAdicionales["actualizarUbicacionId"] = id;
            }
        }
    }
}