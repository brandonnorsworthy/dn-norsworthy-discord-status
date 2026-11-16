using System.Text.Json;
using StatusImageCard.Models;

namespace StatusImageCard.Api
{
    public sealed class InternalApiService : IInternalApiService
    {
        public const string ClientName = "api";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonOpts;

        public InternalApiService(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts)
        {
            _httpClientFactory = httpClientFactory;
            _jsonOpts = jsonOpts;
        }

        public async Task<List<ZcogContainerModel>> FetchSnapshotAsync(CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(ClientName);

            using var resp = await client.GetAsync("/api/containers/stats/", ct);
            resp.EnsureSuccessStatusCode();

            var list = await resp.Content.ReadFromJsonAsync<List<ZcogContainerModel>>(_jsonOpts, ct) ?? [];

            // Keep API order, but drop items missing id or name.
            return list.Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name)).ToList();
        }

        public async Task<List<ZcogContainerModel>> FetchHistoryAsync(string containerId, int hours, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(ClientName);

            using var resp = await client.GetAsync($"/api/containers/stats/{Uri.EscapeDataString(containerId)}?h={hours}", ct);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadFromJsonAsync<List<ZcogContainerModel>>(_jsonOpts, ct) ?? [];
        }
    }
}
