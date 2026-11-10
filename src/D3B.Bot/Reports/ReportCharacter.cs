namespace D3B.Bot.Reports;

/// <summary>The character a report is about, by its number: never its name (D410).</summary>
/// <param name="Id">The character's id on the game server.</param>
/// <param name="Breed">Its breed's id, if known.</param>
/// <param name="Level">Its level, if known.</param>
public sealed record ReportCharacter(long Id, int? Breed, int? Level);
