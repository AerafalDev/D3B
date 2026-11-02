namespace D3B.Bot.Behaviors.Nodes;

/// <summary>The character's life, as the server last said it, at least a share of its most.</summary>
internal sealed class LifeAtLeast : ConditionNode<PercentValues>
{
    public override bool Holds(BehaviorView view, PercentValues values)
    {
        return view.State is { Life: { } life, MaxLife: > 0 and var most } && life * 100L >= (long)values.Percent * most;
    }
}
