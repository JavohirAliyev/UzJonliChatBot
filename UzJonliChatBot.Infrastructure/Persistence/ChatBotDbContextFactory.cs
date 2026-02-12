using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UzJonliChatBot.Infrastructure.Persistence;

public class ChatBotDbContextFactory : IDesignTimeDbContextFactory<ChatBotDbContext>
{
    public ChatBotDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        if (!File.Exists(Path.Combine(basePath, "appSettings.Development.json")))
        {
            basePath = Path.Combine(basePath, "..", "UzJonliChatBot.BotHost");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appSettings.Development.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found in appSettings.Development.json. " +
                "Please ensure your connection string is configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ChatBotDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ChatBotDbContext(optionsBuilder.Options);
    }
}
