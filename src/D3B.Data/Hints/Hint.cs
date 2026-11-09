using System.Runtime.InteropServices;

namespace D3B.Data.Hints;

/// <summary>A point of interest the client's world map draws: a zaap, a dungeon, a workshop, a bank, a boat (D414).</summary>
/// <remarks>
/// <para>
/// The client's <c>HintsDataRoot</c> record, less what the studio does not draw: where it stands is its map's own
/// coordinates on the world it is drawn on, which is not always the world its map belongs to (client.md). Its icon is
/// the client's picture <see cref="Gfx"/>, in <see cref="HintCatalog.Icons"/>.
/// </para>
/// <para>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Hint
{
    /// <summary>Gets the identifier, as the client numbers its hints.</summary>
    public int Id { get; }

    /// <summary>Gets the client's category of the hint, which names it in <see cref="HintCatalog.Categories"/>.</summary>
    public int CategoryId { get; }

    /// <summary>Gets the client's picture of the hint, which <see cref="HintCatalog.Icons"/> holds, and which tells a zaap from a boat in the same category.</summary>
    public int Gfx { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the map it stands on.</summary>
    public long MapId { get; }

    /// <summary>Gets the world's map the client draws it on.</summary>
    public int WorldMapId { get; }

    /// <summary>Gets the horizontal coordinate of the map it stands on.</summary>
    public int X { get; }

    /// <summary>Gets the vertical coordinate of the map it stands on.</summary>
    public int Y { get; }

    /// <summary>Gets the level of a dungeon, and 0 for what has none.</summary>
    public int Level { get; }

    /// <summary>Initializes a new instance of the <see cref="Hint"/> struct.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="categoryId">The category.</param>
    /// <param name="gfx">The picture.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="mapId">The map it stands on.</param>
    /// <param name="worldMapId">The world's map it is drawn on.</param>
    /// <param name="x">The map's horizontal coordinate.</param>
    /// <param name="y">The map's vertical coordinate.</param>
    /// <param name="level">The level, or 0.</param>
    public Hint(int id, int categoryId, int gfx, int nameId, long mapId, int worldMapId, int x, int y, int level)
    {
        Id = id;
        CategoryId = categoryId;
        Gfx = gfx;
        NameId = nameId;
        MapId = mapId;
        WorldMapId = worldMapId;
        X = x;
        Y = y;
        Level = level;
    }
}
