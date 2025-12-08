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
    }
}