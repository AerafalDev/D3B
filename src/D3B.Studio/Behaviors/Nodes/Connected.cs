namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A new game connection that plays the character, its state started over from what the server sends.</summary>
internal sealed class Connected : EventNode<NoValues>
{
    public override bool Happened(BotChange change, NoValues values)
    {
        return !change.Before.IsConnected && change.After.IsConnected;
    }
}
