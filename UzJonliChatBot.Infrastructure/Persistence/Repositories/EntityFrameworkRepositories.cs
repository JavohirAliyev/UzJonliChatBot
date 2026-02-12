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
        entity.Gender = user.Gender.ToString();
        entity.IsAgeVerified = user.IsAgeVerified;
        entity.RegistrationStatus = user.RegistrationStatus.ToString();
        entity.IsBanned = user.IsBanned;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long telegramId)
    {
        // Use AsNoTracking for read-only queries to improve performance
        return await _context.Users.AsNoTracking().AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<IEnumerable<User>> GetRegisteredUsersAsync()
    {
        var entities = await _context.Users.AsNoTracking()
            .Where(u => u.RegistrationStatus == "Registered" && u.IsAgeVerified)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _context.Users.AsNoTracking();

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
        if (string.IsNullOrEmpty(entity.Gender) || entity.Gender.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"User {entity.TelegramId} has no valid gender set. Gender is required.");
        }

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
        // OPTIMIZED: Only select IDs/TelegramIds to avoid loading full User entities
        var chat = await _context.ActiveChats
            .Where(c => c.User1.TelegramId == telegramId || c.User2.TelegramId == telegramId)
            .Select(c => new
            {
                c.Id,
                c.User1Id,
                c.User2Id,
                User1Telegram = c.User1.TelegramId,
                User2Telegram = c.User2.TelegramId,
                c.StartedAt
            })
            .FirstOrDefaultAsync();

        if (chat == null)
            return null;

        // Map selected fields into Chat model (using TelegramIds as User1Id/User2Id)
        return new Chat
        {
            Id = chat.Id,
            User1Id = chat.User1Telegram,
            User2Id = chat.User2Telegram,
            StartedAt = chat.StartedAt
        };
    }

    public async Task AddAsync(Chat chat)
    {
        // OPTIMIZED: Use parameterized query to find both users in one efficient operation
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => u.TelegramId == chat.User1Id || u.TelegramId == chat.User2Id)
            .ToListAsync();

        var user1 = users.FirstOrDefault(u => u.TelegramId == chat.User1Id);
        var user2 = users.FirstOrDefault(u => u.TelegramId == chat.User2Id);

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
        // OPTIMIZED: Single query using TelegramId directly
        var entity = await _context.ActiveChats
            .FirstOrDefaultAsync(c =>
                (c.User1.TelegramId == chat.User1Id && c.User2.TelegramId == chat.User2Id) ||
                (c.User1.TelegramId == chat.User2Id && c.User2.TelegramId == chat.User1Id));

        if (entity != null)
        {
            _context.ActiveChats.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsInChatAsync(long telegramId)
    {
        // OPTIMIZED: Single query with proper JOIN - no intermediate lookup
        return await _context.ActiveChats
            .AsNoTracking()
            .AnyAsync(c => c.User1.TelegramId == telegramId || c.User2.TelegramId == telegramId);
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
        // Use execution strategy for writes so transactions are retriable
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if user exists AND not in queue in ONE query
                var user = await _context.Users
                    .Where(u => u.TelegramId == userId)
                    .Select(u => new { u.Id, InQueue = u.QueueEntry != null })
                    .FirstOrDefaultAsync();

                if (user == null)
                    throw new InvalidOperationException($"User {userId} not found");

                if (user.InQueue)
                {
                    await transaction.CommitAsync();
                    return; // Already queued, exit early
                }

                // Add to queue
                _context.MatchmakingQueue.Add(new MatchmakingQueueEntity
                {
                    UserId = user.Id,
                    QueuedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<long?> DequeueAsync()
    {
        // Dequeue performs a delete, so run inside the execution strategy to support retries
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<long?>(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead);
            try
            {
                // SINGLE QUERY: Select and delete atomically
                var entry = await _context.MatchmakingQueue
                    .OrderBy(q => q.QueuedAt)
                    .Select(q => new { q.Id, q.User.TelegramId })
                    .FirstOrDefaultAsync();

                if (entry == null)
                {
                    await transaction.CommitAsync();
                    return null;
                }

                // Delete directly without re-fetching
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"MatchmakingQueue\" WHERE \"Id\" = {entry.Id}");

                await transaction.CommitAsync();
                return entry.TelegramId;
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
        // Optimize by doing this in a single transaction with proper isolation
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead);
            try
            {
                // OPTIMIZED: Single delete operation with parameterized SQL
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"DELETE FROM ""MatchmakingQueue"" 
                      WHERE ""UserId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""TelegramId"" = {telegramId})");

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<bool> IsInQueueAsync(long telegramId)
    {
        // Optimized: single query without the intermediate user lookup
        return await _context.MatchmakingQueue
            .AnyAsync(q => q.User.TelegramId == telegramId);
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
