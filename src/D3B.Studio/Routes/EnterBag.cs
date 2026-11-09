using D3B.Bot.Movement;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Enters the character's haven bag from the map it stands on (D384).</summary>
internal sealed class EnterBag : ActionNode<NoValues>
{
    public override async Task RunAsync(ActionContext context, NoValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("The haven bag is entered in a step, which has a bot to move.");
        var outcome = await bot.EnterHavenBagAsync(cancellationToken).ConfigureAwait(false);

        if (outcome is not (MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere))
            throw new RouteActionException(RouteActions.HavenBag, outcome.ToString());
    }
}
