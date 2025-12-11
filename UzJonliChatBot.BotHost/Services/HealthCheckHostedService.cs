using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UzJonliChatBot.Infrastructure.Persistence;

namespace UzJonliChatBot.BotHost.Services;

public class HealthCheckHostedService : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HealthCheckHostedService> _logger;
    private readonly IConfiguration _configuration;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly int _port;

    public HealthCheckHostedService(IServiceProvider services, ILogger<HealthCheckHostedService> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
        _port = _configuration.GetValue<int?>("Health:Port") ?? 5005;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting HealthCheckHostedService on port {Port}", _port);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), CancellationToken.None);

        _logger.LogInformation("Health endpoint listening at http://localhost:{Port}/health and /healthz", _port);
        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var ctx = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(ctx), token);
            }
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 995 || ex.ErrorCode == 64)
        {
            // listener closed
            _logger.LogInformation("HttpListener closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in health listener loop");
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        _logger.LogInformation("Health endpoint hit: {Path} from {Remote}", path, ctx.Request.RemoteEndPoint);

        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) || path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            var result = new Dictionary<string, object>();
            var healthy = true;

            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChatBotDbContext>();
                var canConnect = await db.Database.CanConnectAsync();
                result["database"] = new { status = canConnect ? "Healthy" : "Unhealthy" };
                _logger.LogInformation("Health check - database.CanConnect: {CanConnect}", canConnect);
                healthy &= canConnect;

                // simple check for Telegram client registration
                var botClient = scope.ServiceProvider.GetService(typeof(Telegram.Bot.TelegramBotClient));
                var botOk = botClient != null;
                result["telegramClientRegistered"] = new { status = botOk ? "Healthy" : "Unhealthy" };
                _logger.LogInformation("Health check - telegram client registered: {BotOk}", botOk);
                healthy &= botOk;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during health checks");
                result["exception"] = ex.Message;
                healthy = false;
            }

            var statusCode = healthy ? 200 : 503;
            var payload = JsonSerializer.Serialize(new { status = healthy ? "Healthy" : "Unhealthy", checks = result }, new JsonSerializerOptions { WriteIndented = true });
            var buffer = Encoding.UTF8.GetBytes(payload);
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = buffer.Length;
            await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }
        else
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping HealthCheckHostedService...");
        _cts?.Cancel();

        try
        {
            _listener?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing HttpListener");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _listener?.Close();
        _cts?.Dispose();
    }
}