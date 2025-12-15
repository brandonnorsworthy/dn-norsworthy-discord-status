namespace StatusImageCard.Models
{
    public enum StatusState { Online, Offline }

    public sealed class UsageSample
    {
        public DateTimeOffset TsUtc { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryPercent { get; set; }
    }

    public sealed class ContainerCard
    {
        public required string Name { get; set; }
        public required string IdShort { get; set; }
        public required StatusState StatusState { get; set; }
        public required string StatusText { get; set; }
        public required string UptimeText { get; set; }
        public required string HealthText { get; set; }
        public required double CpuNowPercent { get; set; }
        public required double MemoryUsedGb { get; set; }
        public required double MemoryTotalGb { get; set; }
        public required string Subtitle { get; set; }
        public required List<UsageSample> UsageLastDay { get; set; }
    }
}
