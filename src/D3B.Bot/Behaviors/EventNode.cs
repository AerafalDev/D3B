namespace D3B.Bot.Behaviors;

/// <summary>An event a module declares: something that happened between two states of the bot, which starts a rule (D298).</summary>
/// <typeparam name="TValues">Its values, a class the module describes as an activity's settings (D98).</typeparam>
/// <remarks>One per host, made once, called from the bot's loop after everything it handles: it must be quick, and keep nothing.</remarks>
public abstract class EventNode<TValues> : IEventNode
    where TValues : class, new()
{
    /// <summary>Tells whether the event happened.</summary>
    /// <param name="change">The bot before and after.</param>
    /// <param name="values">Its values.</param>
    /// <returns>Whether it happened.</returns>
    public abstract bool Happened(BotChange change, TValues values);

    bool IEventNode.Happened(BotChange change, object values)
    {
        return Happened(change, (TValues)values);
    }
}
