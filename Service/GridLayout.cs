namespace StatusImageCard.Service
{
    public readonly record struct TilePlacement(int X, int Y);

    /// <summary>Where each tile goes in the grid image. Tiles are placed edge to edge; a partial last row is centred.</summary>
    public sealed record GridLayout(int Columns, int Rows, int Width, int Height, IReadOnlyList<TilePlacement> Tiles)
    {
        /// <summary>1 column for one tile, 2 for up to 6, 3 for 7 or more.</summary>
        public static int ColumnsFor(int tileCount) => tileCount switch
        {
            <= 1 => 1,
            <= 6 => 2,
            _ => 3
        };

        public static GridLayout Compute(int tileCount, int tileWidth, int tileHeight)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(tileCount, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(tileWidth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(tileHeight, 1);

            int columns = ColumnsFor(tileCount);
            int rows = (tileCount + columns - 1) / columns;

            var tiles = new List<TilePlacement>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int inThisRow = Math.Min(columns, tileCount - row * columns);
                int offset = (columns - inThisRow) * tileWidth / 2;
                tiles.Add(new TilePlacement(offset + col * tileWidth, row * tileHeight));
            }

            return new GridLayout(columns, rows, columns * tileWidth, rows * tileHeight, tiles);
        }
    }
}
