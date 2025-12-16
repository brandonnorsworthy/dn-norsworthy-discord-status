using Microsoft.Extensions.Caching.Memory;
using StatusImageCard.Models;
using System.Text.Json;

namespace StatusImageCard.Service
{
    public interface IContainerRotationService
    {
        Task<byte[]> GeneratePngAsync(string cacheKey, int cacheSeconds, IMemoryCache cache, IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, int hours, string? pinnedContainerSelector, CancellationToken ct);
        Task<StatusImageModel> GenerateStatusAsync(string cacheKey, int cacheSeconds, IMemoryCache cache, IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, int hours, string? pinnedContainerSelector, CancellationToken ct);
    }
}