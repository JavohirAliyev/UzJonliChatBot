using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Services;

public class ReportService : IReportService
{
    private readonly IUserRepository _userRepository;
    private readonly IReportRepository _reportRepository;

    public ReportService(IUserRepository userRepository, IReportRepository reportRepository)
    {
        _userRepository = userRepository;
        _reportRepository = reportRepository;
    }

    public async Task<ReportSubmissionResult> ReportUserAsync(long reporterTelegramId, long reportedTelegramId, string? reason)
    {
        var reporter = await _userRepository.GetByTelegramIdAsync(reporterTelegramId);
        if (reporter == null)
        {
            return new ReportSubmissionResult { Success = false, Message = "Reporter user does not exist." };
        }

        var reported = await _userRepository.GetByTelegramIdAsync(reportedTelegramId);
        if (reported == null)
        {
            return new ReportSubmissionResult { Success = false, Message = "Reported user does not exist." };
        }

        if (reporter.Id == reported.Id)
        {
            return new ReportSubmissionResult { Success = false, Message = "You cannot report yourself." };
        }

        var isDuplicate = await _reportRepository.HasRecentDuplicateAsync(reporter.Id, reported.Id, TimeSpan.FromHours(24));
        if (isDuplicate)
        {
            return new ReportSubmissionResult
            {
                Success = false,
                Message = "You have already reported this user in the last 24 hours."
            };
        }

        var report = new Report
        {
            ReporterUserId = reporter.Id,
            ReportedUserId = reported.Id,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        };

        await _reportRepository.AddReportAsync(report);

        return new ReportSubmissionResult { Success = true, Message = "Report submitted successfully." };
    }
}
