namespace UzJonliChatBot.Application.Models;

/// <summary>
/// Gender enum for user classification.
/// </summary>
public enum Gender
{
    Male,
    Female
}

/// <summary>
/// User registration status in the system.
/// </summary>
public enum UserRegistrationStatus
{
    NotStarted,           // User hasn't started registration
    GenderPending,        // Waiting for gender selection
    AgeVerificationPending, // Waiting for age confirmation
    Registered            // User is fully registered
}

/// <summary>
/// Represents a Telegram user in the chat system.
/// Stores user gender for future search filtering.
/// </summary>
public class User
{
    public long TelegramId { get; set; }
    public Gender? Gender { get; set; }
    public bool IsAgeVerified { get; set; }
    public UserRegistrationStatus RegistrationStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User()
    {
        RegistrationStatus = UserRegistrationStatus.NotStarted;
        IsAgeVerified = false;
        CreatedAt = DateTime.UtcNow;
    }
}
