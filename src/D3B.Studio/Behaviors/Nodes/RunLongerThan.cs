namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The run gone on at least some minutes.</summary>
internal sealed class RunLongerThan : ConditionNode<DurationValues>
{
    public override bool Holds(BehaviorView view, DurationValues values)
    {
        return view.RunTime >= TimeSpan.FromMinutes(values.Minutes);
    }
}
