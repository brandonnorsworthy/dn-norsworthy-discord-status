using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StatusImageCard.Config;
using System.Net;

namespace StatusImageCard.Discord;

public sealed class DiscordStatusUpdaterService : BackgroundService
{
    private readonly ILogger<DiscordStatusUpdaterService> _log;
    private readonly IConfiguration _cfg;
    private readonly DiscordWebhookClient _discord;
    private readonly DiscordMessageIdStore _store;
    private readonly IHttpClientFactory _httpClientFactory;

    public DiscordStatusUpdaterService(
        ILogger<DiscordStatusUpdaterService> log,
        IConfiguration cfg,
        DiscordWebhookClient discord,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment env)
    {
        _log = log;
        _cfg = cfg;
        _discord = discord;
        _httpClientFactory = httpClientFactory;

        // Always file-based; no env for message id.
        var path = Path.Combine(env.ContentRootPath, "data", "message-id.txt");
        _store = new DiscordMessageIdStore(path);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        // Run once immediately at startup
        await SafeUpdateOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SafeUpdateOnceAsync(stoppingToken);
        }
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
        var hostDomain = _cfg[EnvKeys.StatusImageUrl];

        if (string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(hostDomain))
        {
            _log.LogInformation("Discord updater skipped: missing {WebhookKey} or {HostKey}.",
                EnvKeys.DiscordWebhookUrl, EnvKeys.StatusImageUrl);
            return;
        }

        // Keep this aligned with your /status.png route
        const int hours = 6;

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // This URL is only used to FETCH the image bytes.
        var fetchUrl = $"{hostDomain.TrimEnd('/')}/status.png?h={hours}&t={cacheBust}";

        if (!fetchUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"STATUS_IMAGE_URL must be https for Discord embeds. Got: {fetchUrl}");

        _log.LogInformation("Fetching status image from: {FetchUrl}", fetchUrl);

        // Fetch image bytes so we can upload as an attachment
        var fetchClient = _httpClientFactory.CreateClient("image-fetch");
        var pngBytes = await fetchClient.GetByteArrayAsync(fetchUrl, ct);

        const string fileName = "status.png";

        var payload = new
        {
            // Optional top-level message content (leave null/omit if you don't want a message body)
            // content = "Current Server Status",
            embeds = new[]
            {
                new
                {
                    title = "Current Server Status",
                    description = $"Status Generated at <t:{nowUnix}>, Current View: Last {hours} hours",
                    image = new { url = $"attachment://{fileName}" },
                    color = 3066993
                }
            },
            // Important when editing: prevents old attachments sticking around
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
