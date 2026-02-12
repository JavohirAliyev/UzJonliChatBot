using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    Task AddOrUpdateAsync(User user);
    Task<bool> ExistsAsync(long telegramId);
    Task<IEnumerable<User>> GetRegisteredUsersAsync();
    Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page, int pageSize, string? searchTerm = null);
    Task BanUserAsync(long telegramId);
    Task UnbanUserAsync(long telegramId);
}

public interface IChatRepository
{
    Task<Chat?> GetActiveChatAsync(long userId);
    Task AddAsync(Chat chat);
    Task RemoveAsync(Chat chat);
    Task<bool> IsInChatAsync(long userId);
}

public interface IMatchmakingQueueRepository
{
    Task EnqueueAsync(long userId);
    Task<long?> DequeueAsync();
    Task RemoveFromQueueAsync(long userId);
    Task<bool> IsInQueueAsync(long userId);
}
