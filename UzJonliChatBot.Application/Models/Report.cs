namespace UzJonliChatBot.Application.Models;

public class Report
{
    public long Id { get; set; }
    public long ReporterUserId { get; set; }
    public long ReportedUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Report()
    {
        CreatedAt = DateTime.UtcNow;
        IsResolved = false;
    }
}

public class ReportView
{
    public long Id { get; set; }
    public long ReporterTelegramId { get; set; }
    public string? ReporterUsername { get; set; }
    public long ReportedTelegramId { get; set; }
    public string? ReportedUsername { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ReportSubmissionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
