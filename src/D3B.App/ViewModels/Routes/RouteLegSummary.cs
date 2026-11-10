using D3B.App.Localization;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>What the world's path between a route's anchors comes to: the maps it goes through, the gaps no path crosses and the first of them, said (D299).</summary>
/// <param name="MapCount">How many maps the route goes through, the path's included.</param>
/// <param name="GapCount">How many gaps no path crosses.</param>
/// <param name="Problem">The first gap, said with how to read it, or <see langword="null"/> when there is none.</param>
/// <param name="ProblemStep">The step the first gap starts from, or -1.</param>
internal readonly record struct RouteLegSummary(int MapCount, int GapCount, string? Problem, int ProblemStep)
{
    /// <summary>Sums up the legs between the anchors.</summary>
    /// <param name="anchors">The route's anchors.</param>
    /// <param name="legs">The legs the world's path found between them.</param>
    /// <param name="localizer">Says the problem in the language chosen.</param>
    /// <returns>The summary.</returns>
    public static RouteLegSummary Of(IReadOnlyList<RouteAnchor> anchors, IReadOnlyList<RouteLeg> legs, Localizer localizer)
    {
        var broken = legs.Select((leg, index) => (leg, index)).Where(pair => pair.leg.Kind == RouteLegKind.Unreachable).ToList();
        var maps = new HashSet<long>(anchors.Select(anchor => anchor.MapId));

        foreach (var leg in legs)
            maps.UnionWith(leg.Between);

        if (broken.Count == 0)
            return new RouteLegSummary(maps.Count, 0, null, -1);

        var from = broken[0].index;
        var to = from + 1 >= anchors.Count ? 0 : from + 1;

        return new RouteLegSummary(maps.Count, broken.Count, localizer.Format("RouteProblemNone", from + 1, to + 1), from);
    }
}
