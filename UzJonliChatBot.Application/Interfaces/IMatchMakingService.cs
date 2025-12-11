namespace UzJonliChatBot.Application.Interfaces;

public interface IMatchmakingService
{
    Task<bool> IsWaitingAsync(long userId);
    Task EnqueueUserAsync(long userId);
    Task<long?> DequeueUserAsync();
    Task RemoveFromQueueAsync(long userId);
}
