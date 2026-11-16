using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using StatusImageCard.Api;
using StatusImageCard.Config;
using StatusImageCard.Models;
using StatusImageCard.Service;

namespace StatusImageCard.Tests;

public class StatusImageServiceTests
{
    private sealed class FakeApi : IInternalApiService
    {
        public List<ZcogContainerModel> Snapshot { get; init; } = [];
        public bool SnapshotFails { get; init; }
        public HashSet<string> HistoryFailsFor { get; init; } = [];

        private int _inFlight;
        public int MaxInFlight;
        public readonly List<string> HistoryCalls = [];

        public Task<List<ZcogContainerModel>> FetchSnapshotAsync(CancellationToken ct) =>
            SnapshotFails ? throw new HttpRequestException("monitor down") : Task.FromResult(Snapshot);

        public async Task<List<ZcogContainerModel>> FetchHistoryAsync(string containerId, int hours, CancellationToken ct)
        {
            var now = Interlocked.Increment(ref _inFlight);
            lock (HistoryCalls)
            {
                HistoryCalls.Add(containerId);
                MaxInFlight = Math.Max(MaxInFlight, now);
            }

            await Task.Delay(20, ct);
            Interlocked.Decrement(ref _inFlight);

            var c = Snapshot.Single(x => x.Id == containerId);
            if (HistoryFailsFor.Contains(c.Name))
                throw new HttpRequestException("history failed");
            return TestData.History(c);
        }
    }

    private static StatusImageService Service(FakeApi api, string whitelist)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [EnvKeys.ContainerWhitelist] = whitelist })
            .Build();
        return new StatusImageService(api, cfg, NullLogger<StatusImageService>.Instance);
    }

    private static (int Width, int Height) PngSize(byte[] png)
    {
        using var bmp = SKBitmap.Decode(png);
        return (bmp.Width, bmp.Height);
    }

    private static readonly List<ZcogContainerModel> SixServers =
    [
        TestData.Container("valheim", running: true),
        TestData.Container("rust", running: false),
        TestData.Container("zomboid", running: true),
        TestData.Container("palworld", running: false),
        TestData.Container("atm11", running: true),
        TestData.Container("minecraft", running: true),
        TestData.Container("unlisted", running: true),
    ];

    [Fact]
    public async Task Grid_has_a_tile_per_whitelisted_server_and_only_fetches_history_for_online_ones()
    {
        var api = new FakeApi { Snapshot = SixServers };

        var result = await Service(api, "atm11,minecraft,valheim,zomboid,palworld,rust,missing").RenderGridAsync(default);

        Assert.False(result.IsFallback);
        Assert.Equal(["atm11", "minecraft", "valheim", "zomboid"], result.OnlineServers);
        Assert.Equal(["palworld", "rust"], result.OfflineServers);
        Assert.Equal((1280, 1260), PngSize(result.PngBytes));
        Assert.Equal(4, api.HistoryCalls.Count);
        Assert.DoesNotContain(SixServers.Single(c => c.Name == "rust").Id, api.HistoryCalls);
    }

    [Fact]
    public async Task History_calls_are_bounded()
    {
        var many = Enumerable.Range(0, 12).Select(i => TestData.Container($"s{i:00}", running: true, id: $"id{i:00}")).ToList();
        var api = new FakeApi { Snapshot = many };

        var result = await Service(api, string.Join(",", many.Select(c => c.Name))).RenderGridAsync(default);

        Assert.Equal(12, api.HistoryCalls.Count);
        Assert.InRange(api.MaxInFlight, 1, StatusImageService.MaxConcurrentHistoryCalls);
        Assert.Equal((3 * 640, 4 * 420), PngSize(result.PngBytes));
    }

    [Fact]
    public async Task One_failed_history_does_not_break_the_grid()
    {
        var api = new FakeApi { Snapshot = SixServers, HistoryFailsFor = ["valheim"] };

        var result = await Service(api, "atm11,valheim").RenderGridAsync(default);

        Assert.False(result.IsFallback);
        Assert.Equal(["atm11", "valheim"], result.OnlineServers);
        Assert.Equal((1280, 420), PngSize(result.PngBytes));
    }

    [Fact]
    public async Task Monitor_failure_returns_the_fallback_card()
    {
        var result = await Service(new FakeApi { SnapshotFails = true }, "atm11").RenderGridAsync(default);

        Assert.True(result.IsFallback);
        Assert.Empty(result.OnlineServers);
        Assert.Empty(result.OfflineServers);
        Assert.Equal((640, 420), PngSize(result.PngBytes));
    }

    [Fact]
    public async Task No_whitelisted_container_returns_the_fallback_card()
    {
        var result = await Service(new FakeApi { Snapshot = SixServers }, "nothing,here").RenderGridAsync(default);

        Assert.True(result.IsFallback);
        Assert.Equal((640, 420), PngSize(result.PngBytes));
    }

    [Theory]
    [InlineData("valheim")]
    [InlineData("RUST")]
    public async Task Single_card_for_whitelisted_online_or_offline_container(string selector)
    {
        var png = await Service(new FakeApi { Snapshot = SixServers }, "atm11,valheim,rust").RenderSingleAsync(selector, default);
        Assert.Equal((640, 420), PngSize(png));
    }

    [Fact]
    public async Task Single_card_by_id_prefix_of_a_whitelisted_container()
    {
        var valheim = SixServers.Single(c => c.Name == "valheim");
        var png = await Service(new FakeApi { Snapshot = SixServers }, "valheim").RenderSingleAsync(valheim.Id[..8], default);
        Assert.Equal((640, 420), PngSize(png));
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("unlisted")]
    [InlineData("valheim")]
    public async Task Single_card_for_unknown_or_non_whitelisted_container_throws(string selector)
    {
        var api = new FakeApi { Snapshot = SixServers };

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Service(api, "atm11").RenderSingleAsync(selector, default));
        Assert.Empty(api.HistoryCalls);
    }
}
