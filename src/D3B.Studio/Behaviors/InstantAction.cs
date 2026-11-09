namespace D3B.Studio.Behaviors;

/// <summary>An action played at once, never waiting, such as a note or a pause: it may play in a rule as in a state (D297).</summary>
/// <typeparam name="TValues">Its values, a class the module describes as an activity's settings (D98).</typeparam>
/// <remarks>Played in a rule, it runs on the bot's loop: it must be quick, and must not throw.</remarks>
public abstract class InstantAction<TValues> : ActionNode<TValues>
    where TValues : class, new()
{
    /// <inheritdoc/>
    public sealed override bool IsInstant => true;

    /// <summary>Plays the action.</summary>
    /// <param name="context">What it plays with; its <see cref="ActionContext.Bot"/> is none in a rule.</param>
    /// <param name="values">Its values.</param>
    public abstract void Apply(ActionContext context, TValues values);

    /// <inheritdoc/>
    public sealed override Task RunAsync(ActionContext context, TValues values, CancellationToken cancellationToken)
    {
        Apply(context, values);

        return Task.CompletedTask;
    }
}
