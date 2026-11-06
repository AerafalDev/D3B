using D3B.Bot.Assets;
using D3B.Bot.Movement;

namespace D3B.Bot.Routes;

/// <summary>Goes through a route: to each anchor in the order of its course, playing the actions there (D379).</summary>
/// <remarks>
/// <para>
/// The way to an anchor is the character's own (<see cref="IRouteStage.TravelAsync"/>), so that a zaap it knows enters the
/// path where it saves enough changes, and one it lacks does not: the editor's path assumed them all. An anchor no path
/// reaches is passed, or fails the route, as the route says. A run that finds no anchor reachable in a whole lap of a loop
/// stops, so that a loop of unreachable maps does not spin.
/// </para>
/// <para>
/// An action the host does not know fails the route before it moves, so that it does not walk to the third map to say so; one
/// that cannot be played on its map fails it there.
/// </para>
/// </remarks>
internal static class RouteRunner
{
    private const int BusyRetries = 10;
    private static readonly TimeSpan _busyWait = TimeSpan.FromSeconds(2);

    /// <summary>Goes through the route from where the cursor is.</summary>
    /// <param name="route">The route.</param>
    /// <param name="cursor">Where the run is, which the run moves.</param>
    /// <param name="stage">What the run does in the game.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>How it ended.</returns>
    public static async Task<RouteResult> RunAsync(RouteAsset route, RouteCursor cursor, IRouteStage stage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(stage);

        foreach (var anchor in route.Anchors)
        {
            foreach (var action in anchor.Actions)
            {
                if (!stage.CanPlay(action))
                {
                    stage.Note("RouteNotPlayed", action.Node);

                    return RouteResult.Failed;
                }
            }
        }

        var passed = 0;
        var busy = 0;

        while (!cursor.IsDone)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var anchor = route.Anchors[cursor.Anchor];

            switch (await stage.TravelAsync(anchor.MapId, cancellationToken).ConfigureAwait(false))
            {
                case TravelOutcome.Arrived:
                    passed = 0;
                    busy = 0;

                    long? next = cursor.NextAnchor is >= 0 and var following ? route.Anchors[following].MapId : null;

                    for (; cursor.Played < anchor.Actions.Length; cursor.ActionPlayed())
                    {
                        if (!await stage.PlayAsync(anchor.Actions[cursor.Played], next, cancellationToken).ConfigureAwait(false))
                            return RouteResult.Failed;
                    }

                    cursor.Advance();
                    break;
                case TravelOutcome.Busy:
                    if (++busy > BusyRetries)
                        return RouteResult.Busy;

                    await stage.WaitAsync(_busyWait, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    if (route.Unreachable == RouteUnreachable.Fail)
                    {
                        stage.Note("RouteUnreachableFails", cursor.Anchor + 1, anchor.MapId);

                        return RouteResult.Failed;
                    }

                    stage.Note("RouteUnreachablePassed", cursor.Anchor + 1, anchor.MapId);
                    cursor.Advance();

                    // A lap of a loop without one arrival: no anchor can be reached from here, and going round again would not change it.
                    if (++passed >= route.Anchors.Length)
                    {
                        stage.Note("RouteNothingReachable");

                        return RouteResult.Failed;
                    }

                    break;
            }
        }

        return RouteResult.Done;
    }
}
