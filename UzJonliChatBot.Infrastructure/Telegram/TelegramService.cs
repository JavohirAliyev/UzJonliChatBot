using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UzJonliChatBot.Infrastructure.Telegram;

/// <summary>
/// Main Telegram bot service that handles webhook setup and management.
/// </summary>
public class TelegramService : IHostedService, IDisposable
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramUpdateHandler _updateHandler;
    private readonly ILogger<TelegramService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public TelegramService(
        ITelegramBotClient botClient,
        TelegramUpdateHandler updateHandler,
        ILogger<TelegramService> logger,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Starts the bot and sets up webhook.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram bot service...");
        
        try
        {
            var me = await _botClient.GetMe(cancellationToken: cancellationToken);
            _logger.LogInformation("Bot started: @{Username} (ID: {Id})", me.Username, me.Id);

            // Set up bot commands for dropdown menu
            await SetupCommandsAsync(cancellationToken);

            // Set up webhook
            await SetupWebhookAsync(cancellationToken);
            
            _logger.LogInformation("Telegram bot service started successfully with webhook.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Telegram bot service.");
            throw;
        }
    }

    /// <summary>
    /// Sets up the webhook with Telegram.
    /// </summary>
    private async Task SetupWebhookAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get webhook URL from configuration
            var webhookUrl = _configuration.GetSection("TelegramBot:WebhookUrl").Value;
            
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                // Try to construct from environment (common in Azure)
                var baseUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") 
                    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(';').FirstOrDefault()?.Replace("http://", "").Replace("https://", "");
                
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    // Determine if HTTPS should be used (Azure Web Apps use HTTPS by default)
                    var useHttps = !baseUrl.Contains("localhost") && !baseUrl.Contains("127.0.0.1");
                    var protocol = useHttps ? "https" : "http";
                    
                    // Remove port if present (Azure handles this)
                    if (baseUrl.Contains(':'))
                    {
                        baseUrl = baseUrl.Split(':')[0];
                    }
                    
                    webhookUrl = $"{protocol}://{baseUrl}/webhook";
                    _logger.LogInformation("Constructed webhook URL from environment: {WebhookUrl}", webhookUrl);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Webhook URL not configured. Set 'TelegramBot:WebhookUrl' in configuration or provide WEBSITE_HOSTNAME environment variable.");
                }
            }

            // Telegram API expects update type names like "message" and "callback_query" (note the underscore).
            // Using Enum.ToString().ToLowerInvariant() yields "callbackquery" which Telegram will ignore.
            // Build the allowed_updates array with correct API names.
            var allowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery };
            var allowedUpdatesArray = allowedUpdates
                .Select(u => u switch
                {
                    UpdateType.CallbackQuery => "callback_query",
                    _ => u.ToString().ToLowerInvariant()
                })
                .ToArray();
            
            // Get bot token
            var botToken = _configuration.GetSection("TelegramBot:Token").Value
                ?? throw new InvalidOperationException("Telegram bot token is not configured.");
            
            // Set webhook using direct HTTP call to Telegram API
            var apiUrl = $"https://api.telegram.org/bot{botToken}/setWebhook";
            var payload = new
            {
                url = webhookUrl,
                allowed_updates = allowedUpdatesArray
            };
            
            var response = await _httpClient.PostAsJsonAsync(apiUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (result.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                _logger.LogInformation("Webhook set successfully: {WebhookUrl}", webhookUrl);
            }
            else
            {
                var description = result.TryGetProperty("description", out var desc) ? desc.GetString() : "Unknown error";
                throw new InvalidOperationException($"Failed to set webhook: {description}");
            }

            // Verify webhook info
            var infoUrl = $"https://api.telegram.org/bot{botToken}/getWebhookInfo";
            var infoResponse = await _httpClient.GetAsync(infoUrl, cancellationToken);
            infoResponse.EnsureSuccessStatusCode();
            var webhookInfo = await infoResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            
            if (webhookInfo.TryGetProperty("result", out var resultObj))
            {
                var url = resultObj.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : "N/A";
                var pendingCount = resultObj.TryGetProperty("pending_update_count", out var pendingProp) ? pendingProp.GetInt32() : 0;
                var lastError = resultObj.TryGetProperty("last_error_date", out var errorDateProp) ? errorDateProp.GetInt64() : (long?)null;
                
                _logger.LogInformation("Webhook info - URL: {Url}, Pending updates: {PendingUpdateCount}, Last error: {LastErrorDate}", 
                    url, pendingCount, lastError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up webhook.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram bot service...");
        
        try
        {
            // Delete webhook on shutdown using direct HTTP call
            var botToken = _configuration.GetSection("TelegramBot:Token").Value;
            if (!string.IsNullOrWhiteSpace(botToken))
            {
                var apiUrl = $"https://api.telegram.org/bot{botToken}/deleteWebhook";
                var response = await _httpClient.PostAsync(apiUrl, null, cancellationToken);
                response.EnsureSuccessStatusCode();
                _logger.LogInformation("Webhook deleted successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while deleting webhook during shutdown.");
        }
        
        _logger.LogInformation("Telegram bot service stopped.");
    }

    /// <summary>
    /// Sets up bot commands for the dropdown menu.
    /// </summary>
    private async Task SetupCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var commands = new[]
            {
                new BotCommand { Command = "start", Description = "Ro'yxatdan o'tish" },
                new BotCommand { Command = "keyingi", Description = "Yangi suhbatni boshlash" },
                new BotCommand { Command = "stop", Description = "Suhbatni tugatish" }
            };

            await _botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
            _logger.LogInformation("Bot commands registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting up bot commands. Bot will continue without custom commands.");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
