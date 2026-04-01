using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UzJonliChatBot.Infrastructure.Persistence;

public class ChatBotDbContextFactory : IDesignTimeDbContextFactory<ChatBotDbContext>
{
    public ChatBotDbContext CreateDbContext(string[] args)
    {
        var currentPath = Directory.GetCurrentDirectory();
        var hostPath = Path.Combine(currentPath, "..", "UzJonliChatBot.BotHost");
        var basePath = Directory.Exists(hostPath) ? hostPath : currentPath;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. " +
                "Set 'ConnectionStrings:DefaultConnection' or environment variable 'ConnectionStrings__DefaultConnection'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ChatBotDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ChatBotDbContext(optionsBuilder.Options);
    }
}
