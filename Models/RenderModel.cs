namespace StatusImageCard.Models
{
    public enum StatusState { Online, Offline }

    public sealed class UsageSample
    {
        public DateTimeOffset TsUtc { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryPercent { get; set; }
    }

    /// <summary>Everything one tile needs, already formatted for display.</summary>
    public sealed class ContainerCard
    {
        public required string Name { get; init; }
        public required string IdShort { get; init; }
        public required StatusState StatusState { get; init; }

        /// <summary>Pill text: "Online" or "Offline".</summary>
        public required string StatusText { get; init; }

        /// <summary>Line under the name: "up for 2 weeks" or the monitor's status, e.g. "Exited (0) 2 months ago".</summary>
        public required string DetailText { get; init; }

        /// <summary>Health of an online container (drives the heart icon); null when offline.</summary>
        public bool? Healthy { get; init; }

        public required string CpuText { get; init; }
        public required string MemoryText { get; init; }
        public required string Subtitle { get; init; }

        /// <summary>History samples for the graph; null when there is none (offline, or the history call failed).</summary>
        public IReadOnlyList<UsageSample>? UsageHistory { get; init; }
    }
}
