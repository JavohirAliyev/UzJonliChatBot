namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;

public class MatchmakingService : IMatchmakingService
{
    private readonly Queue<long> _queue = new();

    public bool IsWaiting(long userId) => _queue.Contains(userId);

    public void EnqueueUser(long userId) => _queue.Enqueue(userId);

    public long? DequeueUser() =>
        _queue.Count > 0 ? _queue.Dequeue() : null;

    public void RemoveFromQueue(long userId)
    {
        var newQueue = new Queue<long>(_queue.Where(x => x != userId));
        _queue.Clear();
        foreach (var u in newQueue) _queue.Enqueue(u);
    }
}
