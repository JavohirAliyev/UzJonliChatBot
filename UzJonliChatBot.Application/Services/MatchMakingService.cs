namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Service for managing the user matchmaking queue.
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private readonly IMatchmakingQueueRepository _queueRepository;

    public MatchmakingService(IMatchmakingQueueRepository queueRepository)
    {
        _queueRepository = queueRepository;
    }

    public bool IsWaiting(long userId)
    {
        return _queueRepository.IsInQueueAsync(userId).Result;
    }

    public void EnqueueUser(long userId)
    {
        _queueRepository.EnqueueAsync(userId).Wait();
    }

    public long? DequeueUser()
    {
        return _queueRepository.DequeueAsync().Result;
    }

    public void RemoveFromQueue(long userId)
    {
        _queueRepository.RemoveFromQueueAsync(userId).Wait();
    }
}
