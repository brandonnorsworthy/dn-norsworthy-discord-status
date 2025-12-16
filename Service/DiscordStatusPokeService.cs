using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StatusImageCard.Service
{
    public sealed class DiscordStatusPokeService : BackgroundService
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _cfg;
        private readonly ILogger<DiscordStatusPokeService> _log;

        public DiscordStatusPokeService(IHttpClientFactory factory, IConfiguration cfg, ILogger<DiscordStatusPokeService> log)
        {
            _factory = factory;
            _cfg = cfg;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await UpdateDiscordMessageAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Discord webhook update failed.");
                }
            }
        }

        private async Task UpdateDiscordMessageAsync(CancellationToken ct)
        {
            var webhookUrl = _cfg["DISCORD_WEBHOOK_URL"];
            var messageId = _cfg["DISCORD_MESSAGE_ID"];
            var imageBaseUrl = _cfg["STATUS_IMAGE_URL"];

            if (string.IsNullOrWhiteSpace(webhookUrl) ||
                string.IsNullOrWhiteSpace(messageId) ||
                string.IsNullOrWhiteSpace(imageBaseUrl))
            {
                _log.LogInformation("DiscordStatusPokeService skipped: missing DISCORD_WEBHOOK_URL, DISCORD_MESSAGE_ID, or STATUS_IMAGE_URL.");
                return;
            }

            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var separator = imageBaseUrl.Contains('?') ? "&" : "?";
            var imageUrl = $"{imageBaseUrl}{separator}v={cacheBust}";

            // Minimal payload: update embed image URL (keep your existing embed fields if you have them)
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Current Server Status",
                        image = new { url = imageUrl },
                        content = $"Status Generated at < t:{cacheBust}>, Current View: x",
                        color = 3066993,
                    }
                }
            };

            var client = _factory.CreateClient("discord");

            var editUrl = $"{webhookUrl}/messages/{messageId}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, editUrl)
            {
                Content = JsonContent.Create(payload)
            };

            using var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }
}
