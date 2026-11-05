using System.Runtime.InteropServices;

namespace D3B.Data.WorldMaps;

/// <summary>One zoom of a world's image: its size, cut into square tiles numbered row by row from the top left.</summary>
/// <remarks>Unmanaged and packed, as <see cref="D3B.Data.World.Area"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WorldMapLevel
{
    /// <summary>The side, in pixels, of every tile but those of the last column and the last row, which hold what is left.</summary>
    public const int TileSize = 1024;

    /// <summary>Gets the zoom, as a percentage of the client's full-size image: 100, 80, 75, 60, 50, 40, 25 or 20.</summary>
    public int ZoomPercent { get; }

    /// <summary>Gets the image's width at this zoom, in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the image's height at this zoom, in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets how many columns of tiles cut the image.</summary>
    public int Columns => (Width + TileSize - 1) / TileSize;

    /// <summary>Gets how many rows of tiles cut the image.</summary>
    public int Rows => (Height + TileSize - 1) / TileSize;

    /// <summary>Gets how many tiles cut the image.</summary>
    public int TileCount => Columns * Rows;

    /// <summary>Initializes a new instance of the <see cref="WorldMapLevel"/> struct.</summary>
    /// <param name="zoomPercent">The zoom, from 1 to 100.</param>
    /// <param name="width">The image's width at this zoom, in pixels.</param>
    /// <param name="height">The image's height at this zoom, in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">The zoom is not from 1 to 100, or a size is not positive.</exception>
    public WorldMapLevel(int zoomPercent, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(zoomPercent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(zoomPercent, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        ZoomPercent = zoomPercent;
        Width = width;
        Height = height;
    }

    /// <summary>Gets where a tile lies in the image, and its size.</summary>
    /// <param name="index">The tile's number, from 1: the first row left to right, then the next.</param>
    /// <returns>The tile's left and top in pixels, and its width and height: <see cref="TileSize"/>, but for the last column and row.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level has no such tile.</exception>
    public (int X, int Y, int Width, int Height) TileBounds(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, TileCount);

        var column = (index - 1) % Columns;
        var row = (index - 1) / Columns;
        var x = column * TileSize;
        var y = row * TileSize;

        return (x, y, Math.Min(TileSize, Width - x), Math.Min(TileSize, Height - y));
    }

    /// <summary>Gets the number of the tile that holds a pixel.</summary>
    /// <param name="x">The pixel's column, from 0.</param>
    /// <param name="y">The pixel's row, from 0.</param>
    /// <returns>The tile's number, from 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pixel is outside the image.</exception>
    public int TileAt(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        return ((y / TileSize) * Columns) + (x / TileSize) + 1;
    }
}
