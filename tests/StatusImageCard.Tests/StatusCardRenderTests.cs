using SkiaSharp;
using StatusImageCard.Models;
using StatusImageCard.Service;

namespace StatusImageCard.Tests;

public class StatusCardRenderTests
{
    private static ContainerCard Card(string name, bool running) =>
        ContainerCardMapper.Map(TestData.Container(name, running), history: null, hours: 6);

    private static (int Width, int Height) PngSize(byte[] png)
    {
        using var bmp = SKBitmap.Decode(png);
        return (bmp.Width, bmp.Height);
    }

    [Fact]
    public void Grid_with_online_and_offline_tiles_has_the_packed_size()
    {
        var cards = new List<ContainerCard>();
        cards.AddRange(Enumerable.Range(0, 4).Select(i => Card($"on{i}", running: true)));
        cards.AddRange(Enumerable.Range(0, 8).Select(i => Card($"off{i}", running: false)));

        Assert.Equal((1920, 1260), PngSize(StatusCardRenderService.RenderGrid(cards)));
    }

    [Fact]
    public void Grid_of_only_offline_tiles_is_shorter_than_full_tiles()
    {
        var cards = Enumerable.Range(0, 2).Select(i => Card($"off{i}", running: false)).ToList();

        Assert.Equal((640, 420), PngSize(StatusCardRenderService.RenderGrid(cards)));
    }

    [Fact]
    public void Grid_places_offline_tiles_after_online_ones_even_if_given_first()
    {
        var cards = new List<ContainerCard> { Card("off", running: false), Card("on", running: true) };

        Assert.Equal((1280, 420), PngSize(StatusCardRenderService.RenderGrid(cards)));
    }

    [Fact]
    public void Single_card_of_an_offline_container_is_still_a_full_tile()
    {
        var png = StatusCardRenderService.Render(Card("off", running: false), StatusCardRenderService.TileWidth, StatusCardRenderService.TileHeight);

        Assert.Equal((640, 420), PngSize(png));
    }
}
