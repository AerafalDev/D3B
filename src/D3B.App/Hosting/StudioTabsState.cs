namespace D3B.App.Hosting;

/// <summary>The editor's tabs as <c>app.json</c> keeps them, which come back at the next start (D542).</summary>
/// <param name="Open">The tabs open, by their keys, in their order: <c>home</c>, <c>settings</c>, <c>profile:</c> and a profile's id…</param>
/// <param name="Shown">The tab shown, by its key, or <see langword="null"/> for none.</param>
/// <param name="Bottom">The bottom panel's tab, by its name, or <see langword="null"/> for the journal.</param>
internal sealed record StudioTabsState(IReadOnlyList<string> Open, string? Shown = null, string? Bottom = null);
