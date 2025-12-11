using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var host = CreateHostBuilder(args).Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Host built. Starting application...");

        // Initialize database before starting hosted services
        using (var scope = host.Services.CreateScope())
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

        // Set up graceful shutdown on Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            logger.LogInformation("Cancellation requested via Console.CancelKeyPress.");
        };

        try
        {
            logger.LogInformation("Starting hosted services...");
            // RunAsync will start all IHostedService implementations (TelegramService, HealthCheckHostedService)
            await host.RunAsync(cts.Token);
            logger.LogInformation("Application stopped gracefully.");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Application stopped by cancellation.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while running application.");
            throw;
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;

                config.SetBasePath(env.ContentRootPath)
                    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var tempLogger = loggerFactory.CreateLogger("Startup");

                // Register DbContext
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                tempLogger.LogInformation("GetConnectionString returned: HasValue={HasValue}, Length={Length}", !string.IsNullOrWhiteSpace(connectionString), connectionString?.Length ?? 0);

                // Also check environment-override commonly used in containers and Azure
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
                    tempLogger.LogInformation("Falling back to env var ConnectionStrings__DefaultConnection: HasValue={HasValue}", !string.IsNullOrWhiteSpace(connectionString));
                }

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    // As a last resort dump available configuration keys for diagnostics (no values)
                    tempLogger.LogCritical("Connection string 'DefaultConnection' not found. Available configuration keys: {Keys}", string.Join(',', context.Configuration.AsEnumerable().Select(kvp => kvp.Key).Take(50)));
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Provide 'ConnectionStrings:DefaultConnection' in configuration or set environment variable 'ConnectionStrings__DefaultConnection' (or Azure connection string named 'DefaultConnection').");
                }

                services.AddDbContext<ChatBotDbContext>(options =>
                    options.UseNpgsql(connectionString));

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
                var botClient = TelegramBotClientFactory.Create(context.Configuration);
                services.AddSingleton(botClient);
                services.AddSingleton<TelegramUpdateHandler>();
                tempLogger.LogInformation("Registered Telegram infrastructure services.");

                // Register hosted services (these will start automatically when host runs)
                services.AddHostedService<TelegramService>();
                tempLogger.LogInformation("Registered hosted services (TelegramService, HealthCheckHostedService).");
            });
}