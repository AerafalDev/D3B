namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A flag off: "not", on a canvas, where a wire brings a condition.</summary>
internal sealed class FlagIsOff : ConditionNode<FlagValues>
{
    public override bool Holds(BehaviorView view, FlagValues values)
    {
        return !values.First;
    }
}
