using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Api;
using StatusImageCard.Models;
using System.Text.Json;

namespace StatusImageCard.Service
{
    public sealed class ContainerRotationService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        private List<ZcogContainerModel>? _snapshot;
        private int _index = -1; // so first "next" becomes 0

        public async Task<byte[]> GeneratePngAsync(string cacheKey, int cacheSeconds, IMemoryCache cache, IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, int hours, string? pinnedContainerSelector, CancellationToken ct)
        {
            // Double-checked locking pattern: if another request already filled the cache, return it.
            if (cache.TryGetValue(cacheKey, out byte[]? existing) && existing != null)
                return existing;

            await _gate.WaitAsync(ct);
            try
            {
                // Check again after acquiring lock
                if (cache.TryGetValue(cacheKey, out existing) && existing != null)
                    return existing;

                ContainerCard card;

                if (!string.IsNullOrWhiteSpace(pinnedContainerSelector))
                {
                    // "Pinned" mode (your existing behavior)
                    card = await ZcogApi.FetchFromApi(httpClientFactory, pinnedContainerSelector, hours, jsonOpts, ct);
                }
                else
                {
                    // Rotating mode: advance through a snapshot list
                    var next = await GetNextContainerFromSnapshotAsync(httpClientFactory, jsonOpts, ct);
                    card = await ZcogApi.FetchFromApi(httpClientFactory, next.Id, hours, jsonOpts, ct); // select by id
                }

                var png = StatusCardRenderService.Render(card, width: 640, height: 420);

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

        private async Task<ZcogContainerModel> GetNextContainerFromSnapshotAsync(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, CancellationToken ct)
        {
            if (_snapshot == null || _snapshot.Count == 0)
            {
                _snapshot = await ZcogApi.FetchSnapshotAsync(httpClientFactory, jsonOpts, ct);
                _index = -1;
            }

            _index++;

            // If we hit the end, expire the snapshot and refetch on the next rotation
            if (_index >= _snapshot.Count)
            {
                _snapshot = null;
                _index = -1;

                _snapshot = await ZcogApi.FetchSnapshotAsync(httpClientFactory, jsonOpts, ct);
                _index = 0;
            }

            return _snapshot[_index];
        }
    }
}
