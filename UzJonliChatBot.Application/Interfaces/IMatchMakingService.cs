namespace UzJonliChatBot.Application.Interfaces;

public interface IMatchmakingService
{
    bool IsWaiting(long userId);
    void EnqueueUser(long userId);
    long? DequeueUser();
    void RemoveFromQueue(long userId);
}
