using StatusImageCard.Models;

namespace StatusImageCard.Tests;

internal static class TestData
{
    public static ZcogContainerModel Container(
        string name,
        bool running,
        string? id = null,
        string? status = null,
        double cpuPercent = 1.5,
        double memoryCurrentMb = 2048,
        double memoryMaxMb = 8192) => new()
        {
            Id = id ?? (name.GetHashCode().ToString("x8") + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
            Name = name,
            State = running ? "running" : "exited",
            Status = status ?? (running ? "Up 2 weeks (healthy)" : "Exited (0) 2 months ago"),
            CpuPercent = running ? cpuPercent : 0,
            MemoryCurrent = running ? memoryCurrentMb : 0,
            MemoryMax = memoryMaxMb,
            MemoryPercent = running ? memoryCurrentMb / memoryMaxMb * 100 : 0,
            Timestamp = "2026-11-15T19:42:17"
        };

    public static List<ZcogContainerModel> History(ZcogContainerModel c, int samples = 10) =>
        Enumerable.Range(0, samples).Select(i => new ZcogContainerModel
        {
            Id = c.Id,
            Name = c.Name,
            State = c.State,
            Status = c.Status,
            CpuPercent = i % 3,
            MemoryPercent = 30 + i,
            Timestamp = new DateTime(2026, 11, 15, 13, 42, 0).AddMinutes(i * 36).ToString("yyyy-MM-ddTHH:mm:ss")
        }).ToList();
}
