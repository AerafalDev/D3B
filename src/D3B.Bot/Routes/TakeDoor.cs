using D3B.Bot.Behaviors;
using D3B.Bot.Movement;

namespace D3B.Bot.Routes;

/// <summary>Takes the door whose element stands on a cell of the map (D384).</summary>
internal sealed class TakeDoor : ActionNode<RouteDoorValues>
{
    public override async Task RunAsync(ActionContext context, RouteDoorValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("A door is taken in a step, which has a bot to move.");

        if (values.Cell is not { } cell)
            throw new RouteActionException(RouteActions.Door, "NoCell");

        var outcome = await bot.TakeDoorAsync(cell, cancellationToken).ConfigureAwait(false);

        if (outcome is not (MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere))
            throw new RouteActionException(RouteActions.Door, outcome.ToString());
    }
}
