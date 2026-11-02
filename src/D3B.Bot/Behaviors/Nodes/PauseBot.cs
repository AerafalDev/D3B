namespace D3B.Bot.Behaviors.Nodes;

/// <summary>The bot paused, as the player's hand does, until it is resumed (D66).</summary>
internal sealed class PauseBot : InstantAction<NoValues>
{
    public override void Apply(ActionContext context, NoValues values)
    {
        context.Pause();
    }
}
