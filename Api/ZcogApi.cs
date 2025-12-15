using OxyPlot.Utilities;
using StatusImageCard.Helper;
using StatusImageCard.Models;
using System.Text.Json;

namespace StatusImageCard.Api
{
    public class ZcogApi
    {
        public static async Task<ContainerCard> FetchFromApi(IHttpClientFactory factory, string containerSelector, int hours, JsonSerializerOptions jsonOpts, CancellationToken ct)
        {
            try
            {
                var client = factory.CreateClient("zcog");

                // 1) current snapshot list
                var listResp = await client.GetAsync("/api/containers/stats/", ct);
                listResp.EnsureSuccessStatusCode();

                var list = await listResp.Content.ReadFromJsonAsync<List<ZcogContainerModel>>(jsonOpts, ct) ?? new List<ZcogContainerModel>();

                // Find by exact name, or by id prefix, or full id
                var current = list.FirstOrDefault(x =>
                    x.Name.Equals(containerSelector, StringComparison.OrdinalIgnoreCase) ||
                    x.Id.StartsWith(containerSelector, StringComparison.OrdinalIgnoreCase));

                if (current == null)
                {
                    throw new Exception("Current container not found");
                }

                // 2) history for that container
                var histUrl = $"/api/containers/stats/{current.Id}?h={hours}";
                var histResp = await client.GetAsync(histUrl, ct);
                histResp.EnsureSuccessStatusCode();

                var hist = await histResp.Content.ReadFromJsonAsync<List<ZcogContainerModel>>(jsonOpts, ct) ?? new List<ZcogContainerModel>();

                return MapToCard(current, hist, hours);
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to fetch");
                throw;
            }
        }

        private static ContainerCard MapToCard(ZcogContainerModel current, List<ZcogContainerModel> history, int hours)
        {
            // memory values from your API appear to be MB; convert to GB for display
            double usedGb = current.MemoryCurrent / 1024.0;
            double maxGb = current.MemoryMax / 1024.0;

            var state = current.State?.Equals("running", StringComparison.OrdinalIgnoreCase) == true
                ? StatusState.Online
                : StatusState.Offline;
            Console.WriteLine(current);

            string statusText = state == StatusState.Online ? "Online" : "Offline";

            // status string already contains the "Up ..." text in your payload
            string uptime = ContainerTextHelper.ExtractUpPrefix(current.Status) ?? (state == StatusState.Online ? "Up" : "Down");
            string health = ContainerTextHelper.ExtractHealthSuffix(current.Status) ?? (state == StatusState.Online ? "healthy" : "unhealthy");

            // Build chart samples from history
            var usage = new List<UsageSample>(history.Count);
            foreach (var h in history)
            {
                var ts = ContainerTextHelper.ParseZcogTimestamp(h.Timestamp);
                usage.Add(new UsageSample
                {
                    TsUtc = ts,
                    CpuPercent = h.CpuPercent,
                    MemoryPercent = h.MemoryPercent
                });
            }

            return new ContainerCard
            {
                Name = current.Name,
                IdShort = current.Id.Length >= 12 ? current.Id[..12] + "..." : current.Id,
                StatusState = state,
                StatusText = statusText,
                UptimeText = uptime,
                HealthText = health,
                CpuNowPercent = current.CpuPercent,
                MemoryUsedGb = Math.Round(usedGb, 2),
                MemoryTotalGb = Math.Round(maxGb, 2),
                Subtitle = $"Usage History (Last {hours} hour{(hours == 1 ? "" : "s")})",
                UsageLastDay = usage
            };
        }

        public static async Task<List<ZcogContainerModel>> FetchSnapshotAsync(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOpts, CancellationToken ct)
        {
            var client = httpClientFactory.CreateClient("zcog");

            var listResp = await client.GetAsync("/api/containers/stats/", ct);
            listResp.EnsureSuccessStatusCode();

            var list = await listResp.Content.ReadFromJsonAsync<List<ZcogContainerModel>>(jsonOpts, ct)
                       ?? new List<ZcogContainerModel>();

            // Keep API order, but drop items missing ID/Name
            return list.Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name)).ToList();
        }
    }
}
