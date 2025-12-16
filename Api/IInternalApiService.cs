using StatusImageCard.Models;
using System.Text.Json;

namespace StatusImageCard.Api
{
    public interface IInternalApiService
    {
        Task<List<ZcogContainerModel>> FetchSnapshotAsync(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, CancellationToken ct);
        Task<ContainerCard> FetchFromApi(IHttpClientFactory factory, string containerSelector, int hours, JsonSerializerOptions jsonOpts, CancellationToken ct);
    }
}