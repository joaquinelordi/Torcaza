using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Newtonsoft;
using Telegram.Bot.Types;

namespace ModuloAlertas
{
    public class NotificadorTelegram : INotificador
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly TelegramBot _botClient;

        private readonly string _connectionString = "Host=localhost;Database=pruebas_T1;Username=postgres;Password=Admin01";

        public NotificadorTelegram(TelegramBot botClient)
        {
            _botClient = botClient;
        }
         
        public async Task<bool> EnviarNotificacion(NotificacionDTO notificacion)
        {
            bool resultado = false;
            string chatID = notificacion.GetChatID();
            // Lógica para enviar notificación vía Telegram  
            try
            {
                if (!string.IsNullOrEmpty(chatID))
                {
                    await _botClient.EnviarNotificacionAsync(chatID, notificacion.Mensaje);
                    resultado = true;
                }
                else
                    _logger.Warn($"No se envió notificación a Telegram: ChatID inválido.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al enviar notificación a Telegram: {ex.Message}");
            }

            // Simulamos el envío exitoso  
            return await Task.FromResult(resultado);
        }

        Dictionary<long, long> BuscarChatID(IEnumerable<int> usuarios)
        {
            var chatIds = new Dictionary<long, long>();
            using (var db = new Npgsql.NpgsqlConnection(_connectionString))
            {
                db.Open();
                foreach (var usuarioId in usuarios)
                {
                    using (var cmd = new Npgsql.NpgsqlCommand("SELECT chat_id FROM usuarios_telegram WHERE usuario_id = @usuarioId", db))
                    {
                        cmd.Parameters.AddWithValue("usuarioId", usuarioId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && long.TryParse(result.ToString(), out long chatId))
                        {
                            chatIds[usuarioId] = chatId;
                        }
                        else
                        {
                            _logger.Warn($"No se encontró chat_id para el usuario_id: {usuarioId}");
                        }
                    }
                }
            }
            return chatIds;
        }
    }
}
