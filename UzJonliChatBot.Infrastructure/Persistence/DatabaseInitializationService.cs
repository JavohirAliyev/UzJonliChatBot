using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using UzJonliChatBot.Infrastructure.Persistence;
using UzJonliChatBot.Infrastructure.Persistence.Entities;

namespace UzJonliChatBot.Infrastructure.Persistence;

public static class DatabaseInitializationService
{
    public static async Task InitializeAsync(ChatBotDbContext context, IConfiguration? configuration = null)
    {
        try
        {
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                await context.Database.MigrateAsync();
                Console.WriteLine("Database migrations applied successfully.");
            }
            else
            {
                Console.WriteLine("Database is up to date.");
            }

            await InitializeAdminAsync(context, configuration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing database: {ex.Message}");
            throw;
        }
    }

    private static async Task InitializeAdminAsync(ChatBotDbContext context, IConfiguration? configuration)
    {
        if (await context.Admins.AnyAsync())
        {
            Console.WriteLine("Admin account already exists.");
            return;
        }

        var username = configuration?["Admin:Username"] ?? "admin";
        var password = configuration?["Admin:Password"] ?? "Admin123!";

        var passwordHash = HashPassword(password);

        var admin = new AdminEntity
        {
            Username = username,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };

        context.Admins.Add(admin);
        await context.SaveChangesAsync();

        Console.WriteLine($"Initial admin created with username: {username}");
        Console.WriteLine("IMPORTANT: Please change the default admin password after first login!");
    }

    private static string HashPassword(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        var hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);

        return Convert.ToBase64String(hashBytes);
    }
}
