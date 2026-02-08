namespace UzJonliChatBot.Infrastructure.Persistence.Entities;

/// <summary>
/// Admin entity for database storage.
/// </summary>
public class AdminEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// User entity for database storage.
/// </summary>
public class UserEntity
{
    public long Id { get; set; }
    public long TelegramId { get; set; }
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string Gender { get; set; } = null!;
    public bool IsAgeVerified { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<ActiveChatEntity> ChatsAsUser1 { get; set; } = new List<ActiveChatEntity>();
    public virtual ICollection<ActiveChatEntity> ChatsAsUser2 { get; set; } = new List<ActiveChatEntity>();
    public virtual MatchmakingQueueEntity? QueueEntry { get; set; }
}

/// <summary>
/// Active chat entity for database storage.
/// </summary>
public class ActiveChatEntity
{
    public long Id { get; set; }
    public long User1Id { get; set; }
    public long User2Id { get; set; }
    public DateTime StartedAt { get; set; }

    // Navigation properties
    public virtual UserEntity User1 { get; set; } = null!;
    public virtual UserEntity User2 { get; set; } = null!;
}

/// <summary>
/// Matchmaking queue entity for database storage.
/// </summary>
public class MatchmakingQueueEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public DateTime QueuedAt { get; set; }

    // Navigation property
    public virtual UserEntity User { get; set; } = null!;
}
