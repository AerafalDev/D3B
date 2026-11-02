namespace D3B.App.Hosting;

/// <summary>The studio's layout as <c>app.json</c> keeps it: each edge where it was left and the panels closed (D321).</summary>
/// <param name="LeftWidth">The left column's width, in pixels.</param>
/// <param name="RightWidth">The inspector's width, in pixels.</param>
/// <param name="LeftShare">The characters' share of the left column's height, the assets having the rest.</param>
/// <param name="BottomHeight">The bottom panel's height, in pixels.</param>
/// <param name="Closed">The panels closed, by their name: <c>Characters</c>, <c>Assets</c>, <c>Bottom</c>, <c>Inspector</c>.</param>
internal sealed record StudioLayoutState(double LeftWidth, double RightWidth, double LeftShare, double BottomHeight, IReadOnlyList<string> Closed);
