using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Telegram.Bot.Types;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Services;
using UzJonliChatBot.Infrastructure.Persistence;
using UzJonliChatBot.Infrastructure.Persistence.Repositories;
using UzJonliChatBot.Infrastructure.Telegram;

namespace UzJonliChatBot.BotHost;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Use LoggerFactory directly instead of BuildServiceProvider to avoid duplicate service instances
        using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Building application...");

        ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        var app = builder.Build();

        logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Application built. Initializing database...");

        // Initialize database before starting hosted services
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                logger.LogInformation("Starting database initialization...");
                var dbContext = scope.ServiceProvider.GetRequiredService<ChatBotDbContext>();
                await DatabaseInitializationService.InitializeAsync(dbContext);
                logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Database initialization failed.");
                throw;
            }
        }

        // Configure webhook endpoint
        app.MapPost("/webhook", async (HttpContext context, TelegramUpdateHandler updateHandler, ILogger<Program> logger) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                
                if (string.IsNullOrEmpty(body))
                {
                    logger.LogWarning("Received empty webhook request");
                    return Results.BadRequest("Empty request body");
                }

                var update = JsonSerializer.Deserialize<Update>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (update == null)
                {
                    logger.LogWarning("Failed to deserialize webhook update");
                    return Results.BadRequest("Invalid update format");
                }

                // Process update asynchronously (fire and forget to respond quickly to Telegram)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await updateHandler.HandleUpdateAsync(update);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing webhook update {UpdateId}", update.Id);
                    }
                });

                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling webhook request");
                return Results.StatusCode(500);
            }
        });

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        logger.LogInformation("Starting web application...");
        await app.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var tempLogger = loggerFactory.CreateLogger("Startup");

        // Register DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        tempLogger.LogInformation("GetConnectionString returned: HasValue={HasValue}, Length={Length}", !string.IsNullOrWhiteSpace(connectionString), connectionString?.Length ?? 0);

        // Also check environment-override commonly used in containers
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            tempLogger.LogInformation("Falling back to env var ConnectionStrings__DefaultConnection: HasValue={HasValue}", !string.IsNullOrWhiteSpace(connectionString));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // As a last resort dump available configuration keys for diagnostics (no values)
            tempLogger.LogCritical("Connection string 'DefaultConnection' not found. Available configuration keys: {Keys}", string.Join(',', configuration.AsEnumerable().Select(kvp => kvp.Key).Take(50)));
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Provide 'ConnectionStrings:DefaultConnection' in configuration or set environment variable 'ConnectionStrings__DefaultConnection'.");
        }

        // Force IPv4 to avoid IPv6 connection issues (e.g., on Azure App Service)
        // Supabase connection strings may contain IPv6 addresses which some environments don't support
        connectionString = ForceIPv4Connection(connectionString);

        services.AddDbContext<ChatBotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Enable retry logic for transient failures
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                
                // Set command timeout to prevent hanging operations
                npgsqlOptions.CommandTimeout(30); // 30 seconds
            })
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false));

        tempLogger.LogInformation("Registered ChatBotDbContext with Npgsql.");

        // Register repositories as Scoped (they use scoped DbContext)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IMatchmakingQueueRepository, MatchmakingQueueRepository>();
        tempLogger.LogInformation("Registered repositories.");

        // Register application services as Scoped (they depend on scoped repositories)
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IChatService, ChatService>();
        tempLogger.LogInformation("Registered application services.");

        // Register infrastructure services
        var botClient = TelegramBotClientFactory.Create(configuration);
        services.AddSingleton(botClient);
        services.AddSingleton<TelegramUpdateHandler>();
        tempLogger.LogInformation("Registered Telegram infrastructure services.");

        // Register hosted services (these will start automatically when host runs)
        services.AddHostedService<TelegramService>();
        tempLogger.LogInformation("Registered hosted services (TelegramService).");
    }

    /// <summary>
    /// Forces IPv4 connection by resolving hostname to IPv4 address.
    /// This is necessary for environments that don't support IPv6 (e.g., Azure App Service).
    /// Supabase hostnames may resolve to IPv6 addresses which some environments can't connect to.
    /// </summary>
    private static string ForceIPv4Connection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        try
        {
            // Extract hostname from connection string (format: Host=hostname;Port=5432;...)
            var hostMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"Host=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (hostMatch.Success)
            {
                var hostname = hostMatch.Groups[1].Value.Trim();
                
                // Skip if it's already an IPv4 address
                if (System.Net.IPAddress.TryParse(hostname, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return connectionString;
                }

                // Skip if it's localhost or 127.0.0.1
                if (hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase) || 
                    hostname.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    return connectionString;
                }

                // Resolve hostname to get IPv4 address
                var hostEntry = System.Net.Dns.GetHostEntry(hostname);
                var ipv4Address = hostEntry.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                
                if (ipv4Address != null)
                {
                    // Replace hostname with IPv4 address in connection string
                    connectionString = System.Text.RegularExpressions.Regex.Replace(
                        connectionString,
                        @"Host=([^;]+)",
                        $"Host={ipv4Address}",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            // If DNS resolution fails, log warning but continue with original connection string
            // This allows the connection to proceed, though it may fail if IPv6 is not supported
            Console.WriteLine($"Warning: Could not resolve hostname to IPv4: {ex.Message}. Using original connection string.");
        }

        return connectionString;
    }
}