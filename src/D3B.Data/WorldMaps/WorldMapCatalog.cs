using System.Globalization;
using MemoryPack;

namespace D3B.Data.WorldMaps;

/// <summary>The worlds the client draws a map of, and the zooms their images come in; the tiles are entries of their own (D308, D369).</summary>
/// <remarks>
/// <para>
/// Sorted by identifier and found by binary search, as the other tables. A world's zooms are a slice of
/// <see cref="Levels"/>, from the largest to the smallest. The images are not here: each tile is a JPEG in an entry of
/// its own, named by <see cref="TileEntryName"/>, which <see cref="WorldMapTiles"/> reads, so that a view loads only the
/// tiles it shows. Loaded once and shared: nothing in it changes.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class WorldMapCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "worldmaps/catalog";

    /// <summary>Gets the worlds, in ascending order of identifier.</summary>
    public ReadOnlyMemory<WorldMap> Worlds { get; }

    /// <summary>Gets the zooms of every world: each world's together, from the largest to the smallest, in the order of the worlds.</summary>
    public ReadOnlyMemory<WorldMapLevel> Levels { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldMapCatalog"/> class.</summary>
    /// <param name="worlds">The worlds, in strictly ascending order of identifier, each naming its own zooms.</param>
    /// <param name="levels">The worlds' zooms, each world's together and in the order of the worlds, with nothing between.</param>
    /// <exception cref="ArgumentException">The worlds are not in strictly ascending order, their zooms do not follow one another, or a world's zooms do not go down.</exception>
    [MemoryPackConstructor]
    public WorldMapCatalog(ReadOnlyMemory<WorldMap> worlds, ReadOnlyMemory<WorldMapLevel> levels)
    {
        Tables.CheckAscending(worlds.Span, static world => world.Id, nameof(worlds));
        Tables.CheckSlices(worlds.Span, static world => (world.FirstLevel, world.LevelCount), levels.Length, nameof(levels));

        foreach (var world in worlds.Span)
        {
            var own = levels.Span.Slice(world.FirstLevel, world.LevelCount);

            for (var i = 1; i < own.Length; i++)
            {
                if (own[i].ZoomPercent >= own[i - 1].ZoomPercent)
                    throw new ArgumentException($"World {world.Id}'s zooms do not go down: {own[i].ZoomPercent} % follows {own[i - 1].ZoomPercent} %.", nameof(levels));
            }
        }

        Worlds = worlds;
        Levels = levels;
    }

    /// <summary>Gets the name of the pack entry holding a tile's JPEG.</summary>
    /// <param name="worldId">The world.</param>
    /// <param name="zoomPercent">The zoom, such as 75.</param>
    /// <param name="index">The tile's number in the zoom, from 1.</param>
    /// <returns>An entry name such as <c>worldmaps/1/80/37</c>.</returns>
    public static string TileEntryName(int worldId, int zoomPercent, int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"worldmaps/{worldId}/{zoomPercent}/{index}");
    }

    /// <summary>Finds a world.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="world">The world, when found.</param>
    /// <returns>Whether the client draws a map of it.</returns>
    public bool TryGet(int id, out WorldMap world)
    {
        return Tables.TryFind(Worlds.Span, id, static world => world.Id, out world);
    }

    /// <summary>Gets a world's zooms.</summary>
    /// <param name="world">A world of this catalog.</param>
    /// <returns>Its zooms, from the largest to the smallest.</returns>
    public ReadOnlySpan<WorldMapLevel> LevelsOf(in WorldMap world)
    {
        return Levels.Span.Slice(world.FirstLevel, world.LevelCount);
    }

    /// <summary>Finds the zoom of a world that suits a scale: the smallest one at least as large, else the largest.</summary>
    /// <param name="world">A world of this catalog.</param>
    /// <param name="scale">The scale the view draws at, 1 for the client's full size.</param>
    /// <returns>The zoom whose tiles to draw, never smaller than the scale asks while a larger one exists.</returns>
    public WorldMapLevel LevelFor(in WorldMap world, double scale)
    {
        var levels = LevelsOf(world);
        var chosen = levels[0];

        foreach (var level in levels)
        {
            if (level.ZoomPercent >= scale * 100 - 1e-9)
                chosen = level;
        }

        return chosen;
    }
}
