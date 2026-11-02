namespace D3B.Bot.Behaviors.Nodes;

/// <summary>A wait of some seconds, by the bot's clock: played whole, and again if an interruption cut it.</summary>
internal sealed class Wait : ActionNode<DurationValues>
{
    public override Task RunAsync(ActionContext context, DurationValues values, CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromSeconds(values.Seconds), context.Bot!.Time, cancellationToken);
    }
}
