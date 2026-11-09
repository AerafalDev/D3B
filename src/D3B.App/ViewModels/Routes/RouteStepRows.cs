using D3B.App.Localization;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>Makes the rows of the route's list of steps: each step's place, sub-area and actions, and the gap to the next, said, broken or not (D299, D371).</summary>
internal static class RouteStepRows
{
    /// <summary>Makes a row for each anchor.</summary>
    /// <param name="services">What the editors read: the world's index.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="texts">What says a map's place and sub-area.</param>
    /// <param name="anchors">The route's anchors.</param>
    /// <param name="legs">The legs the planner found, as far as it has.</param>
    /// <param name="mode">How the route goes through its anchors.</param>
    /// <param name="shownLayer">The world shown, a step on another being said elsewhere.</param>
    /// <param name="selected">The steps selected.</param>
    /// <param name="reveal">What choosing a row does, given its step's place.</param>
    /// <param name="remove">What removing a row does, given its step's place.</param>
    /// <returns>The rows, in order.</returns>
    public static List<RouteStepViewModel> Build(RouteServices services, Localizer localizer, RouteTexts texts, IReadOnlyList<RouteAnchor> anchors, IReadOnlyList<RouteLeg> legs, RouteMode mode, int shownLayer, IReadOnlyCollection<int> selected, Action<int> reveal, Action<int> remove)
    {
        var steps = new List<RouteStepViewModel>(anchors.Count);

        for (var i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            var elsewhere = services.Index.TryPlace(anchor.MapId, out var layer, out _, out _) && layer != shownLayer;
            var leg = i < legs.Count ? legs[i] : null;
            var closing = i == anchors.Count - 1;
            string gap = "";
            var broken = false;

            if (leg is not null && (!closing || RoutePlanner.ClosesBack(mode, anchors.Count)))
            {
                (gap, broken) = leg.Kind switch
                {
                    RouteLegKind.Unreachable => (localizer["RouteGapNone"], true),
                    RouteLegKind.Same => (localizer["RouteGapSame"], false),
                    _ when leg.UsesZaap => (localizer["RouteGapZaap"], false),
                    _ => (leg.Between.IsEmpty ? localizer["RouteGapNeighbours"] : localizer.Plural("RouteGapMaps", leg.Between.Length), false),
                };

                if (closing && !broken)
                    gap = localizer.Format("RouteGapBack", gap);
                else if (closing)
                    gap = localizer["RouteGapNone"];
            }

            steps.Add(new RouteStepViewModel(i, anchor.MapId, texts.Place(anchor.MapId), texts.Zone(anchor.MapId), anchor.IsPinned, elsewhere, gap, broken, closing && gap.Length > 0, anchor.Actions.Length, anchor.Actions.Length == 0 ? "" : localizer.Plural("RouteActionsCount", anchor.Actions.Length), reveal, remove) { IsSelected = selected.Contains(i) });
        }

        return steps;
    }
}
