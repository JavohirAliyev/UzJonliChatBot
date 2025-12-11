using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UzJonliChatBot.Infrastructure.Telegram;

/// <summary>
/// Main Telegram bot service that handles updates and runs the bot.
/// </summary>
public class TelegramService : IHostedService, IDisposable
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramUpdateHandler _updateHandler;
    private readonly ILogger<TelegramService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public TelegramService(
        ITelegramBotClient botClient,
        TelegramUpdateHandler updateHandler,
        ILogger<TelegramService> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    /// <summary>
    /// Starts the bot and listens for updates.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram bot service...");
        
        try
        {
            var me = await _botClient.GetMe(cancellationToken: cancellationToken);
            _logger.LogInformation("Bot started: @{Username} (ID: {Id})", me.Username, me.Id);

            // Set up bot commands for dropdown menu
            await SetupCommandsAsync(cancellationToken);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runningTask = RunBotAsync(_cts.Token);
            
            _logger.LogInformation("Telegram bot service started successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Telegram bot service.");
            throw;
        }
    }

    private async Task RunBotAsync(CancellationToken cancellationToken)
    {
        var allowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery };
        var offset = 0;
        var consecutiveErrors = 0;
        const int maxConsecutiveErrors = 10;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdates(
                    offset: offset,
                    allowedUpdates: allowedUpdates,
                    cancellationToken: cancellationToken);

                consecutiveErrors = 0; // Reset error counter on success

                if (updates.Length == 0)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                foreach (var update in updates)
                {
                    await _updateHandler.HandleUpdateAsync(update);
                    offset = update.Id + 1;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Telegram bot service cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                _logger.LogError(ex, "Error getting updates (consecutive errors: {Count})", consecutiveErrors);
                
                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    _logger.LogCritical("Too many consecutive errors ({Count}). Stopping bot service.", consecutiveErrors);
                    throw; // Let the host handle this
                }
                
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram bot service...");
        
        _cts?.Cancel();
        
        if (_runningTask != null)
        {
            try
            {
                await Task.WhenAny(_runningTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping Telegram bot service.");
            }
        }
        
        _logger.LogInformation("Telegram bot service stopped.");
    }

    /// <summary>
    /// Sets up bot commands for the dropdown menu.
    /// </summary>
    private async Task SetupCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var commands = new[]
            {
                new BotCommand { Command = "start", Description = "Ro'yxatdan o'tish" },
                new BotCommand { Command = "keyingi", Description = "Yangi suhbatni boshlash" },
                new BotCommand { Command = "stop", Description = "Suhbatni tugatish" }
            };

            await _botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
            _logger.LogInformation("Bot commands registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting up bot commands. Bot will continue without custom commands.");
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
