namespace D3B.App.Controls.WorldMaps;

/// <summary>A route that is in the world but not being edited: its steps joined by a straight line, fainter than the edited route's (D397).</summary>
/// <param name="Id">The route's id.</param>
/// <param name="Name">The route's name.</param>
/// <param name="Steps">The steps whose maps lie in the layer shown, in the route's order.</param>
/// <param name="IsLoop">Whether the route goes from its last step back to its first.</param>
/// <param name="IsSelected">Whether the route is the one selected in the studio.</param>
internal sealed record WorldMapBackdropRoute(string Id, string Name, IReadOnlyList<WorldMapBackdropStep> Steps, bool IsLoop, bool IsSelected);
