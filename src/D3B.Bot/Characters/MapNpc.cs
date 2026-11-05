using System.Collections.Immutable;
using D3B.Data.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Characters;

/// <summary>A non-player character of the character's map, as the server places it.</summary>
/// <param name="ActorId">Its actor id on the map, negative, which the requests acting on it name.</param>
/// <param name="NpcId">Which NPC it is, by id in the client's tables, such as a banker (D29).</param>
/// <param name="Cell">The cell it stands on, when the server gave one.</param>
/// <param name="Direction">The way it faces, 0 to 7, when the server gave one (D383).</param>
public sealed record MapNpc(int ActorId, int NpcId, int? Cell, int? Direction = null)
{
    private const int Directions = 8;

    /// <summary>Reads the NPCs among the actors of a map.</summary>
    /// <param name="actors">The actors a map's information or an actor shown lists.</param>
    /// <returns>Its NPCs, in the order the server listed them.</returns>
    public static ImmutableArray<MapNpc> In(IEnumerable<Actor> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);

        return
        [
            .. actors
                .Where(actor => actor.Id is < 0 and >= int.MinValue && actor.Information?.Roleplay?.Npc is not null)
                .Select(actor => new MapNpc(
                    (int)actor.Id,
                    actor.Information.Roleplay.Npc.NpcId,
                    actor.Disposition is { HasCellId: true } at && at.CellId is >= 0 and < MapData.CellCount ? at.CellId : null,
                    actor.Disposition is { } facing && (int)facing.Direction is >= 0 and < Directions ? (int)facing.Direction : null)),
        ];
    }
}
