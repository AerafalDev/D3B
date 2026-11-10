namespace D3B.App.ViewModels.Reports;

/// <summary>A character a report may be about, by what the player knows it by.</summary>
/// <param name="Id">The character's id on the game server.</param>
/// <param name="AccountId">Its account's id, which with the id finds its bot.</param>
/// <param name="Name">Its name.</param>
/// <param name="Detail">The line under its name: its level, whether it is online.</param>
internal sealed record ReportCharacterChoice(long AccountId, long Id, string Name, string Detail);
