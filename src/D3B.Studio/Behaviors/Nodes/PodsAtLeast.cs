namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The pods the character carries, at least a share of what it may carry: full, as harvesting says it (D87).</summary>
internal sealed class PodsAtLeast : ConditionNode<PercentValues>
{
    public override bool Holds(BehaviorView view, PercentValues values)
    {
        return view.State.MaxWeight > 0 && view.State.Weight * 100L >= (long)values.Percent * view.State.MaxWeight;
    }
}
