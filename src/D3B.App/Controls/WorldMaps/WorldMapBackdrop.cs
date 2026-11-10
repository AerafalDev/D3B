namespace D3B.App.Controls.WorldMaps;

/// <summary>What the world holds besides what is being edited: the other routes and zones, and the characters (D397).</summary>
/// <param name="Routes">The routes, each fainter than the edited route.</param>
/// <param name="Zones">The zones, tinted under the routes.</param>
/// <param name="Characters">The characters, over everything.</param>
internal sealed record WorldMapBackdrop(IReadOnlyList<WorldMapBackdropRoute> Routes, IReadOnlyList<WorldMapBackdropZone> Zones, IReadOnlyList<WorldMapCharacter> Characters)
{
    /// <summary>Gets the characters that follow a route, each marked on the step it goes to (D419).</summary>
    public IReadOnlyList<WorldMapRun> Runs { get; init; } = [];

    /// <summary>Gets a backdrop with nothing in it.</summary>
    public static WorldMapBackdrop Empty { get; } = new([], [], []);
}
