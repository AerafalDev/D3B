namespace D3B.Bot.Activities;

/// <summary>What an activity does once an interruption cancelled its step (D87).</summary>
public enum InterruptionResponse
{
    /// <summary>Waits until the bot can act again, recovers the character, and goes on.</summary>
    Resume,

    /// <summary>Ends the activity.</summary>
    Stop,
}
