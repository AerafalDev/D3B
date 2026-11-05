using D3B.Data.WorldMaps;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Which tiles of a zoom a part of the image needs, and where each one lies (D369).</summary>
internal static class WorldMapTiling
{
    /// <summary>Lists the tiles of a zoom that a part of the full-size image overlaps.</summary>
    /// <param name="level">The zoom.</param>
    /// <param name="left">The part's left in the full-size image.</param>
    /// <param name="top">Its top.</param>
    /// <param name="right">Its right.</param>
    /// <param name="bottom">Its bottom.</param>
    /// <returns>The tiles' numbers, row by row from the top left; none when the part lies outside the image.</returns>
    public static IReadOnlyList<int> TilesIn(in WorldMapLevel level, double left, double top, double right, double bottom)
    {
        var zoom = level.ZoomPercent / 100.0;

        if (right < 0 || bottom < 0 || left * zoom >= level.Width || top * zoom >= level.Height)
            return [];

        var firstColumn = Math.Max((int)Math.Floor(left * zoom / WorldMapLevel.TileSize), 0);
        var firstRow = Math.Max((int)Math.Floor(top * zoom / WorldMapLevel.TileSize), 0);
        var lastColumn = Math.Min((int)Math.Floor(right * zoom / WorldMapLevel.TileSize), level.Columns - 1);
        var lastRow = Math.Min((int)Math.Floor(bottom * zoom / WorldMapLevel.TileSize), level.Rows - 1);
        List<int> tiles = [];

        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
                tiles.Add((row * level.Columns) + column + 1);
        }

        return tiles;
    }

    /// <summary>Gets where a tile lies in the full-size image, which is the tile's pixels over the zoom.</summary>
    /// <param name="level">The zoom.</param>
    /// <param name="index">The tile's number.</param>
    /// <returns>The tile's left, top, width and height in the full-size image.</returns>
    public static (double X, double Y, double Width, double Height) Place(in WorldMapLevel level, int index)
    {
        var (x, y, width, height) = level.TileBounds(index);
        var zoom = level.ZoomPercent / 100.0;

        return (x / zoom, y / zoom, width / zoom, height / zoom);
    }
}
