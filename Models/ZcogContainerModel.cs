using System.Text.Json.Serialization;

namespace StatusImageCard.Models
{
    public sealed class ZcogContainerModel
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("cpu_current")] public double CpuCurrent { get; set; }
        [JsonPropertyName("cpu_max")] public double CpuMax { get; set; }
        [JsonPropertyName("cpu_percent")] public double CpuPercent { get; set; }

        [JsonPropertyName("memory_current")] public double MemoryCurrent { get; set; } // MB
        [JsonPropertyName("memory_max")] public double MemoryMax { get; set; }         // MB
        [JsonPropertyName("memory_percent")] public double MemoryPercent { get; set; }

        [JsonPropertyName("state")] public string State { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";

        [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    }
}
