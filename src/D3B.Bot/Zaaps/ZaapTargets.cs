using System.Collections.Frozen;
using D3B.Data.World;

namespace D3B.Bot.Zaaps;

/// <summary>The zaaps a discovery seeks: those the client's table activates that the character does not know (D499).</summary>
/// <remarks>
/// 15 of the client's 62 zaaps are not activated, none of them drawn on its world map and what the flag means not measured
/// (client.md): the owner chose to leave them out rather than walk to zaaps that may be gone (D499).
/// </remarks>
public static class ZaapTargets
{
    /// <summary>Lists the zaaps sought.</summary>
    /// <param name="atlas">The pack's atlas, which holds the client's zaaps.</param>
    /// <param name="known">The maps of the zaaps the character knows, as the server listed them.</param>
    /// <returns>The map of each zaap sought, with its sub-area.</returns>
    public static FrozenDictionary<long, int> Of(WorldAtlas atlas, IReadOnlySet<long> known)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(known);

        var sought = new Dictionary<long, int>();

        foreach (var waypoint in atlas.Waypoints.Span)
        {
            if (waypoint.Activated && !known.Contains(waypoint.MapId))
                sought.TryAdd(waypoint.MapId, waypoint.SubAreaId);
        }

        return sought.ToFrozenDictionary();
    }
}
