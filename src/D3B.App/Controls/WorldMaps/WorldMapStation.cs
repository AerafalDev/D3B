namespace D3B.App.Controls.WorldMaps;

/// <summary>An anchor of the route as the view draws it: a glass pill with a numbered disc and the name of the map's zone (D371).</summary>
/// <param name="X">The map's horizontal coordinate.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="Label">The name of the map's zone.</param>
/// <param name="IsPinned">Whether it was a map of the path that the user pinned.</param>
/// <param name="IsSelected">Whether it is the step chosen, outlined in the accent.</param>
internal sealed record WorldMapStation(int X, int Y, string Label, bool IsPinned, bool IsSelected);
