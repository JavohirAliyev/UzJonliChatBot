using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IAdminStatsService
{
    Task<AdminStats> GetStatsAsync();
}
