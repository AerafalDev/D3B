using D3B.Bot.Behaviors;
using D3B.Bot.Fighting;

namespace D3B.Bot.Routes;

/// <summary>Fights the groups of monsters of the map the route stands on that a filter lets through, then goes on (D384).</summary>
internal sealed class FightHere : ActionNode<RouteFightValues>
{
    public override async Task RunAsync(ActionContext context, RouteFightValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("A fight is played in a step, which has a bot to move.");

        await MapFight.FightAsync(bot, values.Monsters, values.LeastMonsters, values.MostMonsters, cancellationToken).ConfigureAwait(false);
    }
}
