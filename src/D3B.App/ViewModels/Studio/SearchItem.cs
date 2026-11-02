namespace D3B.App.ViewModels.Studio;

/// <summary>Something the search finds: an asset, a character, a command or a setting, and what choosing it does (D328).</summary>
/// <param name="Scope">What it is, which groups it.</param>
/// <param name="Key">What it is, the same each time, by which the recent items are kept.</param>
/// <param name="Title">Its name, which the query is looked for in.</param>
/// <param name="Detail">What it is or where, faint beside its name.</param>
/// <param name="Icon">Its IntelliJ icon by its key, or <see langword="null"/> for a character's initials.</param>
/// <param name="Initials">A character's initials, else empty.</param>
/// <param name="Keys">Its shortcut, said in the app's language, else empty.</param>
/// <param name="Run">What choosing it does: opens its tab, plays its command or opens its setting's section.</param>
internal sealed record SearchItem(SearchScope Scope, string Key, string Title, string Detail, string? Icon, string Initials, string Keys, Action Run);
