namespace StatusImageCard.Service
{
    /// <summary>Where one tile goes. A compact tile is half a cell tall and shares its cell with another compact tile.</summary>
    public readonly record struct TilePlacement(int X, int Y, bool Compact = false);

    /// <summary>
    /// Where each tile goes in the grid image. A cell is one full tile slot: an online tile fills a cell, and
    /// two offline (compact) tiles share one, stacked. Cells are placed edge to edge, online cells first;
    /// a partial last row of cells is centred.
    /// </summary>
    public sealed record GridLayout(int Columns, int Rows, int Width, int Height, IReadOnlyList<TilePlacement> Tiles)
    {
        /// <summary>1 column for one cell, 2 for up to 6, 3 for 7 or more.</summary>
        public static int ColumnsFor(int cellCount) => cellCount switch
        {
            <= 1 => 1,
            <= 6 => 2,
            _ => 3
        };

        /// <summary>A layout where every tile is a full tile.</summary>
        public static GridLayout Compute(int tileCount, int tileWidth, int tileHeight) =>
            Compute(tileCount, 0, tileWidth, tileHeight);

        /// <summary>
        /// Lays out online tiles (one cell each) then offline tiles (two per cell, compact). Tiles in the result are in
        /// the same order: online first, then offline. An odd last offline tile takes the top half of its own cell.
        /// </summary>
        public static GridLayout Compute(int onlineCount, int offlineCount, int tileWidth, int tileHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(onlineCount);
            ArgumentOutOfRangeException.ThrowIfNegative(offlineCount);
            ArgumentOutOfRangeException.ThrowIfLessThan(onlineCount + offlineCount, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(tileWidth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(tileHeight, 2);

            int cells = onlineCount + (offlineCount + 1) / 2;
            int columns = ColumnsFor(cells);
            int rows = (cells + columns - 1) / columns;

            (int X, int Y) CellOrigin(int cell)
            {
                int row = cell / columns;
                int col = cell % columns;
                int inThisRow = Math.Min(columns, cells - row * columns);
                int offset = (columns - inThisRow) * tileWidth / 2;
                return (offset + col * tileWidth, row * tileHeight);
            }

            var tiles = new List<TilePlacement>(onlineCount + offlineCount);
            for (int i = 0; i < onlineCount; i++)
            {
                var (x, y) = CellOrigin(i);
                tiles.Add(new TilePlacement(x, y));
            }

            for (int j = 0; j < offlineCount; j++)
            {
                var (x, y) = CellOrigin(onlineCount + j / 2);
                tiles.Add(new TilePlacement(x, y + j % 2 * (tileHeight / 2), Compact: true));
            }

            return new GridLayout(columns, rows, columns * tileWidth, rows * tileHeight, tiles);
        }
    }
}
