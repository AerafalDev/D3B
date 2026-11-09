namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A fight over, won or lost as asked.</summary>
internal sealed class FightOver : EventNode<FightValues>
{
    public override bool Happened(BotChange change, FightValues values)
    {
        return change.Before.FightsEnded != change.After.FightsEnded
            && change.After.LastFightEnd is { } end
            && LastFight.Matches(values.Result, end.Outcome);
    }
}
