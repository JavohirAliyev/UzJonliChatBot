namespace UzJonliChatBot.Application.Models;

public enum Gender
{
    Male,
    Female
}

public enum UserRegistrationStatus
{
    NotStarted,
    GenderPending,
    AgeVerificationPending,
    Registered
}

public class User
{
    public long Id { get; set; }
    public long TelegramId { get; set; }
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public Gender Gender { get; set; }
    public bool IsPremium { get; set; }
    public bool IsAgeVerified { get; set; }
    public UserRegistrationStatus RegistrationStatus { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User()
    {
        Gender = Gender.Male;
        IsPremium = false;
        RegistrationStatus = UserRegistrationStatus.NotStarted;
        IsAgeVerified = false;
        IsBanned = false;
        CreatedAt = DateTime.UtcNow;
    }
}
