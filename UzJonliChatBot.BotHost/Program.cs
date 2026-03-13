using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Telegram.Bot.Types;
using UzJonliChatBot.BotHost.Api;
using UzJonliChatBot.BotHost.Configuration;
using UzJonliChatBot.Infrastructure.Persistence;
using UzJonliChatBot.Infrastructure.Telegram;

namespace UzJonliChatBot.BotHost;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Building application...");

        builder.Services.AddApplicationServices(builder.Configuration, logger);
        builder.Services.AddJwtAuthentication(builder.Configuration);

        var app = builder.Build();

        logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Application built. Initializing database...");

        await InitializeDatabaseAsync(app, logger);

        ConfigureMiddleware(app);

        MapEndpoints(app, logger);

        logger.LogInformation("Starting web application...");
        await app.RunAsync();
    }

    private static async Task InitializeDatabaseAsync(WebApplication app, ILogger logger)
    {
        using var scope = app.Services.CreateScope();
        try
        {
            logger.LogInformation("Starting database initialization...");
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ChatBotDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            await DatabaseInitializationService.InitializeAsync(dbContext, configuration);
            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialization failed");
            throw;
        }
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(app.Environment.ContentRootPath, "wwwroot")),
            RequestPath = ""
        });

        app.UseAuthenticationAndAuthorization();
    }

    private static void MapEndpoints(WebApplication app, ILogger logger)
    {
        app.MapPost("/webhook", async (
            HttpContext context,
            TelegramUpdateHandler updateHandler,
            ILogger<Program> webhookLogger) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(body))
                {
                    webhookLogger.LogWarning("Received empty webhook request");
                    return Results.BadRequest("Empty request body");
                }

                var update = JsonSerializer.Deserialize<Update>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (update == null)
                {
                    webhookLogger.LogWarning("Failed to deserialize webhook update");
                    return Results.BadRequest("Invalid update format");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await updateHandler.HandleUpdateAsync(update);
                    }
                    catch (Exception ex)
                    {
                        webhookLogger.LogError(ex, "Error processing webhook update {UpdateId}", update.Id);
                    }
                });

                return Results.Ok();
            }
            catch (Exception ex)
            {
                webhookLogger.LogError(ex, "Error handling webhook request");
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        }));

        app.MapAdminEndpoints();

        app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));

        logger.LogInformation("Endpoints mapped successfully");
    }
}
