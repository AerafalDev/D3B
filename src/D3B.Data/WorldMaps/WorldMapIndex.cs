using System.Collections.Frozen;
using D3B.Data.World;

namespace D3B.Data.WorldMaps;

/// <summary>The maps standing on each coordinates of each world's layer: what the route editor clicks, types and marks (D299).</summary>
/// <remarks>
/// <para>
/// A layer is a world the client draws. A map of the world stands on it by its own coordinates; an inside, a map the client
/// shows on no world map such as a bank's, a cave or the haven bag, lives in the layer of the world its sub-area's other maps
/// are on, else the least world that has a map at its coordinates (D299). Of 3,676 insides on 3.7.13.17, 2,608 have a
/// sub-area on one world, and 2,499 of those stand on a square of it.
/// </para>
/// <para>
/// On coordinates that several maps share, those of the world come first, then the insides, each by ascending id. Built
/// once from the atlas, shared: nothing in it changes.
/// </para>
/// </remarks>
public sealed class WorldMapIndex
{
    private static readonly long[] _none = [];

    private readonly FrozenDictionary<(int Layer, int X, int Y), long[]> _maps;
    private readonly FrozenDictionary<long, (int Layer, int X, int Y)> _places;
    private readonly FrozenDictionary<int, WorldMapSquare[]> _squares;

    /// <summary>Initializes a new instance of the <see cref="WorldMapIndex"/> class.</summary>
    /// <param name="atlas">The atlas giving each map its coordinates, sub-area and world.</param>
    public WorldMapIndex(WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        var positions = atlas.Positions.Span;
        var worldsOfSubArea = new Dictionary<int, int>();
        var worldsAt = new Dictionary<(int X, int Y), int>();

        foreach (var position in positions)
        {
            if (position.WorldMap < 0)
                continue;

            // A sub-area on several worlds has no layer of its own for its insides: -2 marks it, so that the squares decide.
            worldsOfSubArea[position.SubAreaId] = worldsOfSubArea.TryGetValue(position.SubAreaId, out var known) && known != position.WorldMap ? -2 : position.WorldMap;
            worldsAt[(position.X, position.Y)] = worldsAt.TryGetValue((position.X, position.Y), out var least) ? Math.Min(least, position.WorldMap) : position.WorldMap;
        }

        var places = new Dictionary<long, (int Layer, int X, int Y)>(positions.Length);
        var lists = new Dictionary<(int Layer, int X, int Y), List<(bool Inside, long Map)>>();

        foreach (var position in positions)
        {
            int layer = position.WorldMap;
            var inside = layer < 0;

            if (inside)
            {
                layer = worldsOfSubArea.TryGetValue(position.SubAreaId, out var own) && own >= 0 ? own
                    : worldsAt.TryGetValue((position.X, position.Y), out var least) ? least
                    : -1;
            }

            if (layer < 0)
                continue;

            var square = (layer, (int)position.X, (int)position.Y);

            places[position.MapId] = square;

            if (!lists.TryGetValue(square, out var list))
                lists[square] = list = [];

            list.Add((inside, position.MapId));
        }

        _places = places.ToFrozenDictionary();
        _maps = lists.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.OrderBy(entry => entry.Inside).ThenBy(entry => entry.Map).Select(entry => entry.Map).ToArray());
        _squares = lists
            .GroupBy(pair => pair.Key.Layer)
            .ToFrozenDictionary(layer => layer.Key, layer => layer.Select(pair => new WorldMapSquare(pair.Key.X, pair.Key.Y, pair.Value.Count)).OrderBy(square => square.Y).ThenBy(square => square.X).ToArray());
    }

    /// <summary>Gets the maps standing on coordinates of a layer.</summary>
    /// <param name="layer">The world.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <returns>Their ids, the world's own first, then its insides, each by ascending id; none where the layer has no map.</returns>
    public ReadOnlySpan<long> MapsAt(int layer, int x, int y)
    {
        return _maps.TryGetValue((layer, x, y), out var maps) ? maps : _none;
    }

    /// <summary>Gets the coordinates of a layer that hold maps.</summary>
    /// <param name="layer">The world.</param>
    /// <returns>The squares, row by row; none for a world the client does not draw.</returns>
    public ReadOnlySpan<WorldMapSquare> SquaresOf(int layer)
    {
        return _squares.TryGetValue(layer, out var squares) ? squares : [];
    }

    /// <summary>Finds where a map is placed.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="layer">The world it lives in.</param>
    /// <param name="x">Its horizontal coordinate.</param>
    /// <param name="y">Its vertical coordinate.</param>
    /// <returns>Whether the map has a layer: an inside with neither a sub-area on a world nor a square of one has none.</returns>
    public bool TryPlace(long mapId, out int layer, out int x, out int y)
    {
        var found = _places.TryGetValue(mapId, out var place);

        (layer, x, y) = found ? place : (-1, 0, 0);

        return found;
    }
}
