using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StatusImageCard.Config;
using StatusImageCard.Service;
using System.Net;

namespace StatusImageCard.Discord;

public sealed class DiscordStatusUpdaterService : BackgroundService
{
    private readonly ILogger<DiscordStatusUpdaterService> _log;
    private readonly IConfiguration _cfg;
    private readonly DiscordWebhookClient _discord;
    private readonly DiscordMessageIdStore _store;

    private readonly IStatusImageService _statusImages;

    public DiscordStatusUpdaterService(
        ILogger<DiscordStatusUpdaterService> log,
        IConfiguration cfg,
        DiscordWebhookClient discord,
        IHostEnvironment env,
        IStatusImageService statusImages)
    {
        _log = log;
        _cfg = cfg;
        _discord = discord;

        _statusImages = statusImages;

        // Always file-based; no env for message id.
        var path = Path.Combine(env.ContentRootPath, "data", "message-id.txt");
        _store = new DiscordMessageIdStore(path);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

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

        const string fileName = "status.png";

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Always render fresh for Discord; the HTTP endpoint's cache is not used here.
        var result = await _statusImages.RenderGridAsync(ct);
        var pngBytes = result.PngBytes;

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = "Current Server Status",
                    description = DiscordDescriptionBuilder.Build(nowUnix, result.OnlineServers, result.OfflineServers),
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
