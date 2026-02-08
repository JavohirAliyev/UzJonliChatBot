using Microsoft.EntityFrameworkCore;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Services;
using UzJonliChatBot.Infrastructure.Persistence;
using UzJonliChatBot.Infrastructure.Persistence.Repositories;
using UzJonliChatBot.Infrastructure.Telegram;

namespace UzJonliChatBot.BotHost.Configuration;

/// <summary>
/// Configuration for application services.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Registers all application services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        // Register DbContext
        ConfigureDatabase(services, configuration, logger);

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IMatchmakingQueueRepository, MatchmakingQueueRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        logger.LogInformation("Registered repositories");

        // Register application services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAdminService, AdminService>();
        logger.LogInformation("Registered application services");

        // Register Telegram services
        var botClient = TelegramBotClientFactory.Create(configuration);
        services.AddSingleton(botClient);
        services.AddSingleton<TelegramUpdateHandler>();
        logger.LogInformation("Registered Telegram infrastructure services");

        // Register hosted services
        services.AddHostedService<TelegramService>();
        logger.LogInformation("Registered hosted services");

        return services;
    }

    private static void ConfigureDatabase(
        IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        logger.LogInformation("GetConnectionString returned: HasValue={HasValue}, Length={Length}",
            !string.IsNullOrWhiteSpace(connectionString), connectionString?.Length ?? 0);

        // Fallback to environment variable
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            logger.LogInformation("Falling back to env var ConnectionStrings__DefaultConnection: HasValue={HasValue}",
                !string.IsNullOrWhiteSpace(connectionString));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var availableKeys = string.Join(',',
                configuration.AsEnumerable().Select(kvp => kvp.Key).Take(50));
            logger.LogCritical("Connection string 'DefaultConnection' not found. Available configuration keys: {Keys}",
                availableKeys);
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Provide 'ConnectionStrings:DefaultConnection' in configuration or " +
                "set environment variable 'ConnectionStrings__DefaultConnection'.");
        }

        services.AddDbContext<ChatBotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Enable retry logic for transient failures
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                // Set command timeout
                npgsqlOptions.CommandTimeout(30);
            })
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false));

        logger.LogInformation("Registered ChatBotDbContext with Npgsql");
    }
}
