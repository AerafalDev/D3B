namespace D3B.App.ViewModels.Studio;

/// <summary>A scope of the search as its bar shows it: its name, lit when its results show (D328).</summary>
/// <param name="Scope">The scope.</param>
/// <param name="Name">Its name, in the app's language.</param>
/// <param name="IsOn">Whether its results show.</param>
internal sealed record SearchScopeChip(SearchScope Scope, string Name, bool IsOn);
