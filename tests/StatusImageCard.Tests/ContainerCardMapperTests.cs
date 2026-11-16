using StatusImageCard.Models;
using StatusImageCard.Service;

namespace StatusImageCard.Tests;

public class ContainerCardMapperTests
{
    [Fact]
    public void Online_container_maps_uptime_health_cpu_memory_and_history()
    {
        var c = TestData.Container("valheim", running: true,
            id: "00ab5b82043bc428942c22a1c589169732206f721750626cbecb25105a7ca015",
            status: "Up 3 weeks (unhealthy)", cpuPercent: 3.774, memoryCurrentMb: 5503, memoryMaxMb: 16384);

        var card = ContainerCardMapper.Map(c, TestData.History(c, 5), hours: 6);

        Assert.Equal("valheim", card.Name);
        Assert.Equal("00ab5b82043b...", card.IdShort);
        Assert.Equal(StatusState.Online, card.StatusState);
        Assert.Equal("Online", card.StatusText);
        Assert.Equal("Up 3 weeks (unhealthy)", card.DetailText);
        Assert.Equal("3.77%", card.CpuText);
        Assert.Equal("5.37GB / 16GB", card.MemoryText);
        Assert.Equal("Usage History (Last 6 hours)", card.Subtitle);
        Assert.NotNull(card.UsageHistory);
        Assert.Equal(5, card.UsageHistory!.Count);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 13, 42, 0, TimeSpan.Zero), card.UsageHistory[0].TsUtc);
    }

    [Fact]
    public void Online_status_without_health_defaults_to_healthy()
    {
        var c = TestData.Container("valheim", running: true, status: "Up 3 weeks");
        Assert.Equal("Up 3 weeks (healthy)", ContainerCardMapper.Map(c, null, 6).DetailText);
    }

    [Fact]
    public void Online_without_history_has_no_graph_data()
    {
        var c = TestData.Container("valheim", running: true);
        Assert.Null(ContainerCardMapper.Map(c, null, 6).UsageHistory);
        Assert.Null(ContainerCardMapper.Map(c, [], 6).UsageHistory);
    }

    [Fact]
    public void Offline_container_shows_status_text_and_placeholders()
    {
        var c = TestData.Container("rust", running: false, status: "Exited (128) 17 months ago");

        var card = ContainerCardMapper.Map(c, TestData.History(c), hours: 6);

        Assert.Equal(StatusState.Offline, card.StatusState);
        Assert.Equal("Offline", card.StatusText);
        Assert.Equal("Exited (128) 17 months ago", card.DetailText);
        Assert.Equal("--", card.CpuText);
        Assert.Equal("--", card.MemoryText);
        Assert.Null(card.UsageHistory);
    }

    [Fact]
    public void Offline_container_with_empty_status_says_not_running()
    {
        var c = TestData.Container("rust", running: false, status: "");
        Assert.Equal("Not running", ContainerCardMapper.Map(c, null, 6).DetailText);
    }

    [Fact]
    public void Short_id_is_not_ellipsized()
    {
        var c = TestData.Container("x", running: true, id: "abc123");
        Assert.Equal("abc123", ContainerCardMapper.Map(c, null, 1).IdShort);
        Assert.Equal("Usage History (Last 1 hour)", ContainerCardMapper.Map(c, null, 1).Subtitle);
    }
}
