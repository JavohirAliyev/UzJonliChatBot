using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UzJonliChatBot.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory for migrations.
/// Used by dotnet ef commands without requiring the application to run.
/// </summary>
public class ChatBotDbContextFactory : IDesignTimeDbContextFactory<ChatBotDbContext>
{
    public ChatBotDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        
        // Navigate to BotHost directory if running from Infrastructure
        if (!File.Exists(Path.Combine(basePath, "appSettings.json")))
        {
            basePath = Path.Combine(basePath, "UzJonliChatBot.BotHost");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appSettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found in appSettings.json. " +
                "Please ensure your connection string is configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ChatBotDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ChatBotDbContext(optionsBuilder.Options);
    }
}
