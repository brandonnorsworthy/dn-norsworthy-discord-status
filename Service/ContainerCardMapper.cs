using System.Globalization;
using StatusImageCard.Helper;
using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    /// <summary>Maps monitor API models to display-ready cards.</summary>
    public static class ContainerCardMapper
    {
        public const string Placeholder = "--";

        /// <param name="current">The container's current snapshot.</param>
        /// <param name="history">History samples, or null when there are none (offline, or the call failed).</param>
        /// <param name="hours">The history window, for the subtitle.</param>
        public static ContainerCard Map(ZcogContainerModel current, IReadOnlyList<ZcogContainerModel>? history, int hours)
        {
            bool online = ContainerSelection.IsOnline(current);
            string idShort = current.Id.Length > 12 ? current.Id[..12] + "..." : current.Id;
            string subtitle = $"Usage History (Last {hours} hour{(hours == 1 ? "" : "s")})";

            if (!online)
            {
                return new ContainerCard
                {
                    Name = current.Name,
                    IdShort = idShort,
                    StatusState = StatusState.Offline,
                    StatusText = "Offline",
                    DetailText = string.IsNullOrWhiteSpace(current.Status) ? "Not running" : current.Status.Trim(),
                    CpuText = Placeholder,
                    MemoryText = Placeholder,
                    Subtitle = subtitle,
                    UsageHistory = null
                };
            }

            // The status string carries the "Up ..." text, e.g. "Up 26 hours (healthy)".
            string uptime = ContainerTextHelper.FormatUptime(current.Status);
            bool healthy = (ContainerTextHelper.ExtractHealthSuffix(current.Status) ?? "healthy")
                .Equals("healthy", StringComparison.OrdinalIgnoreCase);

            // Memory values from the API are MB; show GB.
            double usedGb = Math.Round(current.MemoryCurrent / 1024.0, 2);
            double maxGb = Math.Round(current.MemoryMax / 1024.0, 2);

            List<UsageSample>? usage = history?
                .Select(h => new UsageSample
                {
                    TsUtc = ContainerTextHelper.ParseZcogTimestamp(h.Timestamp),
                    CpuPercent = h.CpuPercent,
                    MemoryPercent = h.MemoryPercent
                })
                .ToList();

            return new ContainerCard
            {
                Name = current.Name,
                IdShort = idShort,
                StatusState = StatusState.Online,
                StatusText = "Online",
                DetailText = uptime,
                Healthy = healthy,
                CpuText = string.Format(CultureInfo.InvariantCulture, "{0:0.##}%", current.CpuPercent),
                MemoryText = string.Format(CultureInfo.InvariantCulture, "{0:0.##}GB / {1:0.##}GB", usedGb, maxGb),
                Subtitle = subtitle,
                UsageHistory = usage is { Count: > 0 } ? usage : null
            };
        }
    }
}
