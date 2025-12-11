using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ChatBotDbContext>();
            await DatabaseInitializationService.InitializeAsync(dbContext);
        }

        // Get the Telegram service and start the bot
        var telegramService = host.Services.GetRequiredService<TelegramService>();
        var cts = new CancellationTokenSource();

        // Handle graceful shutdown
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await telegramService.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Bot stopped.");
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("UzJonliChatBot.BotHost/appSettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Register database context
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = Environment.GetEnvironmentVariable("AZURE_POSTGRESQL_CONNECTIONSTRING");
                }

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Provide 'ConnectionStrings:DefaultConnection' in configuration or set environment variable 'ConnectionStrings__DefaultConnection' (or Azure connection string named 'DefaultConnection').");
                }

                services.AddDbContext<ChatBotDbContext>(options =>
                    options.UseNpgsql(connectionString));

                // Register repositories
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IChatRepository, ChatRepository>();
                services.AddScoped<IMatchmakingQueueRepository, MatchmakingQueueRepository>();

                // Register application services
                services.AddSingleton<IUserService, UserService>();
                services.AddSingleton<IRegistrationService, RegistrationService>();
                services.AddSingleton<IMatchmakingService, MatchmakingService>();
                services.AddSingleton<IChatService, ChatService>();

                // Register infrastructure services
                var botClient = TelegramBotClientFactory.Create(context.Configuration);
                services.AddSingleton(botClient);
                services.AddSingleton<TelegramUpdateHandler>();
                services.AddSingleton<TelegramService>();
            });
}