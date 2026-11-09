namespace D3B.App.Controls.WorldMaps;

/// <summary>What lies under a point of the world's map.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="X">The map's horizontal coordinate, for anything but nothing.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="Station">The station's place in the scene, for a station.</param>
/// <param name="Leg">The leg's place in the scene, for a point of the path.</param>
/// <param name="Id">The key or id of the character, route or zone of the backdrop, for those.</param>
/// <param name="Step">The step's place in its route, for a step of the backdrop.</param>
internal readonly record struct WorldMapHit(WorldMapHitKind Kind, int X, int Y, int Station, int Leg, string? Id = null, int Step = -1)
{
    /// <summary>Gets a hit on nothing.</summary>
    public static WorldMapHit None { get; } = new(WorldMapHitKind.None, 0, 0, -1, -1);
}
