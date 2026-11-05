using D3B.Data.Packs;

namespace D3B.Data.WorldMaps;

/// <summary>Reads the tiles of the world maps from a pack, one at a time, as the JPEG files the extractor encoded (D369).</summary>
/// <remarks>
/// A tile is read when asked for and kept by nobody: a view keeps the pictures it decodes. Reads are positional and safe
/// from any number of threads, as <see cref="GamePack.Read{T}"/> is.
/// </remarks>
public sealed class WorldMapTiles
{
    private readonly GamePack _pack;

    /// <summary>Initializes a new instance of the <see cref="WorldMapTiles"/> class.</summary>
    /// <param name="pack">The pack holding the tiles.</param>
    public WorldMapTiles(GamePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        _pack = pack;
    }

    /// <summary>Reads a tile.</summary>
    /// <param name="worldId">The world.</param>
    /// <param name="zoomPercent">The zoom, such as 75.</param>
    /// <param name="index">The tile's number in the zoom, from 1.</param>
    /// <returns>The tile's JPEG file.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no such tile.</exception>
    /// <exception cref="PackFormatException">The tile's entry is damaged.</exception>
    public byte[] Read(int worldId, int zoomPercent, int index)
    {
        return _pack.Read<byte[]>(WorldMapCatalog.TileEntryName(worldId, zoomPercent, index));
    }
}
