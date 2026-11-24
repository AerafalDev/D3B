namespace D3B.App.Hosting;

/// <summary>The studio's layout as <c>app.json</c> keeps it: each edge where it was left, the parts shown and the side bar's view (D321, D513).</summary>
/// <param name="LeftWidth">The side bar's width, in pixels.</param>
/// <param name="RightWidth">The Details panel's width, in pixels.</param>
/// <param name="BottomHeight">The bottom panel's height, in pixels.</param>
/// <param name="Closed">The panels an older D3B closed, by their name, read and never written: <c>Outliner</c> and <c>Assets</c> both closed hide the side bar.</param>
/// <param name="Shown">The parts shown, by their name: <c>Side</c>, <c>Bottom</c>; absent in an older file. The Details are never kept: a selection shows them.</param>
/// <param name="View">The side bar's view, by its name.</param>
/// <param name="AdvancedTools">Whether the advanced tools show: the protocol.</param>
internal sealed record StudioLayoutState(double LeftWidth, double RightWidth, double BottomHeight, IReadOnlyList<string>? Closed = null, IReadOnlyList<string>? Shown = null, string? View = null, bool AdvancedTools = false);
