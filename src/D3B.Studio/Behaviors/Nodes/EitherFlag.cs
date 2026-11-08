namespace D3B.Studio.Behaviors.Nodes;

/// <summary>One of two flags on at least: "or", on a canvas, where wires bring conditions.</summary>
internal sealed class EitherFlag : ConditionNode<FlagValues>
{
    public override bool Holds(BehaviorView view, FlagValues values)
    {
        return values.First || values.Second;
    }
}
