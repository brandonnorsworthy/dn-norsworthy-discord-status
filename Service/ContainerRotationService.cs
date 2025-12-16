using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    public sealed class ContainerRotationService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly IConfiguration _cfg;
        private readonly ILogger<ContainerRotationService> _log;
        private readonly IInternalApiService _internalApiService;

        private List<ZcogContainerModel>? _snapshot;
        private int _index = -1;

        private HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase);

        public ContainerRotationService(IConfiguration cfg, ILogger<ContainerRotationService> log, IInternalApiService internalApiService)
        {
            _cfg = cfg;
            _log = log;
            ReloadWhitelist();
            _internalApiService = internalApiService;
        }

        private void ReloadWhitelist()
        {
            // Example: "api,worker,redis" (names and/or id prefixes)
            var raw = _cfg[EnvKeys.ContainerWhitelist] ?? "";
            _whitelist = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private bool IsAllowed(ZcogContainerModel c)
        {
            if (_whitelist.Count == 0) return true; // if you want "must whitelist", change to false
            return _whitelist.Contains(c.Name)
                   || _whitelist.Any(w => c.Id.StartsWith(w, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOnline(ZcogContainerModel c) =>
            c.State.Equals("running", StringComparison.OrdinalIgnoreCase);

        public async Task<byte[]> GeneratePngAsync(
            string cacheKey,
            int cacheSeconds,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            JsonSerializerOptions jsonOpts,
            int hours,
            string? pinnedContainerSelector,
            CancellationToken ct)
        {
            if (cache.TryGetValue(cacheKey, out byte[]? existing) && existing != null)
                return existing;

            await _gate.WaitAsync(ct);
            try
            {
                if (cache.TryGetValue(cacheKey, out existing) && existing != null)
                    return existing;

                byte[] png;

                if (!string.IsNullOrWhiteSpace(pinnedContainerSelector))
                {
                    var card = await _internalApiService.FetchFromApi(httpClientFactory, pinnedContainerSelector, hours, jsonOpts, ct);
                    png = StatusCardRenderService.Render(card, width: 640, height: 420);
                }
                else
                {
                    var next = await GetNextOnlineWhitelistedAsync(httpClientFactory, jsonOpts, ct);

                    if (next == null)
                    {
                        png = StatusCardRenderService.RenderAllOffline(width: 640, height: 420);
                    }
                    else
                    {
                        var card = await _internalApiService.FetchFromApi(httpClientFactory, next.Id, hours, jsonOpts, ct);
                        png = StatusCardRenderService.Render(card, width: 640, height: 420);
                    }
                }

                cache.Set(cacheKey, png, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds)
                });

                return png;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ZcogContainerModel?> GetNextOnlineWhitelistedAsync(
            IHttpClientFactory httpClientFactory,
            JsonSerializerOptions jsonOpts,
            CancellationToken ct)
        {
            if (_snapshot == null || _snapshot.Count == 0)
            {
                ReloadWhitelist();
                var all = await _internalApiService.FetchSnapshotAsync(httpClientFactory, jsonOpts, ct);

                _snapshot = all.Where(IsAllowed)
                               .Where(IsOnline)
                               .ToList();

                _index = -1;
            }

            if (_snapshot.Count == 0)
            {
                // no online containers under whitelist
                _snapshot = null;
                _index = -1;
                return null;
            }

            _index++;

            if (_index >= _snapshot.Count)
            {
                // end of cycle: refetch snapshot next time
                _snapshot = null;
                _index = -1;

                return await GetNextOnlineWhitelistedAsync(httpClientFactory, jsonOpts, ct);
            }

            return _snapshot[_index];
        }
    }
}
