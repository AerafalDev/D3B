using D3B.Bot.Npcs;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>Talks to an NPC of the map the route stands on and gives it the replies chosen (D384).</summary>
internal sealed class TalkToNpc : ActionNode<RouteNpcValues>
{
    public override async Task RunAsync(ActionContext context, RouteNpcValues values, CancellationToken cancellationToken)
    {
        var bot = context.Bot ?? throw new InvalidOperationException("An NPC is talked to in a step, which has a bot to move.");

        if (values.Npc is not { } npc)
            throw new RouteActionException(RouteActions.Npc, "NoNpc");

        var outcome = await bot.TalkAsync(npc, values.Replies, cancellationToken).ConfigureAwait(false);

        if (outcome is not NpcTalkOutcome.Done)
            throw new RouteActionException(RouteActions.Npc, outcome.ToString());
    }
}
