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

        entity.FullName = user.FullName;
        entity.Username = user.Username;
        entity.Gender = user.Gender?.ToString() ?? "Unknown";
        entity.IsAgeVerified = user.IsAgeVerified;
        entity.RegistrationStatus = user.RegistrationStatus.ToString();
        entity.IsBanned = user.IsBanned;
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

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => u.TelegramId.ToString().Contains(searchTerm));
        }

        var totalCount = await query.CountAsync();

        var entities = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (entities.Select(MapToModel), totalCount);
    }

    public async Task BanUserAsync(long telegramId)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (entity != null)
        {
            entity.IsBanned = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UnbanUserAsync(long telegramId)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (entity != null)
        {
            entity.IsBanned = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private static User MapToModel(UserEntity entity)
    {
        return new User
        {
            TelegramId = entity.TelegramId,
            FullName = entity.FullName,
            Username = entity.Username,
            Gender = Enum.Parse<Gender>(entity.Gender, ignoreCase: true),
            IsAgeVerified = entity.IsAgeVerified,
            RegistrationStatus = Enum.Parse<UserRegistrationStatus>(entity.RegistrationStatus),
            IsBanned = entity.IsBanned,
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

/// <summary>
/// Repository for admin persistence operations using Entity Framework.
/// </summary>
public class AdminRepository : IAdminRepository
{
    private readonly ChatBotDbContext _context;

    public AdminRepository(ChatBotDbContext context)
    {
        _context = context;
    }

    public async Task<Admin?> GetByUsernameAsync(string username)
    {
        var entity = await _context.Admins
            .FirstOrDefaultAsync(a => a.Username == username);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddAsync(Admin admin)
    {
        var entity = new AdminEntity
        {
            Username = admin.Username,
            PasswordHash = admin.PasswordHash,
            CreatedAt = admin.CreatedAt
        };

        _context.Admins.Add(entity);
        await _context.SaveChangesAsync();
        
        admin.Id = entity.Id;
    }

    public async Task UpdateLastLoginAsync(int adminId)
    {
        var entity = await _context.Admins.FindAsync(adminId);
        if (entity != null)
        {
            entity.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        return await _context.Admins.AnyAsync();
    }

    private static Admin MapToModel(AdminEntity entity)
    {
        return new Admin
        {
            Id = entity.Id,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            CreatedAt = entity.CreatedAt,
            LastLoginAt = entity.LastLoginAt
        };
    }
}
