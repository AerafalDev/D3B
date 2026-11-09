using D3B.App.Controls.WorldMaps;

namespace D3B.App.ViewModels.Routes;

/// <summary>What the map draws of a route, with which step each station is and which leg each path is, so that a press on the map finds its step.</summary>
/// <param name="Scene">What the map draws over its image.</param>
/// <param name="StationAnchors">For each station of the scene, the place of its step in the route.</param>
/// <param name="SceneLegs">For each path of the scene, the place of its leg in the route.</param>
internal sealed record RouteSceneParts(WorldMapScene Scene, IReadOnlyList<int> StationAnchors, IReadOnlyList<int> SceneLegs);
