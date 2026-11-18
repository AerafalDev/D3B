namespace D3B.Bot.Characters;

/// <summary>The exchange between the character and another character, from its asking to its end (D435, D441).</summary>
/// <param name="SourceId">The character who asked for it.</param>
/// <param name="TargetId">The character asked.</param>
/// <param name="IsOpen">Whether the one asked accepted, and the exchange opened for both.</param>
/// <param name="Changes">How many times objects went into it, by either side: the step its readiness names, measured as 3 after 3 moves (D441).</param>
/// <param name="IsReady">Whether the character said it is ready to conclude.</param>
/// <param name="IsOtherReady">Whether the other said it is ready to conclude.</param>
public sealed record PlayerExchange(long SourceId, long TargetId, bool IsOpen = false, int Changes = 0, bool IsReady = false, bool IsOtherReady = false);
