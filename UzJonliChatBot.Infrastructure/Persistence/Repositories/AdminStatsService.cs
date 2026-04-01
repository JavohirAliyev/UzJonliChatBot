using Microsoft.EntityFrameworkCore;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Infrastructure.Persistence.Repositories;

public class AdminStatsService : IAdminStatsService
{
    private readonly IDbContextFactory<ChatBotDbContext> _contextFactory;

    public AdminStatsService(IDbContextFactory<ChatBotDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<AdminStats> GetStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var todayUtc = DateTime.UtcNow.Date;

        var totalUsersTask = context.Users.AsNoTracking().CountAsync();
        var bannedUsersTask = context.Users.AsNoTracking().CountAsync(u => u.IsBanned);
        var activeChatsTask = context.ActiveChats.AsNoTracking().CountAsync();
        var queuedUsersTask = context.MatchmakingQueue.AsNoTracking().CountAsync();
        var totalReportsTask = context.Reports.AsNoTracking().CountAsync();
        var unresolvedReportsTask = context.Reports.AsNoTracking().CountAsync(r => !r.IsResolved);
        var registeredTodayTask = context.Users.AsNoTracking().CountAsync(u => u.CreatedAt >= todayUtc);

        await Task.WhenAll(
            totalUsersTask,
            bannedUsersTask,
            activeChatsTask,
            queuedUsersTask,
            totalReportsTask,
            unresolvedReportsTask,
            registeredTodayTask);

        return new AdminStats
        {
            TotalUsers = totalUsersTask.Result,
            BannedUsers = bannedUsersTask.Result,
            ActiveChats = activeChatsTask.Result,
            QueuedUsers = queuedUsersTask.Result,
            TotalReports = totalReportsTask.Result,
            UnresolvedReports = unresolvedReportsTask.Result,
            RegisteredToday = registeredTodayTask.Result
        };
    }
}
