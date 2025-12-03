using Telegram.Bot;

namespace UzJonliChatBot.Infrastructure.Telegram
{
    public class TelegramService(ITelegramBotClient bot)
    {
        private readonly ITelegramBotClient _bot = bot;

        public async Task Send(long userId, string text)
            => await _bot.SendMessage(userId, text);
    }
}
