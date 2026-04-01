using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;
using UzJonliChatBot.Infrastructure.Persistence.Entities;

namespace UzJonliChatBot.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for user persistence operations using Entity Framework.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;

    public UserRepository(IDbContextFactory<ChatBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddOrUpdateAsync(User user)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == user.TelegramId);

        if (entity == null)
        {
            entity = new UserEntity
            {
                TelegramId = user.TelegramId,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(entity);
        }

        entity.FullName = user.FullName;
        entity.Username = user.Username;
        entity.Gender = user.Gender.ToString();
        entity.IsPremium = user.IsPremium;
        entity.IsAgeVerified = user.IsAgeVerified;
        entity.RegistrationStatus = user.RegistrationStatus.ToString();
        entity.IsBanned = user.IsBanned;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Use AsNoTracking for read-only queries to improve performance
        return await context.Users.AsNoTracking().AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<IEnumerable<User>> GetRegisteredUsersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entities = await context.Users.AsNoTracking()
            .Where(u => u.RegistrationStatus == "Registered" && u.IsAgeVerified)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllUsersAsync(int page, int pageSize, string? searchTerm = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Users.AsNoTracking();

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
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (entity != null)
        {
            entity.IsBanned = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task UnbanUserAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (entity != null)
        {
            entity.IsBanned = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
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
            Id = entity.Id,
            TelegramId = entity.TelegramId,
            FullName = entity.FullName,
            Username = entity.Username,
            Gender = Enum.Parse<Gender>(entity.Gender, ignoreCase: true),
            IsPremium = entity.IsPremium,
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
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;
    private readonly ILogger<ChatRepository> _logger;
    public ChatRepository(IDbContextFactory<ChatBotDbContext> contextFactory, ILogger<ChatRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Chat?> GetActiveChatAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // OPTIMIZED: Only select IDs/TelegramIds to avoid loading full User entities
            var chat = await context.ActiveChats
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
        finally
        {
            sw.Stop();
            _logger?.LogDebug("GetActiveChatAsync for telegramId {TelegramId} elapsedMs {ElapsedMs}", telegramId, sw.ElapsedMilliseconds);
        }
    }

    public async Task AddAsync(Chat chat)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // OPTIMIZED: Use parameterized query to find both users in one efficient operation
            var users = await context.Users
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

            context.ActiveChats.Add(entity);
            await context.SaveChangesAsync();
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("AddAsync Chat between {User1} and {User2} elapsedMs {ElapsedMs}", chat.User1Id, chat.User2Id, sw.ElapsedMilliseconds);
        }
    }

    public async Task RemoveAsync(Chat chat)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // OPTIMIZED: Single query using TelegramId directly
            var entity = await context.ActiveChats
                .FirstOrDefaultAsync(c =>
                    (c.User1.TelegramId == chat.User1Id && c.User2.TelegramId == chat.User2Id) ||
                    (c.User1.TelegramId == chat.User2Id && c.User2.TelegramId == chat.User1Id));

            if (entity != null)
            {
                context.ActiveChats.Remove(entity);
                await context.SaveChangesAsync();
            }
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("RemoveAsync Chat {ChatId} elapsedMs {ElapsedMs}", chat.Id, sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> IsInChatAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // OPTIMIZED: Single query with proper JOIN - no intermediate lookup
            return await context.ActiveChats
                .AsNoTracking()
                .AnyAsync(c => c.User1.TelegramId == telegramId || c.User2.TelegramId == telegramId);
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("IsInChatAsync for telegramId {TelegramId} elapsedMs {ElapsedMs}", telegramId, sw.ElapsedMilliseconds);
        }
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
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;
    private readonly ILogger<MatchmakingQueueRepository> _logger;

    public MatchmakingQueueRepository(IDbContextFactory<ChatBotDbContext> contextFactory, ILogger<MatchmakingQueueRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(long userId, string? genderPreference = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // Use execution strategy for writes so transactions are retriable
            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // Check if user exists AND not in queue in ONE query
                    var user = await context.Users
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
                    context.MatchmakingQueue.Add(new MatchmakingQueueEntity
                    {
                        UserId = user.Id,
                        GenderPreference = string.IsNullOrWhiteSpace(genderPreference) ? null : genderPreference,
                        QueuedAt = DateTime.UtcNow
                    });

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("EnqueueAsync for telegramId {TelegramId} elapsedMs {ElapsedMs}", userId, sw.ElapsedMilliseconds);
        }
    }

    public async Task<long?> DequeueAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // Dequeue performs a delete, so run inside the execution strategy to support retries
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<long?>(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.RepeatableRead);
                try
                {
                    // SINGLE QUERY: Select and delete atomically
                    var entry = await context.MatchmakingQueue
                        .OrderBy(q => q.QueuedAt)
                        .Select(q => new { q.Id, q.User.TelegramId })
                        .FirstOrDefaultAsync();

                    if (entry == null)
                    {
                        await transaction.CommitAsync();
                        return null;
                    }

                    // Delete directly without re-fetching
                    await context.Database.ExecuteSqlInterpolatedAsync(
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
        finally
        {
            sw.Stop();
            _logger?.LogDebug("DequeueAsync elapsedMs {ElapsedMs}", sw.ElapsedMilliseconds);
        }
    }

    public async Task<long?> DequeueByPartnerGenderAsync(string preferredGender)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<long?>(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.RepeatableRead);
                try
                {
                    var entry = await context.MatchmakingQueue
                        .Where(q => q.User.Gender == preferredGender)
                        .OrderBy(q => q.QueuedAt)
                        .Select(q => new { q.Id, q.User.TelegramId })
                        .FirstOrDefaultAsync();

                    if (entry == null)
                    {
                        await transaction.CommitAsync();
                        return null;
                    }

                    await context.Database.ExecuteSqlInterpolatedAsync(
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
        finally
        {
            sw.Stop();
            _logger?.LogDebug("DequeueByPartnerGenderAsync for gender {PreferredGender} elapsedMs {ElapsedMs}", preferredGender, sw.ElapsedMilliseconds);
        }
    }

    public async Task RemoveFromQueueAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // Optimize by doing this in a single transaction with proper isolation
            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.RepeatableRead);
                try
                {
                    // OPTIMIZED: Single delete operation with parameterized SQL
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM \"MatchmakingQueue\" WHERE \"UserId\" = (SELECT \"Id\" FROM \"Users\" WHERE \"TelegramId\" = {telegramId})");

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("RemoveFromQueueAsync for telegramId {TelegramId} elapsedMs {ElapsedMs}", telegramId, sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> IsInQueueAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sw = Stopwatch.StartNew();
        try
        {
            // Optimized: single query without the intermediate user lookup
            return await context.MatchmakingQueue
                .AnyAsync(q => q.User.TelegramId == telegramId);
        }
        finally
        {
            sw.Stop();
            _logger?.LogDebug("IsInQueueAsync for telegramId {TelegramId} elapsedMs {ElapsedMs}", telegramId, sw.ElapsedMilliseconds);
        }
    }
}

public class ReportRepository : IReportRepository
{
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;

    public ReportRepository(IDbContextFactory<ChatBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddReportAsync(Report report)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.Reports.Add(new ReportEntity
        {
            ReporterUserId = report.ReporterUserId,
            ReportedUserId = report.ReportedUserId,
            Reason = report.Reason,
            CreatedAt = DateTime.UtcNow,
            IsResolved = false
        });

        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ReportView>> GetReportsByReportedUserAsync(long telegramId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Reports
            .AsNoTracking()
            .Where(r => r.ReportedUser.TelegramId == telegramId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReportView
            {
                Id = r.Id,
                ReporterTelegramId = r.ReporterUser.TelegramId,
                ReporterUsername = r.ReporterUser.Username,
                ReportedTelegramId = r.ReportedUser.TelegramId,
                ReportedUsername = r.ReportedUser.Username,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                IsResolved = r.IsResolved,
                ResolvedAt = r.ResolvedAt
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ReportView>> GetAllReportsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Reports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReportView
            {
                Id = r.Id,
                ReporterTelegramId = r.ReporterUser.TelegramId,
                ReporterUsername = r.ReporterUser.Username,
                ReportedTelegramId = r.ReportedUser.TelegramId,
                ReportedUsername = r.ReportedUser.Username,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                IsResolved = r.IsResolved,
                ResolvedAt = r.ResolvedAt
            })
            .ToListAsync();
    }

    public async Task<bool> HasRecentDuplicateAsync(long reporterUserId, long reportedUserId, TimeSpan window)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var threshold = DateTime.UtcNow.Subtract(window);
        return await context.Reports.AsNoTracking().AnyAsync(r =>
            r.ReporterUserId == reporterUserId &&
            r.ReportedUserId == reportedUserId &&
            r.CreatedAt >= threshold);
    }

    public async Task<bool> DismissReportAsync(long reportId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var report = await context.Reports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report == null)
        {
            return false;
        }

        report.IsResolved = true;
        report.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetTotalReportsCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Reports.AsNoTracking().CountAsync();
    }

    public async Task<int> GetUnresolvedReportsCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Reports.AsNoTracking().CountAsync(r => !r.IsResolved);
    }
}

/// <summary>
/// Repository for admin persistence operations using Entity Framework.
/// </summary>
public class AdminRepository : IAdminRepository
{
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;

    public AdminRepository(IDbContextFactory<ChatBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Admin?> GetByUsernameAsync(string username)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Admins
            .FirstOrDefaultAsync(a => a.Username == username);

        return entity == null ? null : MapToModel(entity);
    }

    public async Task AddAsync(Admin admin)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = new AdminEntity
        {
            Username = admin.Username,
            PasswordHash = admin.PasswordHash,
            CreatedAt = admin.CreatedAt
        };

        context.Admins.Add(entity);
        await context.SaveChangesAsync();

        admin.Id = entity.Id;
    }

    public async Task UpdateLastLoginAsync(int adminId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var entity = await context.Admins.FindAsync(adminId);
        if (entity != null)
        {
            entity.LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Admins.AnyAsync();
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
