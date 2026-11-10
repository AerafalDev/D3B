namespace D3B.App.Controls.WorldMaps;

/// <summary>What the world's map draws over its image: the route's stations and the ways between them, or a zone's squares, and what else the world holds.</summary>
/// <param name="Stations">The stations, in the route's order; a station's number is its place here plus one.</param>
/// <param name="Legs">The ways, in order.</param>
/// <param name="Squares">The squares of a zone, or <see langword="null"/> for a route's scene.</param>
/// <param name="Backdrop">The other routes and zones and the characters, or <see langword="null"/> for none (D397).</param>
internal sealed record WorldMapScene(IReadOnlyList<WorldMapStation> Stations, IReadOnlyList<WorldMapLeg> Legs, IReadOnlyList<WorldMapZoneSquare>? Squares = null, WorldMapBackdrop? Backdrop = null)
{
    /// <summary>Gets the station lit because the Sequencer's pointer is on its column, or -1 for none (D419).</summary>
    public int LitStep { get; init; } = -1;

    /// <summary>Gets a scene that draws nothing.</summary>
    public static WorldMapScene Empty { get; } = new([], []);
}
