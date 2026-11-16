using StatusImageCard.Models;

namespace StatusImageCard.Api
{
    public interface IInternalApiService
    {
        /// <summary>Current stats for every container the monitor knows (GET /api/containers/stats/).</summary>
        Task<List<ZcogContainerModel>> FetchSnapshotAsync(CancellationToken ct);

        /// <summary>History samples for one container (GET /api/containers/stats/{id}?h=hours).</summary>
        Task<List<ZcogContainerModel>> FetchHistoryAsync(string containerId, int hours, CancellationToken ct);
    }
}
