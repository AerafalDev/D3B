namespace D3B.App.Controls.WorldMaps;

/// <summary>A zone that is in the world but not being edited: the maps it holds, tinted (D397).</summary>
/// <param name="Id">The zone's id.</param>
/// <param name="Name">The zone's name.</param>
/// <param name="Squares">The coordinates of the maps it holds in the layer shown.</param>
/// <param name="IsSelected">Whether the zone is the one selected in the studio.</param>
internal sealed record WorldMapBackdropZone(string Id, string Name, IReadOnlyList<(int X, int Y)> Squares, bool IsSelected)
{
    private HashSet<(int X, int Y)>? _held;

    /// <summary>Tells whether the zone holds the map at coordinates.</summary>
    /// <param name="x">The map's horizontal coordinate.</param>
    /// <param name="y">The map's vertical coordinate.</param>
    /// <returns>Whether it holds it.</returns>
    public bool Holds(int x, int y)
    {
        // Asked at each move of the pointer: a set, built once.
        _held ??= [.. Squares];

        return _held.Contains((x, y));
    }
}
