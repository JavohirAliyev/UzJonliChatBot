using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Repository interface for user persistence operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by Telegram ID.
    /// </summary>
    Task<User?> GetByTelegramIdAsync(long telegramId);

    /// <summary>
    /// Adds or updates a user.
    /// </summary>
    Task AddOrUpdateAsync(User user);

    /// <summary>
    /// Checks if a user exists by Telegram ID.
    /// </summary>
    Task<bool> ExistsAsync(long telegramId);

    /// <summary>
    /// Gets all registered and verified users.
    /// </summary>
    Task<IEnumerable<User>> GetRegisteredUsersAsync();
}

/// <summary>
/// Repository interface for chat persistence operations.
/// </summary>
public interface IChatRepository
{
    /// <summary>
    /// Gets an active chat for a user.
    /// </summary>
    Task<Chat?> GetActiveChatAsync(long userId);

    /// <summary>
    /// Creates a new chat between two users.
    /// </summary>
    Task AddAsync(Chat chat);

    /// <summary>
    /// Removes a chat (ends the conversation).
    /// </summary>
    Task RemoveAsync(Chat chat);

    /// <summary>
    /// Checks if a user is in an active chat.
    /// </summary>
    Task<bool> IsInChatAsync(long userId);
}

/// <summary>
/// Repository interface for matchmaking queue persistence.
/// </summary>
public interface IMatchmakingQueueRepository
{
    /// <summary>
    /// Adds a user to the queue.
    /// </summary>
    Task EnqueueAsync(long userId);

    /// <summary>
    /// Gets the first user from the queue.
    /// </summary>
    Task<long?> DequeueAsync();

    /// <summary>
    /// Removes a user from the queue.
    /// </summary>
    Task RemoveFromQueueAsync(long userId);

    /// <summary>
    /// Checks if a user is in the queue.
    /// </summary>
    Task<bool> IsInQueueAsync(long userId);
}
