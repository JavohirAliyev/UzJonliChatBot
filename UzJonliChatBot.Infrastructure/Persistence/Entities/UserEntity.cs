namespace UzJonliChatBot.Infrastructure.Persistence.Entities;

public class AdminEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserEntity
{
    public long Id { get; set; }
    public long TelegramId { get; set; }
    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string Gender { get; set; } = null!;
    public bool IsPremium { get; set; }
    public bool IsAgeVerified { get; set; }
    public string RegistrationStatus { get; set; } = null!;
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ActiveChatEntity> ChatsAsUser1 { get; set; } = new List<ActiveChatEntity>();
    public virtual ICollection<ActiveChatEntity> ChatsAsUser2 { get; set; } = new List<ActiveChatEntity>();
    public virtual ICollection<ReportEntity> ReportsFiled { get; set; } = new List<ReportEntity>();
    public virtual ICollection<ReportEntity> ReportsReceived { get; set; } = new List<ReportEntity>();
    public virtual MatchmakingQueueEntity? QueueEntry { get; set; }
}

public class ActiveChatEntity
{
    public long Id { get; set; }
    public long User1Id { get; set; }
    public long User2Id { get; set; }
    public DateTime StartedAt { get; set; }

    public virtual UserEntity User1 { get; set; } = null!;
    public virtual UserEntity User2 { get; set; } = null!;
}

public class MatchmakingQueueEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? GenderPreference { get; set; }
    public DateTime QueuedAt { get; set; }

    public virtual UserEntity User { get; set; } = null!;
}

public class ReportEntity
{
    public long Id { get; set; }
    public long ReporterUserId { get; set; }
    public long ReportedUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual UserEntity ReporterUser { get; set; } = null!;
    public virtual UserEntity ReportedUser { get; set; } = null!;
}
