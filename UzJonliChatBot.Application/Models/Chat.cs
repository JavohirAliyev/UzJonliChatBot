namespace UzJonliChatBot.Application.Models;

/// <summary>
/// Represents an active chat between two users.
/// </summary>
public class Chat
{
    public long Id { get; set; }
    public long User1Id { get; set; }
    public long User2Id { get; set; }
    public DateTime StartedAt { get; set; }

    public Chat()
    {
        StartedAt = DateTime.UtcNow;
    }
}
