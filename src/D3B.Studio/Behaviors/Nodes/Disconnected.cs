namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The game connection that played the character lost, such as the client closed.</summary>
internal sealed class Disconnected : EventNode<NoValues>
{
    public override bool Happened(BotChange change, NoValues values)
    {
        return change.Before.IsConnected && !change.After.IsConnected;
    }
}
