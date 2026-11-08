namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A flag on, such as a variable's: what a state's conditions read of a variable.</summary>
internal sealed class FlagIsOn : ConditionNode<FlagValues>
{
    public override bool Holds(BehaviorView view, FlagValues values)
    {
        return values.First;
    }
}
