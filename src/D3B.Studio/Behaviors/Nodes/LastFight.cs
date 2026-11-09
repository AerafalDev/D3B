using D3B.Protocol.Game;

namespace D3B.Studio.Behaviors.Nodes;

/// <summary>How the last fight the bot saw end ended for the character.</summary>
internal sealed class LastFight : ConditionNode<FightValues>
{
    public static bool Matches(FightResult result, FightOutcome outcome)
    {
        return result switch
        {
            FightResult.Won => outcome is not FightOutcome.Lost,
            FightResult.Lost => outcome is FightOutcome.Lost,
            _ => true,
        };
    }

    public override bool Holds(BehaviorView view, FightValues values)
    {
        return view.LastFightEnd is { } end && Matches(values.Result, end.Outcome);
    }
}
