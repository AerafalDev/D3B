using D3B.Studio.Assets;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Follows a route of the project: its course, its maps in order and the actions of each, as the character it plays on can (D379).</summary>
/// <remarks>
/// A loop never ends but by an interruption, a state that takes over or the run's stop; the run then goes on from the anchor it
/// was at (<see cref="RouteRuns"/>). A route that fails ends the run, with a note in the journal saying why.
/// </remarks>
internal sealed class FollowRoute : ActionNode<RouteFollowValues>
{
    private readonly AssetLibrary _assets;
    private readonly NodeCatalog _nodes;
    private readonly RouteRuns _runs;

    /// <summary>Initializes a new instance of the <see cref="FollowRoute"/> class.</summary>
    /// <param name="assets">The project, which holds the routes.</param>
    /// <param name="nodes">The kinds of nodes, which the actions of the route are.</param>
    /// <param name="runs">The cursors of the runs under way.</param>
    public FollowRoute(AssetLibrary assets, NodeCatalog nodes, RouteRuns runs)
    {
        _assets = assets;
        _nodes = nodes;
        _runs = runs;
    }

    /// <inheritdoc/>
    public override async Task RunAsync(ActionContext context, RouteFollowValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("A route is followed in a step, which has a bot to move.");
        var stage = new GameRouteStage(context, bot, _nodes);

        if (_assets.Find<RouteAsset>(values.Route) is not { } route)
        {
            stage.Note("RouteMissing", values.Route);
            context.Stop();

            return;
        }

        var key = bot.State.Key;
        var cursor = _runs.Of(key, route, context.View.Started, context.Pass);
        RouteResult result;

        _runs.Begin(key, route, cursor);

        try
        {
            result = await RouteRunner.RunAsync(route, cursor, stage, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _runs.End(key, cursor);
        }

        if (result != RouteResult.Busy)
            _runs.Forget(key, route.Id);

        if (result == RouteResult.Failed)
            context.Stop();
    }
}
