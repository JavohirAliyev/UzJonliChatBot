using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IReportService
{
    Task<ReportSubmissionResult> ReportUserAsync(long reporterTelegramId, long reportedTelegramId, string? reason);
}
