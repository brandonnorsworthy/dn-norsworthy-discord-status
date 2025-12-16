using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StatusImageCard.Config;
using StatusImageCard.Service;
using System.Net;
using System.Text.Json;

namespace StatusImageCard.Discord;

public sealed class DiscordStatusUpdaterService : BackgroundService
{
    private readonly ILogger<DiscordStatusUpdaterService> _log;
    private readonly IConfiguration _cfg;
    private readonly DiscordWebhookClient _discord;
    private readonly DiscordMessageIdStore _store;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IContainerRotationService _rotation;
    private readonly IMemoryCache _cache;
    private readonly JsonSerializerOptions _jsonOpts;

    public DiscordStatusUpdaterService(
        ILogger<DiscordStatusUpdaterService> log,
        IConfiguration cfg,
        DiscordWebhookClient discord,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment env,
        IContainerRotationService containerRotationService,
        IMemoryCache cache,
        JsonSerializerOptions jsonOpts)
    {
        _log = log;
        _cfg = cfg;
        _discord = discord;

        _httpClientFactory = httpClientFactory;
        _rotation = containerRotationService;
        _cache = cache;
        _jsonOpts = jsonOpts;

        // Always file-based; no env for message id.
        var path = Path.Combine(env.ContentRootPath, "data", "message-id.txt");
        _store = new DiscordMessageIdStore(path);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        await SafeUpdateOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SafeUpdateOnceAsync(stoppingToken);
    }

    private async Task SafeUpdateOnceAsync(CancellationToken ct)
    {
        try
        {
            await UpdateOnceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error updating Discord status.");
        }
    }

    private async Task UpdateOnceAsync(CancellationToken ct)
    {
        var webhookUrl = _cfg[EnvKeys.DiscordWebhookUrl];

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _log.LogInformation("Discord updater skipped: missing {WebhookKey}.", EnvKeys.DiscordWebhookUrl);
            return;
        }

        const int hours = 6;
        const int cacheSeconds = 60;
        const string fileName = "status.png";

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cacheKey = $"status-png:rotating:{hours}";

        var result = await _rotation.GenerateStatusAsync(
            cacheKey: cacheKey,
            cacheSeconds: cacheSeconds,
            cache: _cache,
            httpClientFactory: _httpClientFactory,
            jsonOpts: _jsonOpts,
            hours: hours,
            pinnedContainerSelector: null,
            ct: ct);

        var currentServer = result.CurrentlyShowing;
        var onlineServers = result.OnlineServers;
        var pngBytes = result.PngBytes;

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = "Current Server Status",
                    description =
                        $"Generated at <t:{nowUnix}>\n" +
                        $"Current Online Servers: {string.Join(", ", onlineServers)}",
                    image = new { url = $"attachment://{fileName}" },
                    color = 3066993
                }
            },
            // Prevent old attachments sticking around on edits.
            attachments = Array.Empty<object>()
        };

        if (!_store.TryRead(out var messageId))
        {
            _log.LogInformation("Message ID file not found. Sending new message...");
            var id = await _discord.SendNewWithFileAsync(webhookUrl, payload, pngBytes, fileName, "image/png", ct);
            _store.Write(id);
            _log.LogInformation("Message sent. ID saved: {MessageId}", id);
            return;
        }

        var status = await _discord.EditWithFileAsync(webhookUrl, messageId, payload, pngBytes, fileName, "image/png", ct);

        if (status == HttpStatusCode.NotFound)
        {
            _log.LogWarning("Discord message not found (404). Deleting ID and sending a new message...");
            _store.DeleteIfExists();

            var id = await _discord.SendNewWithFileAsync(webhookUrl, payload, pngBytes, fileName, "image/png", ct);
            _store.Write(id);
            _log.LogInformation("Message re-sent. New ID saved: {MessageId}", id);
            return;
        }

        _log.LogInformation("Message edited. ID: {MessageId}", messageId);
    }
}
