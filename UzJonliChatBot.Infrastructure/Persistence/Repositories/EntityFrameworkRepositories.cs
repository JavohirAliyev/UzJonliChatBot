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

    public async Task<Chat?> GetActiveChatAsync(long telegramId)
    {
        // Convert Telegram ID to database User ID
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
            return null;

        // Query using database User ID
        var entity = await _context.ActiveChats
            .Include(c => c.User1)
            .Include(c => c.User2)
            .FirstOrDefaultAsync(c => c.User1Id == user.Id || c.User2Id == user.Id);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddAsync(Chat chat)
    {
        // chat.User1Id and chat.User2Id are Telegram IDs - convert to database User IDs
        var user1 = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == chat.User1Id);
        var user2 = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == chat.User2Id);

        if (user1 == null)
            throw new InvalidOperationException($"User with Telegram ID {chat.User1Id} does not exist. User must be registered first.");
        if (user2 == null)
            throw new InvalidOperationException($"User with Telegram ID {chat.User2Id} does not exist. User must be registered first.");

        var entity = new ActiveChatEntity
        {
            User1Id = user1.Id,
            User2Id = user2.Id,
            StartedAt = chat.StartedAt
        };

        _context.ActiveChats.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(Chat chat)
    {
        // chat.User1Id and chat.User2Id are Telegram IDs - convert to database User IDs
        var user1 = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == chat.User1Id);
        var user2 = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == chat.User2Id);

        if (user1 == null || user2 == null)
            return;

        var entity = await _context.ActiveChats
            .FirstOrDefaultAsync(c => c.User1Id == user1.Id && c.User2Id == user2.Id);

        if (entity != null)
        {
            _context.ActiveChats.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInChatAsync(long telegramId)
    {
        // Convert Telegram ID to database User ID
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
            return false;

        // Query using database User ID
        return await _context.ActiveChats
            .AnyAsync(c => c.User1Id == user.Id || c.User2Id == user.Id);
    }

    private static Chat MapToModel(ActiveChatEntity entity)
    {
        // Convert database User IDs back to Telegram IDs
        return new Chat
        {
            Id = entity.Id,
            User1Id = entity.User1.TelegramId,
            User2Id = entity.User2.TelegramId,
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
        // Use execution strategy to wrap the transaction - this is required when using
        // retry strategies with explicit transactions in EF Core
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync<long?>(async () =>
        {
            // Use a transaction with serializable isolation level to prevent race conditions
            // This ensures that concurrent dequeue operations don't return the same user
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
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
