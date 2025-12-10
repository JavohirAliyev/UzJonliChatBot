namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Service for managing user data and existence.
/// </summary>
public class UserService : IUserService
{
    // In-memory user storage (can be replaced with database)
    private readonly HashSet<long> _users = new();

    public bool Exists(long userId) => _users.Contains(userId);

    public void EnsureCreated(long userId)
    {
        if (!_users.Contains(userId))
        {
            _users.Add(userId);
        }
    }
}

