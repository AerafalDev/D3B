using D3B.Bot.Runtime;

namespace D3B.Bot.Behaviors;

/// <summary>What an action plays with: the bot's behavior context, what the conditions read, and the behavior's run (D297).</summary>
public sealed class ActionContext
{
    private readonly IBehaviorControl _control;

    /// <summary>Gets the step's behavior context, which sends to the game; none in a rule, which plays only actions played at once.</summary>
    public BotContext? Bot { get; }

    /// <summary>Gets what the conditions read now.</summary>
    public BehaviorView View { get; }

    internal ActionContext(BotContext? bot, BehaviorView view, IBehaviorControl control)
    {
        Bot = bot;
        View = view;
        _control = control;
    }

    /// <summary>Pauses the bot as the player's hand does: the run waits until it is resumed (D66).</summary>
    public void Pause()
    {
        _control.Pause();
    }

    /// <summary>Ends the run, done, once the step running is over.</summary>
    public void Stop()
    {
        _control.Stop();
    }

    /// <summary>Writes a note in the bot's journal, which the app may show in a toast.</summary>
    /// <param name="text">The note.</param>
    public void Note(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _control.Note(text);
    }
}
