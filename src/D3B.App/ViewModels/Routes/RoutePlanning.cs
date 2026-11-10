using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>The search of the world's path between a route's anchors, off the interface's thread, and the legs it found (D299).</summary>
/// <remarks>One search per change: a later change planned again makes the answer of an earlier one old, and it is dropped.</remarks>
internal sealed class RoutePlanning
{
    private int _version;

    /// <summary>Gets the legs the last search found, one between each anchor and the next.</summary>
    public IReadOnlyList<RouteLeg> Legs { get; private set; } = [];

    /// <summary>Gets what the search has not finished.</summary>
    public Task Planned { get; private set; } = Task.CompletedTask;

    /// <summary>Plans the legs between anchors, and tells once they are found unless a later search began.</summary>
    /// <param name="planner">The world's path.</param>
    /// <param name="maps">The anchors' maps, in order.</param>
    /// <param name="mode">How the route goes through them.</param>
    /// <param name="found">Called on the interface's thread once <see cref="Legs"/> holds the legs found.</param>
    public void Start(RoutePlanner planner, List<long> maps, RouteMode mode, Action found)
    {
        Planned = PlanAsync(++_version, planner, maps, mode, found);
    }

    private async Task PlanAsync(int version, RoutePlanner planner, List<long> maps, RouteMode mode, Action found)
    {
        var legs = await Task.Run(() => planner.Plan(maps, mode)).ConfigureAwait(true);

        // A later change planned again: this answer is old.
        if (version != _version)
            return;

        Legs = legs;
        found();
    }
}
