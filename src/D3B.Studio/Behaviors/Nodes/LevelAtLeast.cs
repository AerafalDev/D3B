namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The character's level, or a profession's, at least a level.</summary>
internal sealed class LevelAtLeast : ConditionNode<LevelValues>
{
    public override bool Holds(BehaviorView view, LevelValues values)
    {
        if (values.Job is { } job)
            return view.State.Jobs.TryGetValue(job, out var known) && known.Level >= values.Level;

        return view.State.Level >= values.Level;
    }
}
