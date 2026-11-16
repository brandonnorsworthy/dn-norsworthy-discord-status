using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    /// <summary>The rendered grid plus the server names it shows, for the Discord description.</summary>
    public sealed record StatusImageResult(
        byte[] PngBytes,
        IReadOnlyList<string> OnlineServers,
        IReadOnlyList<string> OfflineServers,
        bool IsFallback);

    public interface IStatusImageService
    {
        /// <summary>Renders a fresh grid of every whitelisted server. Never throws for monitor failures: returns the fallback card instead.</summary>
        Task<StatusImageResult> RenderGridAsync(CancellationToken ct);

        /// <summary>Renders one card for the whitelisted container matching the selector (name or id prefix). Throws <see cref="KeyNotFoundException"/> if no whitelisted container matches.</summary>
        Task<byte[]> RenderSingleAsync(string selector, CancellationToken ct);
    }

    public sealed class StatusImageService : IStatusImageService
    {
        public const int HistoryHours = 6;
        public const int MaxConcurrentHistoryCalls = 4;

        private readonly IInternalApiService _api;
        private readonly IConfiguration _cfg;
        private readonly ILogger<StatusImageService> _log;

        public StatusImageService(IInternalApiService api, IConfiguration cfg, ILogger<StatusImageService> log)
        {
            _api = api;
            _cfg = cfg;
            _log = log;
        }

        public async Task<StatusImageResult> RenderGridAsync(CancellationToken ct)
        {
            List<ZcogContainerModel> all;
            try
            {
                all = await _api.FetchSnapshotAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogError(ex, "Monitor API snapshot failed; rendering the fallback card.");
                return Fallback();
            }

            var whitelist = ContainerSelection.ParseWhitelist(_cfg[EnvKeys.ContainerWhitelist]);
            var selected = ContainerSelection.SelectAndOrder(all, whitelist);
            if (selected.Count == 0)
            {
                _log.LogInformation("No whitelisted containers found; rendering the fallback card.");
                return Fallback();
            }

            var cards = await BuildCardsAsync(selected, ct);
            var png = StatusCardRenderService.RenderGrid(cards);

            return new StatusImageResult(
                png,
                selected.Where(ContainerSelection.IsOnline).Select(c => c.Name).ToList(),
                selected.Where(c => !ContainerSelection.IsOnline(c)).Select(c => c.Name).ToList(),
                IsFallback: false);
        }

        public async Task<byte[]> RenderSingleAsync(string selector, CancellationToken ct)
        {
            var all = await _api.FetchSnapshotAsync(ct);
            var whitelist = ContainerSelection.ParseWhitelist(_cfg[EnvKeys.ContainerWhitelist]);
            var current = ContainerSelection.FindWhitelisted(all, whitelist, selector)
                          ?? throw new KeyNotFoundException("No whitelisted container matches the selector.");

            var cards = await BuildCardsAsync([current], ct);
            return StatusCardRenderService.Render(cards[0], StatusCardRenderService.TileWidth, StatusCardRenderService.TileHeight);
        }

        private async Task<ContainerCard[]> BuildCardsAsync(IReadOnlyList<ZcogContainerModel> containers, CancellationToken ct)
        {
            var cards = new ContainerCard[containers.Count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentHistoryCalls, CancellationToken = ct };

            await Parallel.ForEachAsync(Enumerable.Range(0, containers.Count), options, async (i, token) =>
            {
                var c = containers[i];
                IReadOnlyList<ZcogContainerModel>? history = null;

                if (ContainerSelection.IsOnline(c))
                {
                    try
                    {
                        history = await _api.FetchHistoryAsync(c.Id, HistoryHours, token);
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        _log.LogWarning(ex, "History fetch failed for {Container}; rendering its tile without a graph.", c.Name);
                    }
                }

                cards[i] = ContainerCardMapper.Map(c, history, HistoryHours);
            });

            return cards;
        }

        private static StatusImageResult Fallback() => new(
            StatusCardRenderService.RenderAllOffline(StatusCardRenderService.TileWidth, StatusCardRenderService.TileHeight),
            [],
            [],
            IsFallback: true);
    }
}
