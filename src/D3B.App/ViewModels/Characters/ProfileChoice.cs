namespace D3B.App.ViewModels.Characters;

/// <summary>A profile a character may be given, as the inspector's and the cards' menus list it (D324).</summary>
/// <param name="Id">The profile asset's id, or <see langword="null"/> for none.</param>
/// <param name="Name">Its name, or what the app says for none.</param>
/// <param name="Detail">The behavior it is a variant of, and how many values it changes or that it is given.</param>
/// <param name="IsGiven">Whether it is given with D3B, read only.</param>
/// <param name="ActivityId">An activity played with its defaults, for the session only, as the bots' cards started one (D99), instead of a profile.</param>
internal sealed record ProfileChoice(string? Id, string Name, string Detail, bool IsGiven, string? ActivityId = null);
