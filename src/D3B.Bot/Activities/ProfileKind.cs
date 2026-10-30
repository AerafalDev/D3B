namespace D3B.Bot.Activities;

/// <summary>What a profile is: an activity the app starts on a bot, or a fight's strategy a character plays (D233).</summary>
public enum ProfileKind
{
    /// <summary>An activity, started on a bot with its settings.</summary>
    Activity,

    /// <summary>A fight's strategy, never started: the AI fights a character's fights with it.</summary>
    Strategy,
}
