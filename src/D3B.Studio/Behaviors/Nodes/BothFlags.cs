namespace D3B.Studio.Behaviors.Nodes;

/// <summary>Two flags both on: "and", on a canvas, where wires bring conditions.</summary>
internal sealed class BothFlags : ConditionNode<FlagValues>
{
    public override bool Holds(BehaviorView view, FlagValues values)
    {
        return values.First && values.Second;
    }
}
