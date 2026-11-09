using D3B.Bot.Movement;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Leaves the map by a side or a cell of its edge, whether or not the world's graph goes that way (D384).</summary>
internal sealed class ForceExit : ActionNode<RouteExitValues>
{
    public override async Task RunAsync(ActionContext context, RouteExitValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("An exit is forced in a step, which has a bot to move.");
        var direction = values.Direction switch
        {
            RouteDirection.Top => ForcedMoves.North,
            RouteDirection.Right => ForcedMoves.East,
            RouteDirection.Bottom => ForcedMoves.South,
            _ => ForcedMoves.West,
        };

        var outcome = await bot.LeaveAsync(direction, values.Cell, cancellationToken).ConfigureAwait(false);

        if (outcome is not (MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere))
            throw new RouteActionException(RouteActions.Exit, outcome.ToString());
    }
}
