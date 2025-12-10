using Microsoft.Extensions.Configuration;
using Telegram.Bot;

namespace UzJonliChatBot.Infrastructure.Telegram;

/// <summary>
/// Factory for creating TelegramBotClient instances.
/// </summary>
public static class TelegramBotClientFactory
{
    public static ITelegramBotClient Create(IConfiguration configuration)
    {
        var token = configuration.GetSection("TelegramBot:Token").Value
            ?? throw new InvalidOperationException("Telegram bot token is not configured.");

        return new TelegramBotClient(token);
    }
}
