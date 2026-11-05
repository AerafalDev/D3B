namespace D3B.App.Controls.WorldMaps;

/// <summary>What the world's map draws over its image: the route's stations and the ways between them.</summary>
/// <param name="Stations">The stations, in the route's order; a station's number is its place here plus one.</param>
/// <param name="Legs">The ways, in order.</param>
internal sealed record WorldMapScene(IReadOnlyList<WorldMapStation> Stations, IReadOnlyList<WorldMapLeg> Legs)
{
    /// <summary>Gets a scene that draws nothing.</summary>
    public static WorldMapScene Empty { get; } = new([], []);
}
