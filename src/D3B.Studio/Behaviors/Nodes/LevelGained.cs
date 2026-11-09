namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A level gained, the character's or a profession's.</summary>
internal sealed class LevelGained : EventNode<LevelValues>
{
    public override bool Happened(BotChange change, LevelValues values)
    {
        if (!change.IsSameConnection)
            return false;

        if (values.Job is { } job)
        {
            return change.Before.State.Jobs.TryGetValue(job, out var before)
                && change.After.State.Jobs.TryGetValue(job, out var after)
                && after.Level > before.Level;
        }

        return change.After.State.Level > change.Before.State.Level;
    }
}
