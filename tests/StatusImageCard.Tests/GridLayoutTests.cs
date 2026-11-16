using StatusImageCard.Service;

namespace StatusImageCard.Tests;

public class GridLayoutTests
{
    private const int W = 640;
    private const int H = 420;

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 2)]
    [InlineData(5, 2, 3)]
    [InlineData(6, 2, 3)]
    [InlineData(7, 3, 3)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 3, 4)]
    public void Columns_rows_and_size(int tiles, int columns, int rows)
    {
        var layout = GridLayout.Compute(tiles, W, H);

        Assert.Equal(columns, layout.Columns);
        Assert.Equal(rows, layout.Rows);
        Assert.Equal(columns * W, layout.Width);
        Assert.Equal(rows * H, layout.Height);
        Assert.Equal(tiles, layout.Tiles.Count);
    }

    [Fact]
    public void One_tile_is_a_single_card()
    {
        var layout = GridLayout.Compute(1, W, H);
        Assert.Equal((W, H), (layout.Width, layout.Height));
        Assert.Equal(new TilePlacement(0, 0), layout.Tiles[0]);
    }

    [Fact]
    public void Two_tiles_side_by_side()
    {
        var layout = GridLayout.Compute(2, W, H);
        Assert.Equal([new TilePlacement(0, 0), new TilePlacement(W, 0)], layout.Tiles);
    }

    [Fact]
    public void Five_tiles_centre_the_last_one()
    {
        var layout = GridLayout.Compute(5, W, H);
        Assert.Equal(
            [
                new TilePlacement(0, 0), new TilePlacement(W, 0),
                new TilePlacement(0, H), new TilePlacement(W, H),
                new TilePlacement(W / 2, 2 * H)
            ],
            layout.Tiles);
    }

    [Fact]
    public void Six_tiles_fill_two_by_three()
    {
        var layout = GridLayout.Compute(6, W, H);
        Assert.Equal(new TilePlacement(W, 2 * H), layout.Tiles[5]);
        Assert.All(layout.Tiles, t => Assert.True(t.X is 0 or W));
    }

    [Fact]
    public void Seven_tiles_centre_the_last_one_in_three_columns()
    {
        var layout = GridLayout.Compute(7, W, H);
        Assert.Equal(new TilePlacement(2 * W, H), layout.Tiles[5]);
        Assert.Equal(new TilePlacement(W, 2 * H), layout.Tiles[6]);
    }

    [Fact]
    public void Eight_tiles_centre_the_last_two()
    {
        var layout = GridLayout.Compute(8, W, H);
        Assert.Equal(new TilePlacement(W / 2, 2 * H), layout.Tiles[6]);
        Assert.Equal(new TilePlacement(W / 2 + W, 2 * H), layout.Tiles[7]);
    }

    [Fact]
    public void Zero_tiles_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayout.Compute(0, W, H));
    }
}
