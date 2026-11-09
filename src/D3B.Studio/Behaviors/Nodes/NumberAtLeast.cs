namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A number at least a bound, such as a variable's that counts.</summary>
internal sealed class NumberAtLeast : ConditionNode<NumberValues>
{
    public override bool Holds(BehaviorView view, NumberValues values)
    {
        return values.Value >= values.Least;
    }
}
