using System.Runtime.InteropServices;

namespace D3B.Data.WorldMaps;

/// <summary>A world the client draws a map of, from its <c>WorldMapsDataRoot</c>: where maps stand on its image, and how far it zooms.</summary>
/// <remarks>
/// Unmanaged and packed, as <see cref="D3B.Data.World.Area"/>. Its zooms are a slice of the catalog's levels, named by
/// their first index and their count (client.md).
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WorldMap
{
    /// <summary>Gets the world's identifier, the <see cref="D3B.Data.World.MapPosition.WorldMap"/> of its maps.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the column of the pixel, at the client's full size, that the map at coordinates 0, 0 stands at.</summary>
    public int OriginX { get; }

    /// <summary>Gets the row of the pixel, at the client's full size, that the map at coordinates 0, 0 stands at.</summary>
    public int OriginY { get; }

    /// <summary>Gets the width of a map on the image, at the client's full size, in pixels.</summary>
    public float MapWidth { get; }

    /// <summary>Gets the height of a map on the image, at the client's full size, in pixels.</summary>
    public float MapHeight { get; }

    /// <summary>Gets the least scale the client lets its view zoom out to.</summary>
    public float MinScale { get; }

    /// <summary>Gets the most scale the client lets its view zoom in to.</summary>
    public float MaxScale { get; }

    /// <summary>Gets the scale the client opens the world at.</summary>
    public float StartScale { get; }

    /// <summary>Gets the image's width at the client's full size, in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the image's height at the client's full size, in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets whether the client lists the world among those its map shows.</summary>
    public bool Visible { get; }

    /// <summary>Gets the index in the catalog's levels of the world's first zoom, the largest.</summary>
    public int FirstLevel { get; }

    /// <summary>Gets how many zooms the world has.</summary>
    public int LevelCount { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldMap"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="originX">The pixel of the map at 0, 0.</param>
    /// <param name="originY">The pixel of the map at 0, 0.</param>
    /// <param name="mapWidth">The width of a map on the image.</param>
    /// <param name="mapHeight">The height of a map on the image.</param>
    /// <param name="minScale">The least scale of the client's view.</param>
    /// <param name="maxScale">The most scale of the client's view.</param>
    /// <param name="startScale">The scale the client opens the world at.</param>
    /// <param name="width">The image's width at full size.</param>
    /// <param name="height">The image's height at full size.</param>
    /// <param name="visible">Whether the client's map lists the world.</param>
    /// <param name="firstLevel">The index of its first zoom among the catalog's.</param>
    /// <param name="levelCount">How many zooms it has.</param>
    /// <exception cref="ArgumentOutOfRangeException">A map or the image is not at least a pixel wide and high, or the world has no zoom.</exception>
    public WorldMap(int id, int nameId, int originX, int originY, float mapWidth, float mapHeight, float minScale, float maxScale, float startScale, int width, int height, bool visible, int firstLevel, int levelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mapWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mapHeight, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(levelCount, 1);

        Id = id;
        NameId = nameId;
        OriginX = originX;
        OriginY = originY;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        MinScale = minScale;
        MaxScale = maxScale;
        StartScale = startScale;
        Width = width;
        Height = height;
        Visible = visible;
        FirstLevel = firstLevel;
        LevelCount = levelCount;
    }

    /// <summary>Gets where the map at coordinates stands on the image at the client's full size.</summary>
    /// <param name="x">The map's x.</param>
    /// <param name="y">The map's y.</param>
    /// <returns>The pixel of the map's top left corner, by the rule measured in client.md; at another zoom, multiply by it.</returns>
    public (double X, double Y) PixelOf(int x, int y)
    {
        return (OriginX + (x * (double)MapWidth), OriginY + (y * (double)MapHeight));
    }
}
