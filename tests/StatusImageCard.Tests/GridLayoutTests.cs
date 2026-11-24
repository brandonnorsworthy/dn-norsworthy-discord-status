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

    private const int Half = H / 2;

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(7, 3, 3)]
    public void All_online_matches_the_full_tile_layout(int online, int columns, int rows)
    {
        var layout = GridLayout.Compute(online, 0, W, H);

        Assert.Equal(GridLayout.Compute(online, W, H).Tiles, layout.Tiles);
        Assert.Equal((columns, rows, columns * W, rows * H), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.All(layout.Tiles, t => Assert.False(t.Compact));
    }

    [Fact]
    public void Eight_offline_share_four_cells()
    {
        var layout = GridLayout.Compute(0, 8, W, H);

        Assert.Equal((2, 2, 2 * W, 2 * H), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.Equal(8, layout.Tiles.Count);
        Assert.All(layout.Tiles, t => Assert.True(t.Compact));
        Assert.Equal(new TilePlacement(0, 0, true), layout.Tiles[0]);
        Assert.Equal(new TilePlacement(0, Half, true), layout.Tiles[1]);
        Assert.Equal(new TilePlacement(W, 0, true), layout.Tiles[2]);
        Assert.Equal(new TilePlacement(W, Half, true), layout.Tiles[3]);
        Assert.Equal(new TilePlacement(0, H, true), layout.Tiles[4]);
        Assert.Equal(new TilePlacement(W, H + Half, true), layout.Tiles[7]);
    }

    [Fact]
    public void Odd_offline_tile_takes_the_top_of_its_own_cell()
    {
        var layout = GridLayout.Compute(0, 3, W, H);

        Assert.Equal((2, 1, 2 * W, H), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.Equal(
            [new TilePlacement(0, 0, true), new TilePlacement(0, Half, true), new TilePlacement(W, 0, true)],
            layout.Tiles);
    }

    [Fact]
    public void One_offline_tile_is_one_cell()
    {
        var layout = GridLayout.Compute(0, 1, W, H);

        Assert.Equal((1, 1, W, H), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.Equal([new TilePlacement(0, 0, true)], layout.Tiles);
    }

    [Fact]
    public void One_online_and_one_offline()
    {
        var layout = GridLayout.Compute(1, 1, W, H);

        Assert.Equal((2, 1, 2 * W, H), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.Equal([new TilePlacement(0, 0), new TilePlacement(W, 0, true)], layout.Tiles);
    }

    [Fact]
    public void Four_online_and_eight_offline_make_eight_cells()
    {
        var layout = GridLayout.Compute(4, 8, W, H);

        Assert.Equal((3, 3, 1920, 1260), (layout.Columns, layout.Rows, layout.Width, layout.Height));
        Assert.Equal(12, layout.Tiles.Count);
        // Cells 0-3 are online (cell 3 starts row 1); offline cells are 4-7, stacked in pairs.
        Assert.Equal(new TilePlacement(0, H), layout.Tiles[3]);
        Assert.Equal(new TilePlacement(W, H, true), layout.Tiles[4]);
        Assert.Equal(new TilePlacement(W, H + Half, true), layout.Tiles[5]);
        Assert.Equal(new TilePlacement(2 * W, H, true), layout.Tiles[6]);
        // The last row holds two cells, centred.
        Assert.Equal(new TilePlacement(W / 2, 2 * H, true), layout.Tiles[8]);
        Assert.Equal(new TilePlacement(W / 2, 2 * H + Half, true), layout.Tiles[9]);
        Assert.Equal(new TilePlacement(W / 2 + W, 2 * H, true), layout.Tiles[10]);
        Assert.Equal(new TilePlacement(W / 2 + W, 2 * H + Half, true), layout.Tiles[11]);
    }

    [Fact]
    public void Partial_last_row_of_cells_is_centred()
    {
        // 3 online + 4 offline = 5 cells: the last (offline) cell is centred in row 3.
        var layout = GridLayout.Compute(3, 4, W, H);

        Assert.Equal((2, 3), (layout.Columns, layout.Rows));
        Assert.Equal(new TilePlacement(W / 2, 2 * H, true), layout.Tiles[5]);
        Assert.Equal(new TilePlacement(W / 2, 2 * H + Half, true), layout.Tiles[6]);
    }

    [Fact]
    public void Stacked_pair_second_tile_is_half_a_tile_lower()
    {
        var layout = GridLayout.Compute(2, 6, W, H);

        for (int j = 0; j < 6; j += 2)
        {
            var top = layout.Tiles[2 + j];
            var bottom = layout.Tiles[2 + j + 1];
            Assert.Equal(top.X, bottom.X);
            Assert.Equal(top.Y + Half, bottom.Y);
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 8)]
    [InlineData(4, 8)]
    [InlineData(3, 4)]
    [InlineData(7, 5)]
    [InlineData(2, 3)]
    public void Every_tile_is_inside_the_image_and_tiles_do_not_overlap(int online, int offline)
    {
        var layout = GridLayout.Compute(online, offline, W, H);

        var rects = layout.Tiles.Select(t => (t.X, t.Y, Right: t.X + W, Bottom: t.Y + (t.Compact ? Half : H))).ToList();
        Assert.All(rects, r =>
        {
            Assert.InRange(r.X, 0, layout.Width - W);
            Assert.InRange(r.Y, 0, layout.Height);
            Assert.True(r.Right <= layout.Width);
            Assert.True(r.Bottom <= layout.Height);
        });

        for (int a = 0; a < rects.Count; a++)
            for (int b = a + 1; b < rects.Count; b++)
                Assert.False(
                    rects[a].X < rects[b].Right && rects[b].X < rects[a].Right &&
                    rects[a].Y < rects[b].Bottom && rects[b].Y < rects[a].Bottom,
                    $"tiles {a} and {b} overlap");
    }

    [Fact]
    public void Zero_tiles_of_either_kind_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayout.Compute(0, 0, W, H));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayout.Compute(-1, 2, W, H));
    }
}
