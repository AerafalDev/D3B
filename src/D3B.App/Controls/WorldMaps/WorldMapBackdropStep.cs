namespace D3B.App.Controls.WorldMaps;

/// <summary>A step of a route that is not the one being edited, as a small node on its line (D397).</summary>
/// <param name="X">The map's horizontal coordinate.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="Index">The step's place in its route, from 0.</param>
/// <param name="Actions">What the step does, one entry for each kind.</param>
internal sealed record WorldMapBackdropStep(int X, int Y, int Index, IReadOnlyList<WorldMapActionKind> Actions);
