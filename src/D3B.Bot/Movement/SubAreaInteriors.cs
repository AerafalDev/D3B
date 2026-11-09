using System.Collections.Frozen;
using D3B.Data.World;

namespace D3B.Bot.Movement;

/// <summary>The interiors of the sub-areas that also hold maps on a world map: the houses, the mills and the mines of a town or a field, as opposed to a dungeon (D388, D416).</summary>
/// <remarks>
/// A map on no world map is an interior. Measured on the pack of 3.7.13.17: of the 444 sub-areas holding monsters, 214 hold maps on a
/// world map and interiors, 68 only interiors, the dungeons, which are gone through whole. Astrub's city, sub-area 95, holds 52 maps on
/// a world map and 83 interiors the world graph reaches, mostly houses.
/// </remarks>
public static class SubAreaInteriors
{
    /// <summary>Gets the interiors among the maps of a zone whose sub-area also holds a map on a world map.</summary>
    /// <param name="reached">The positions of the zone's maps a character travels to.</param>
    /// <returns>The maps; none of a sub-area whose maps are all interiors.</returns>
    public static FrozenSet<long> Of(IEnumerable<MapPosition> reached)
    {
        ArgumentNullException.ThrowIfNull(reached);

        var positions = reached.ToArray();
        var outdoors = positions.Where(position => position.WorldMap >= 0).Select(position => position.SubAreaId).ToHashSet();

        return positions.Where(position => position.WorldMap < 0 && outdoors.Contains(position.SubAreaId)).Select(position => position.MapId).ToFrozenSet();
    }
}
