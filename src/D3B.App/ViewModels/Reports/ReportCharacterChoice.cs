using Avalonia.Media;

namespace D3B.App.ViewModels.Reports;

/// <summary>A character a report may be about, by what the player knows it by.</summary>
/// <param name="Id">The character's id on the game server.</param>
/// <param name="AccountId">Its account's id, which with the id finds its bot.</param>
/// <param name="Name">Its name.</param>
/// <param name="Detail">The line under its name: its level, whether it is online.</param>
/// <param name="Initials">The first letters of its name, its portrait without a head.</param>
/// <param name="Head">Its class's head, if known and in the pack.</param>
/// <param name="PortraitColor">Its portrait's colour, its breed's.</param>
/// <param name="IsDimmed">Whether its portrait is dimmed, as the Outliner's are for a character offline.</param>
internal sealed record ReportCharacterChoice(long AccountId, long Id, string Name, string Detail, string Initials = "", IImage? Head = null, IBrush? PortraitColor = null, bool IsDimmed = false);
