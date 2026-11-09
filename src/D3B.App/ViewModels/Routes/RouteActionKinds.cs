using System.Collections.Immutable;
using D3B.App.Controls.WorldMaps;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>Tells what a step does in the few kinds the map's dots tell apart (D397).</summary>
internal static class RouteActionKinds
{
    /// <summary>Gives the kinds of a step's actions, each once, in the order the actions come.</summary>
    /// <param name="actions">The step's actions.</param>
    /// <returns>The kinds.</returns>
    public static IReadOnlyList<WorldMapActionKind> Of(ImmutableArray<NodeCall> actions)
    {
        if (actions.IsDefaultOrEmpty)
            return [];

        var kinds = new List<WorldMapActionKind>();

        foreach (var call in actions)
        {
            var kind = OfNode(call.Node);

            if (!kinds.Contains(kind))
                kinds.Add(kind);
        }

        return kinds;
    }

    /// <summary>Tells what an action is in the few kinds the map's dots tell apart.</summary>
    /// <param name="node">The action's node id.</param>
    /// <returns>Its kind.</returns>
    public static WorldMapActionKind OfNode(string node)
    {
        return node switch
        {
            RouteActions.Harvest => WorldMapActionKind.Harvest,
            RouteActions.Fight => WorldMapActionKind.Fight,
            RouteActions.Door or RouteActions.Exit => WorldMapActionKind.Door,
            RouteActions.Npc => WorldMapActionKind.Npc,
            _ => WorldMapActionKind.Other,
        };
    }
}
