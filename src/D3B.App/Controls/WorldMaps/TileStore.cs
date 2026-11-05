namespace D3B.App.Controls.WorldMaps;

/// <summary>A bounded cache of the tiles a view draws, filled off the interface's thread (D308, D369).</summary>
/// <typeparam name="TTile">What a tile is once loaded, such as a decoded picture.</typeparam>
/// <remarks>
/// <para>
/// The view says which tiles it wants, nearest the centre first, each time it draws; the store loads those it lacks on a few
/// threads of the pool and forgets the rest of its queue, so that a quick pan never decodes what it already left. When the
/// pictures kept weigh more than the budget, the least recently drawn go, but never one the view wants nor one it pinned,
/// which is the whole world at its coarsest zoom, so that no hole shows while a finer tile loads.
/// </para>
/// <para>A tile that failed to load is not asked for again. Safe from any number of threads.</para>
/// </remarks>
internal sealed class TileStore<TTile> : IDisposable
    where TTile : class
{
    private readonly Func<WorldMapTileKey, TTile?> _load;
    private readonly Func<TTile, long> _costOf;
    private readonly Action<TTile> _release;
    private readonly long _budget;
    private readonly int _workers;
    private readonly object _gate = new();
    private readonly Dictionary<WorldMapTileKey, LinkedListNode<Kept>> _kept = [];
    private readonly LinkedList<Kept> _recent = new();
    private readonly HashSet<WorldMapTileKey> _failed = [];
    private readonly HashSet<WorldMapTileKey> _loading = [];
    private List<WorldMapTileKey> _queue = [];
    private HashSet<WorldMapTileKey> _wanted = [];
    private HashSet<WorldMapTileKey> _pinned = [];
    private int _running;
    private long _used;
    private bool _disposed;

    /// <summary>Gets the weight of the tiles kept, by their cost.</summary>
    public long Used
    {
        get
        {
            lock (_gate)
            {
                return _used;
            }
        }
    }

    /// <summary>Gets how many tiles are kept.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _kept.Count;
            }
        }
    }

    /// <summary>Raised, on a thread of the pool, each time a tile has loaded.</summary>
    public event Action? Loaded;

    /// <summary>Initializes a new instance of the <see cref="TileStore{TTile}"/> class.</summary>
    /// <param name="load">Loads a tile, or returns <see langword="null"/> when it cannot be; called on the pool's threads.</param>
    /// <param name="costOf">Weighs a tile, in bytes.</param>
    /// <param name="release">Frees a tile the store lets go of, such as disposing a picture; called outside the store's lock.</param>
    /// <param name="budget">The weight of tiles to keep, in bytes.</param>
    /// <param name="workers">How many tiles load at once.</param>
    public TileStore(Func<WorldMapTileKey, TTile?> load, Func<TTile, long> costOf, Action<TTile> release, long budget, int workers)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(costOf);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentOutOfRangeException.ThrowIfLessThan(budget, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);

        _load = load;
        _costOf = costOf;
        _release = release;
        _budget = budget;
        _workers = workers;
    }

    /// <summary>Gets a tile if it is kept, which makes it the most recently used.</summary>
    /// <param name="key">The tile.</param>
    /// <param name="tile">The tile, when kept.</param>
    /// <returns>Whether it is kept.</returns>
    public bool TryGet(WorldMapTileKey key, out TTile tile)
    {
        lock (_gate)
        {
            if (_kept.TryGetValue(key, out var node))
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
                tile = node.Value.Tile;

                return true;
            }
        }

        tile = null!;

        return false;
    }

    /// <summary>Says which tiles the view draws now, replacing what was asked before.</summary>
    /// <param name="pinned">The tiles to keep whatever the budget, loaded first.</param>
    /// <param name="wanted">The tiles the view needs, in the order to load them.</param>
    public void Want(IReadOnlyList<WorldMapTileKey> pinned, IReadOnlyList<WorldMapTileKey> wanted)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        ArgumentNullException.ThrowIfNull(wanted);

        var starts = 0;

        lock (_gate)
        {
            if (_disposed)
                return;

            _pinned = [.. pinned];
            _wanted = [.. pinned, .. wanted];
            _queue = [.. pinned.Concat(wanted).Distinct().Where(key => !_kept.ContainsKey(key) && !_loading.Contains(key) && !_failed.Contains(key))];
            starts = Math.Min(_workers - _running, _queue.Count);
            _running += starts;
        }

        for (var i = 0; i < starts; i++)
            Task.Run(Work);
    }

    /// <summary>Lets go of every tile kept; the store loads nothing more.</summary>
    public void Dispose()
    {
        List<TTile> freed;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _queue = [];
            freed = [.. _recent.Select(kept => kept.Tile)];
            _recent.Clear();
            _kept.Clear();
            _used = 0;
        }

        foreach (var tile in freed)
            _release(tile);
    }

    private void Work()
    {
        while (true)
        {
            WorldMapTileKey key;

            lock (_gate)
            {
                if (_disposed || _queue.Count == 0)
                {
                    _running--;

                    return;
                }

                key = _queue[0];
                _queue.RemoveAt(0);
                _loading.Add(key);
            }

            TTile? tile = null;

            try
            {
                tile = _load(key);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or KeyNotFoundException or ArgumentException or NotSupportedException or InvalidOperationException)
            {
                // A tile the pack lacks or that does not decode stays a hole: the coarser tiles under it show.
            }

            List<TTile> freed = [];

            lock (_gate)
            {
                _loading.Remove(key);

                if (tile is null)
                {
                    _failed.Add(key);
                }
                else if (_disposed)
                {
                    freed.Add(tile);
                }
                else
                {
                    var cost = _costOf(tile);
                    var node = _recent.AddFirst(new Kept(key, tile, cost));

                    _kept[key] = node;
                    _used += cost;
                    Evict(freed);
                }
            }

            foreach (var gone in freed)
                _release(gone);

            if (tile is not null)
                Loaded?.Invoke();
        }
    }

    private void Evict(List<TTile> freed)
    {
        var node = _recent.Last;

        while (_used > _budget && node is not null)
        {
            var previous = node.Previous;

            if (!_wanted.Contains(node.Value.Key) && !_pinned.Contains(node.Value.Key))
            {
                _recent.Remove(node);
                _kept.Remove(node.Value.Key);
                _used -= node.Value.Cost;
                freed.Add(node.Value.Tile);
            }

            node = previous;
        }
    }

    private readonly record struct Kept(WorldMapTileKey Key, TTile Tile, long Cost);
}
