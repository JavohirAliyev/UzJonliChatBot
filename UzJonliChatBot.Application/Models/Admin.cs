namespace UzJonliChatBot.Application.Models;

/// <summary>
/// Represents an admin user who can access the admin dashboard.
/// </summary>
public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public Admin()
    {
        CreatedAt = DateTime.UtcNow;
    }
}
