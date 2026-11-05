namespace D3B.App.Controls.WorldMaps;

/// <summary>The way from a station to the next as the view draws it (D299).</summary>
/// <param name="Squares">The coordinates of the maps from the first station to the second, the maps crossed between them included; those the layer does not hold are left out.</param>
/// <param name="Kind">How it is drawn.</param>
/// <param name="IsClosing">Whether it leads from the last station back to the first, which a loop does and is drawn dashed.</param>
internal sealed record WorldMapLeg(IReadOnlyList<(int X, int Y)> Squares, WorldMapLegKind Kind, bool IsClosing);
