using MemoryPack;

namespace D3B.Data.World;

/// <summary>Where maps lie in the world: their positions, the sub-areas and areas they belong to, and the zaaps.</summary>
/// <remarks>
/// Each table is sorted by identifier and found by binary search. The client's tables do not cover every map: about
/// 2,300 maps of client 3.7.12.12 have no position (client.md). Loaded once and shared: nothing in it changes.
/// </remarks>
[MemoryPackable]
public sealed partial class WorldAtlas
{
    /// <summary>The name of the pack entry holding the atlas.</summary>
    public const string EntryName = "world/atlas";

    /// <summary>Gets the maps' positions, in ascending order of map.</summary>
    public ReadOnlyMemory<MapPosition> Positions { get; }

    /// <summary>Gets the sub-areas, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SubArea> SubAreas { get; }

    /// <summary>Gets the areas, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Area> Areas { get; }

    /// <summary>Gets the zaaps, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Waypoint> Waypoints { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldAtlas"/> class.</summary>
    /// <param name="positions">The maps' positions, in strictly ascending order of map.</param>
    /// <param name="subAreas">The sub-areas, in strictly ascending order of identifier.</param>
    /// <param name="areas">The areas, in strictly ascending order of identifier.</param>
    /// <param name="waypoints">The zaaps, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public WorldAtlas(ReadOnlyMemory<MapPosition> positions, ReadOnlyMemory<SubArea> subAreas, ReadOnlyMemory<Area> areas, ReadOnlyMemory<Waypoint> waypoints)
    {
        CheckAscending(positions.Span, static position => position.MapId, nameof(positions));
        CheckAscending(subAreas.Span, static subArea => subArea.Id, nameof(subAreas));
        CheckAscending(areas.Span, static area => area.Id, nameof(areas));
        CheckAscending(waypoints.Span, static waypoint => waypoint.Id, nameof(waypoints));

        Positions = positions;
        SubAreas = subAreas;
        Areas = areas;
        Waypoints = waypoints;
    }

    private static void CheckAscending<T>(ReadOnlySpan<T> items, Func<T, long> key, string name)
    {
        for (var i = 1; i < items.Length; i++)
        {
            if (key(items[i]) <= key(items[i - 1]))
                throw new ArgumentException($"The {name} are not in strictly ascending order: {key(items[i])} follows {key(items[i - 1])}.", name);
        }
    }

    private static bool TryFind<T>(ReadOnlySpan<T> items, long id, Func<T, long> key, out T found)
    {
        int low = 0, high = items.Length - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var current = key(items[middle]);

            if (current == id)
            {
                found = items[middle];

                return true;
            }

            if (current < id)
                low = middle + 1;
            else
                high = middle - 1;
        }

        found = default!;

        return false;
    }

    /// <summary>Finds where a map lies.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="position">Its position, when found.</param>
    /// <returns>Whether the client gives the map a position.</returns>
    public bool TryGetPosition(long mapId, out MapPosition position)
    {
        return TryFind(Positions.Span, mapId, static position => position.MapId, out position);
    }

    /// <summary>Finds a sub-area.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="subArea">The sub-area, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetSubArea(int id, out SubArea subArea)
    {
        return TryFind(SubAreas.Span, id, static subArea => subArea.Id, out subArea);
    }

    /// <summary>Finds an area.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="area">The area, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetArea(int id, out Area area)
    {
        return TryFind(Areas.Span, id, static area => area.Id, out area);
    }

    /// <summary>Finds a zaap.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="waypoint">The zaap, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetWaypoint(int id, out Waypoint waypoint)
    {
        return TryFind(Waypoints.Span, id, static waypoint => waypoint.Id, out waypoint);
    }
}
