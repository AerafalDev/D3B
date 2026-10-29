namespace D3B.Scripting;

/// <summary>A line of a script's console.</summary>
/// <param name="Time">When the script wrote it, by the bot's clock.</param>
/// <param name="Kind">What it says.</param>
/// <param name="Text">Its text.</param>
public readonly record struct ScriptLine(DateTimeOffset Time, ScriptLineKind Kind, string Text);
