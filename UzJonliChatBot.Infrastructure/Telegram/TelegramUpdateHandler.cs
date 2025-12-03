using UzJonliChatBot.Application.Interfaces;

namespace UzJonliChatBot.Infrastructure.Telegram
{
    public class TelegramUpdateHandler(
        IMatchmakingService matchmakingService,
        IChatService chatService)
    {
    }
}