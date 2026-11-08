namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A fight the character was not in.</summary>
internal sealed class FightBegan : EventNode<NoValues>
{
    public override bool Happened(BotChange change, NoValues values)
    {
        return change.Before.State.Fight is null && change.After.State.Fight is not null;
    }
}
