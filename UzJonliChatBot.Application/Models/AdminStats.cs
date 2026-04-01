namespace UzJonliChatBot.Application.Models;

public class AdminStats
{
    public int TotalUsers { get; set; }
    public int BannedUsers { get; set; }
    public int ActiveChats { get; set; }
    public int QueuedUsers { get; set; }
    public int TotalReports { get; set; }
    public int UnresolvedReports { get; set; }
    public int RegisteredToday { get; set; }
}
