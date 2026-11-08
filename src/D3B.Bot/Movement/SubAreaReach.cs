using D3B.Bot.Runtime;
using D3B.Data.World;

namespace D3B.Bot.Movement;

/// <summary>Whether a character can travel from where it stands to a map of a sub-area, by the path the world graph finds with its rules now (D391).</summary>
public static class SubAreaReach
{
    /// <summary>Makes the question for a character, each sub-area's answer kept.</summary>
    /// <param name="context">The behavior's context, which says where the character stands and what it may follow.</param>
    /// <param name="atlas">The atlas, placing the maps in their sub-areas.</param>
    /// <returns>A function that tells whether a sub-area is reached; every one is when the character's position is not known.</returns>
    public static Func<int, bool> Of(BotContext context, WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(atlas);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || !context.Maps.TryGet(mapId, out var map)
            || !context.World.TryGetVertex(map, cell, out var vertex))
        {
            return _ => true;
        }

        var rules = WorldTravel.RulesOf(context);
        var inside = new Dictionary<int, HashSet<long>>();

        foreach (var position in atlas.Positions.Span)
        {
            if (!inside.TryGetValue(position.SubAreaId, out var maps))
                inside[position.SubAreaId] = maps = [];

            maps.Add(position.MapId);
        }

        var answers = new Dictionary<int, bool>();

        return subArea =>
        {
            if (!answers.TryGetValue(subArea, out var reached))
                answers[subArea] = reached = inside.TryGetValue(subArea, out var maps) && context.World.TryFind(vertex, maps, rules, out _);

            return reached;
        };
    }
}
