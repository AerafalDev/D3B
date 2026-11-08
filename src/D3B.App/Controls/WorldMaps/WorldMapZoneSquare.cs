namespace D3B.App.Controls.WorldMaps;

/// <summary>A square of a zone as the view draws it (D394).</summary>
/// <param name="X">The map's horizontal coordinate.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="IsLeftOut">Whether the zone leaves the square's map out, rather than holds it.</param>
internal sealed record WorldMapZoneSquare(int X, int Y, bool IsLeftOut);
