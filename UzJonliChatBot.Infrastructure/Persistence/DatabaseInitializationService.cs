using Microsoft.EntityFrameworkCore;
using UzJonliChatBot.Infrastructure.Persistence;

namespace UzJonliChatBot.Infrastructure.Persistence;

/// <summary>
/// Service for initializing the database (migrations, etc).
/// </summary>
public static class DatabaseInitializationService
{
    /// <summary>
    /// Initializes the database by applying pending migrations.
    /// </summary>
    public static async Task InitializeAsync(ChatBotDbContext context)
    {
        try
        {
            // Apply pending migrations
            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                await context.Database.MigrateAsync();
                Console.WriteLine("Database migrations applied successfully.");
            }
            else
            {
                Console.WriteLine("Database is up to date.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing database: {ex.Message}");
            throw;
        }
    }
}
