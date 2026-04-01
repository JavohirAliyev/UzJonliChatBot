using Microsoft.EntityFrameworkCore;
using UzJonliChatBot.Infrastructure.Persistence.Entities;

namespace UzJonliChatBot.Infrastructure.Persistence;

public class ChatBotDbContext(DbContextOptions<ChatBotDbContext> options) : DbContext(options)
{
    public DbSet<AdminEntity> Admins { get; set; } = null!;
    public DbSet<UserEntity> Users { get; set; } = null!;
    public DbSet<ActiveChatEntity> ActiveChats { get; set; } = null!;
    public DbSet<MatchmakingQueueEntity> MatchmakingQueue { get; set; } = null!;
    public DbSet<ReportEntity> Reports { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastLoginAt);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TelegramId).IsUnique();

            entity.Property(e => e.TelegramId).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Gender).IsRequired().HasMaxLength(10);
            entity.Property(e => e.IsPremium).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsAgeVerified).IsRequired();
            entity.Property(e => e.RegistrationStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsBanned).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);

            entity.HasMany(e => e.ChatsAsUser1)
                .WithOne(c => c.User1)
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ChatsAsUser2)
                .WithOne(c => c.User2)
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.QueueEntry)
                .WithOne(q => q.User)
                .HasForeignKey<MatchmakingQueueEntity>(q => q.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActiveChatEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.User1Id, e.User2Id }).IsUnique();

            entity.Property(e => e.User1Id).IsRequired();
            entity.Property(e => e.User2Id).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();

            entity.HasOne(e => e.User1)
                .WithMany(u => u.ChatsAsUser1)
                .HasForeignKey(e => e.User1Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User2)
                .WithMany(u => u.ChatsAsUser2)
                .HasForeignKey(e => e.User2Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchmakingQueueEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.GenderPreference).HasMaxLength(10);
            entity.Property(e => e.QueuedAt).IsRequired();

            entity.HasOne(e => e.User)
                .WithOne(u => u.QueueEntry)
                .HasForeignKey<MatchmakingQueueEntity>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.ToTable("Reports");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsResolved).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.ResolvedAt);

            entity.HasIndex(e => e.ReportedUserId);
            entity.HasIndex(e => new { e.ReporterUserId, e.ReportedUserId, e.CreatedAt });

            entity.HasOne(e => e.ReporterUser)
                .WithMany(u => u.ReportsFiled)
                .HasForeignKey(e => e.ReporterUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReportedUser)
                .WithMany(u => u.ReportsReceived)
                .HasForeignKey(e => e.ReportedUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
