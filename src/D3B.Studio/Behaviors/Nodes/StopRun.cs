namespace D3B.Studio.Behaviors.Nodes;

/// <summary>The run over, done.</summary>
internal sealed class StopRun : InstantAction<NoValues>
{
    public override void Apply(ActionContext context, NoValues values)
    {
        context.Stop();
    }
}
