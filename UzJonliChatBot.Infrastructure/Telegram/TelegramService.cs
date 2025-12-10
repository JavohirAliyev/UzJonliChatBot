using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UzJonliChatBot.Infrastructure.Telegram;

/// <summary>
/// Main Telegram bot service that handles updates and runs the bot.
/// </summary>
public class TelegramService
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramUpdateHandler _updateHandler;

    public TelegramService(
        ITelegramBotClient botClient,
        TelegramUpdateHandler updateHandler)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
    }

    /// <summary>
    /// Starts the bot and listens for updates.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _botClient.GetMe(cancellationToken: cancellationToken);
        Console.WriteLine($"Bot started: @{me.Username}");

        // Set up bot commands for dropdown menu
        await SetupCommandsAsync(cancellationToken);

        var allowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery };

        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdates(
                    offset: offset,
                    allowedUpdates: allowedUpdates,
                    cancellationToken: cancellationToken);

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting updates: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }
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
            Console.WriteLine("Bot commands registered");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up commands: {ex.Message}");
        }
    }
}
