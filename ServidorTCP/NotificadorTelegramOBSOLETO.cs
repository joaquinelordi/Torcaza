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

namespace ServidorTCP
{
    public class TelegramBotardoViejo
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly TelegramBotClient _botClient;

        // TODO: esto es temporario para no persistir de momento
        private int _lastUpdateId = 0; // Para manejar el offset de GetUpdates
        private readonly ConcurrentDictionary<long, string?> _chatsActivos;

        public TelegramBotardoViejo(string token)
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

        public async Task EnviarNotificacionAsync(string chatId, string mensaje)
        {
            try
            {
                var request = new SendMessageRequest
                {
                    ChatId = chatId,
                    Text = mensaje,
                    ParseMode = ParseMode.Markdown
                };

                await _botClient.SendRequest(request);
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

    public class NotificadorTelegramOBSOLETO
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly TelegramBotardoViejo _botClient;
        private readonly string _chatId;

        public NotificadorTelegramOBSOLETO(TelegramBotardoViejo botClient, string chatId)
        {
            _botClient = botClient;
            _chatId = chatId;
        }

        public async Task EnviarNotificacionAsync(string mensaje)
        {
            try
            {
                await _botClient.EnviarNotificacionAsync(_chatId, mensaje);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error al enviar notificación a Telegram: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Servicio host que inicia el bot de Telegram y escucha actualizaciones.
    /// </summary>
    public class TelegramBotardoViejoHostedService : BackgroundService
    {
        private readonly TelegramBotardoViejo _bot;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public TelegramBotardoViejoHostedService(TelegramBotardoViejo bot)
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
