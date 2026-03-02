using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Requests;
using NLog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using NLog.Fluent;

namespace ModuloAlertas
{
    public class TelegramBot
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly TelegramBotClient _botClient;
        private readonly string _connectionString;

        // TODO: esto es temporario para no persistir de momento
        private int _lastUpdateId = 0; // Para manejar el offset de GetUpdates
        private readonly ConcurrentDictionary<long, string?> _chatsActivos;

        public TelegramBot(string token, string connectionString)
        {
            _botClient = new TelegramBotClient(token);
            _chatsActivos = new ConcurrentDictionary<long, string?>();

            var cts = new CancellationTokenSource();

            // Configuramos el receiver
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // recibe todos los tipos de updates
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );
            _connectionString = connectionString;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message is { } message)
            {
                var chatId = message.Chat.Id;
                var username = message.Chat.Username;

                _chatsActivos.TryAdd(chatId, username);

                _logger.Info($"Usuario: @{username} - ChatId: {chatId}");

                // usar el cliente interno
                await _botClient.SendRequest(new SendMessageRequest
                {
                    ChatId = chatId,
                    Text = "Registro exitoso ✅"
                });

                try
                {
                    //TODO: PERSISTIR _el_chatId_y_username
                    using (var db = new Npgsql.NpgsqlConnection(_connectionString))
                    {
                        long numUser;
                        db.Open();
                        string updateQuery = "UPDATE usuario_telegram" +
                                             "SET user_chatidtelegram = @chatId, user_usernametelegram = @username " +
                                             "ON CONFLICT (chat_id) DO UPDATE SET username = EXCLUDED.username;";

                        using (var cmd = new Npgsql.NpgsqlCommand(updateQuery, db))
                        {
                            cmd.Parameters.AddWithValue("chatId", chatId);
                            cmd.Parameters.AddWithValue("username", (object?)username ?? DBNull.Value);

                            numUser = cmd.ExecuteNonQuery();
                        }
                    }
                    _logger.Info($"Persistido chatId/username en DB: @{username} - {chatId}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error al persistir chatId/username en DB: {ex.Message}");
                }
            }
        }
        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            _logger.Info($"Error en bot: {ex.Message}");
            return Task.CompletedTask;
        }

        public List<long> GetChatIdsActivos()
        {
            return _chatsActivos.Keys.ToList();
        }

        public async Task EnviarNotificacionAsync(NotificacionDTO notificacion)
        {
            try
            {
                string mensaje = notificacion.Mensaje;
                var chatId = notificacion.GetChatID();
                var lat = notificacion.GetLatitud();
                var lon = notificacion.GetLongitud();

                var request = new SendMessageRequest
                {
                    ChatId = chatId,
                    Text = mensaje,
                    ParseMode = ParseMode.Markdown
                };

                // mensaje de texto
                await _botClient.SendRequest(request);

                if(!string.IsNullOrEmpty(lat) || !string.IsNullOrEmpty(lon))
                {
                    //lat = "-34,6257";
                    //lon = "-58,3708";
                    // mensaje de ubicación
                    var locationRequest = new SendLocationRequest
                    {
                        ChatId = chatId,
                        Latitude = double.TryParse(lat, out var latVal) ? latVal : -34.6257,
                        Longitude = double.TryParse(lon, out var lonVal) ? lonVal : -58.3708,
                        LivePeriod = 60 * 30 // la ubicación se muestra durante 30 minutos
                    };
                    Message mensajeUbicacion = await _botClient.SendRequest(locationRequest);

                    int mensajeId = mensajeUbicacion.MessageId;
                }

            }
            catch (Exception ex)
            {
                _logger.Error($"Error al enviar notificación a Telegram: {ex.Message}");
            }
        }

        /// <summary>
        /// Pull manual de actualizaciones pendientes desde Telegram.
        /// </summary>
        public async Task<Update[]> GetUpdatesAsync()
        {
            try
            {
                var updates = await _botClient.GetUpdates(
                    offset: _lastUpdateId + 1,
                    timeout: 5
                );

                if (updates.Any())
                {
                    _lastUpdateId = updates.Max(u => u.Id);
                }

                return updates;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error al obtener actualizaciones de Telegram.");
                return Array.Empty<Update>();
            }
        }

        /// <summary>
        /// Arranca la escucha en tiempo real con StartReceiving.
        /// </summary>
        /// <param name="handleUpdateAsync"></param>
        /// <param name="handleErrorAsync"></param>
        /// <param name="cancellationToken"></param>
        public void StartReceiving(
            Func<ITelegramBotClient, Update, CancellationToken, Task> handleUpdateAsync,
            Func<ITelegramBotClient, Exception, CancellationToken, Task> handleErrorAsync,
            CancellationToken cancellationToken = default
)
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // recibe todos los tipos de updates
            };

            var updateHandler = new DefaultUpdateHandler(handleUpdateAsync, handleErrorAsync);

            _botClient.StartReceiving(
                updateHandler: updateHandler,
                receiverOptions: receiverOptions,
                cancellationToken: cancellationToken
            );

            _logger.Info("Bot de Telegram escuchando actualizaciones en tiempo real...");
        }
    }

    /// <summary>
    /// Servicio host que inicia el bot de Telegram y escucha actualizaciones.
    /// </summary>
    public class TelegramBotHostedService : BackgroundService
    {
        private readonly TelegramBot _bot;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public TelegramBotHostedService(TelegramBot bot)
        {
            _bot = bot;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Recuperar updates pendientes
            var updates = await _bot.GetUpdatesAsync();
            foreach (var update in updates)
            {
                if (update.Message != null)
                {
                    var chatId = update.Message.Chat.Id.ToString();
                    var username = update.Message.Chat.Username;

                    _logger.Info("Usuario: @{username} - ChatId: {chatId}", username, chatId);

                    // TODO: Guardar chatId/username en DB
                }
            }

            // Escuchar 
            _bot.StartReceiving(
                async (client, update, ct) =>
                {
                    if (update.Message != null)
                    {
                        var chatId = update.Message.Chat.Id.ToString();
                        var username = update.Message.Chat.Username;

                        _logger.Info("[Tiempo real] Usuario: @{username} - ChatId: {chatId}", username, chatId);

                        // TODO: Guardar chatId/username en DB
                    }
                },
                async (client, ex, ct) =>
                {
                    _logger.Error(ex, "Error en el bot de Telegram");
                    await Task.CompletedTask;
                },
                stoppingToken
            );
        }
    }
}
