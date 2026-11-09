namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The character on another map than it was.</summary>
internal sealed class MapChanged : EventNode<NoValues>
{
    public override bool Happened(BotChange change, NoValues values)
    {
        return change.Before.State.MapId is { } before && change.After.State.MapId is { } after && before != after;
    }
}
