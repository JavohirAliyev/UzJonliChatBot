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

    public async Task<bool> IsWaitingAsync(long userId)
    {
        return await _queueRepository.IsInQueueAsync(userId);
    }

    public async Task EnqueueUserAsync(long userId)
    {
        await _queueRepository.EnqueueAsync(userId);
    }

    public async Task<long?> DequeueUserAsync()
    {
        return await _queueRepository.DequeueAsync();
    }

    public async Task RemoveFromQueueAsync(long userId)
    {
        await _queueRepository.RemoveFromQueueAsync(userId);
    }
}
