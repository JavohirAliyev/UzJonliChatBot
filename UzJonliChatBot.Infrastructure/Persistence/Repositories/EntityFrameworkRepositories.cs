using Microsoft.EntityFrameworkCore;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;
using UzJonliChatBot.Infrastructure.Persistence.Entities;

namespace UzJonliChatBot.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for user persistence operations using Entity Framework.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ChatBotDbContext _context;

    public UserRepository(ChatBotDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddOrUpdateAsync(User user)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == user.TelegramId);

        if (entity == null)
        {
            entity = new UserEntity
            {
                TelegramId = user.TelegramId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(entity);
        }

        entity.Gender = user.Gender?.ToString() ?? "Unknown";
        entity.IsAgeVerified = user.IsAgeVerified;
        entity.RegistrationStatus = user.RegistrationStatus.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long telegramId)
    {
        return await _context.Users.AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<IEnumerable<User>> GetRegisteredUsersAsync()
    {
        var entities = await _context.Users
            .Where(u => u.RegistrationStatus == "Registered" && u.IsAgeVerified)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    private static User MapToModel(UserEntity entity)
    {
        return new User
        {
            TelegramId = entity.TelegramId,
            Gender = Enum.Parse<Gender>(entity.Gender, ignoreCase: true),
            IsAgeVerified = entity.IsAgeVerified,
            RegistrationStatus = Enum.Parse<UserRegistrationStatus>(entity.RegistrationStatus),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}

/// <summary>
/// Repository for chat persistence operations using Entity Framework.
/// </summary>
public class ChatRepository : IChatRepository
{
    private readonly ChatBotDbContext _context;

    public ChatRepository(ChatBotDbContext context)
    {
        _context = context;
    }

    public async Task<Chat?> GetActiveChatAsync(long userId)
    {
        var entity = await _context.ActiveChats
            .FirstOrDefaultAsync(c => c.User1Id == userId || c.User2Id == userId);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddAsync(Chat chat)
    {
        var entity = new ActiveChatEntity
        {
            User1Id = chat.User1Id,
            User2Id = chat.User2Id,
            StartedAt = chat.StartedAt
        };

        _context.ActiveChats.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(Chat chat)
    {
        var entity = await _context.ActiveChats
            .FirstOrDefaultAsync(c => c.User1Id == chat.User1Id && c.User2Id == chat.User2Id);

        if (entity != null)
        {
            _context.ActiveChats.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInChatAsync(long userId)
    {
        return await _context.ActiveChats
            .AnyAsync(c => c.User1Id == userId || c.User2Id == userId);
    }

    private static Chat MapToModel(ActiveChatEntity entity)
    {
        return new Chat
        {
            Id = entity.Id,
            User1Id = entity.User1Id,
            User2Id = entity.User2Id,
            StartedAt = entity.StartedAt
        };
    }
}

/// <summary>
/// Repository for matchmaking queue persistence using Entity Framework.
/// </summary>
public class MatchmakingQueueRepository : IMatchmakingQueueRepository
{
    private readonly ChatBotDbContext _context;

    public MatchmakingQueueRepository(ChatBotDbContext context)
    {
        _context = context;
    }

    public async Task EnqueueAsync(long userId)
    {
        // userId is actually TelegramId - find the actual UserEntity.Id
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == userId);
        
        if (user == null)
            throw new InvalidOperationException($"User with Telegram ID {userId} does not exist. User must be registered first.");

        var existingEntry = await _context.MatchmakingQueue
            .FirstOrDefaultAsync(q => q.UserId == user.Id);

        if (existingEntry == null)
        {
            _context.MatchmakingQueue.Add(new MatchmakingQueueEntity
            {
                UserId = user.Id,
                QueuedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task<long?> DequeueAsync()
    {
        // Use a transaction with serializable isolation level to prevent race conditions
        // This ensures that concurrent dequeue operations don't return the same user
        // Retry logic handles serialization failures that can occur with high concurrency
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                // Get the first entry ordered by queue time
                // The serializable isolation level ensures no concurrent transaction
                // can modify this row until we commit
                var entry = await _context.MatchmakingQueue
                    .Include(q => q.User)
                    .OrderBy(q => q.QueuedAt)
                    .FirstOrDefaultAsync();

                if (entry == null)
                {
                    await transaction.CommitAsync();
                    return null;
                }

                var telegramId = entry.User.TelegramId;

                // Remove the entry atomically within the transaction
                _context.MatchmakingQueue.Remove(entry);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return telegramId;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                // Another transaction modified the data, retry
                await transaction.RollbackAsync();
                if (attempt == maxRetries - 1) throw;
                await Task.Delay(10 * (attempt + 1)); // Exponential backoff
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        return null;
    }

    public async Task RemoveFromQueueAsync(long telegramId)
    {
        // telegramId is the Telegram ID, find the user first
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
            return;

        var entry = await _context.MatchmakingQueue
            .FirstOrDefaultAsync(q => q.UserId == user.Id);

        if (entry != null)
        {
            _context.MatchmakingQueue.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInQueueAsync(long telegramId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
            return false;

        return await _context.MatchmakingQueue
            .AnyAsync(q => q.UserId == user.Id);
    }
}
