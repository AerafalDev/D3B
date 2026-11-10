using D3B.Studio.Assets;

namespace D3B.Studio.Routes;

/// <summary>Where a run is on its route: which anchor it goes to next, and how far through that anchor's actions it is (D379).</summary>
/// <remarks>
/// <para>
/// The course is the route's <see cref="RouteMode"/>: a loop goes round its anchors for ever, a one-way route goes through
/// them once, and a route there and back goes to the last and back to the first. A single anchor is visited once whatever the
/// course, a loop of it having nothing to go round.
/// </para>
/// <para>
/// The cursor is the run's memory: a behavior plays its actions again from the first after an interruption, and a route
/// which started over would walk back to its first anchor each time a fight cut it.
/// </para>
/// </remarks>
public sealed class RouteCursor
{
    private readonly Lock _gate = new();
    private readonly int _count;
    private readonly RouteMode _mode;
    private readonly long _firstLap;
    private long _visit;
    private int _played;
    private bool _arrived;
    private long _announced = -1;

    /// <summary>Initializes a new instance of the <see cref="RouteCursor"/> class.</summary>
    /// <param name="anchors">How many anchors the route has.</param>
    /// <param name="mode">How it goes through them.</param>
    /// <param name="firstLap">
    /// How many laps came before this run's: a step that repeats follows a loop one lap at a time, each pass a cursor of its own
    /// that goes through the anchors once (D420), and says which lap it is.
    /// </param>
    public RouteCursor(int anchors, RouteMode mode, long firstLap = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(anchors);
        ArgumentOutOfRangeException.ThrowIfNegative(firstLap);

        _count = anchors;
        _mode = anchors < 2 ? RouteMode.OneWay : mode;
        _firstLap = firstLap;
    }

    /// <summary>Gets whether the run has been to every anchor its course asks for.</summary>
    public bool IsDone => _count == 0 || _mode switch
    {
        RouteMode.Loop => false,
        RouteMode.RoundTrip => _visit >= (2 * _count) - 1,
        _ => _visit >= _count,
    };

    /// <summary>Gets the anchor to go to, or -1 once the run is done.</summary>
    public int Anchor
    {
        get
        {
            if (IsDone)
                return -1;

            return _mode switch
            {
                RouteMode.Loop => (int)(_visit % _count),
                RouteMode.RoundTrip => _visit < _count ? (int)_visit : (int)((2 * _count) - 2 - _visit),
                _ => (int)_visit,
            };
        }
    }

    /// <summary>Gets the anchor the run goes to after the one it is at, or -1 when it is the last: the route's next step, which a zaap is taken towards.</summary>
    public int NextAnchor
    {
        get
        {
            var after = new RouteCursor(_count, _mode) { _visit = _visit + 1 };

            return after.Anchor;
        }
    }

    /// <summary>Gets how many anchors the run has been to, a loop's counted again at each lap.</summary>
    public long Visits => _visit;

    /// <summary>Gets how many laps of a loop the run has finished.</summary>
    public long Laps => _firstLap + (_mode == RouteMode.Loop && _count > 0 ? _visit / _count : 0);

    /// <summary>Gets whether a route there and back is on its way back, from the last anchor to the first.</summary>
    public bool Returning => _mode == RouteMode.RoundTrip && _visit >= _count;

    /// <summary>Gets whether the character is on the map of the anchor it goes to, rather than on its way there (D419).</summary>
    public bool Arrived => _arrived;

    /// <summary>Gets how many of the anchor's actions the run has played.</summary>
    public int Played => _played;

    /// <summary>Notes that one more action of the anchor was played.</summary>
    public void ActionPlayed()
    {
        lock (_gate)
        {
            _played++;
        }
    }

    /// <summary>Notes that the character is on the anchor's map.</summary>
    public void Arrive()
    {
        lock (_gate)
        {
            _arrived = true;
        }
    }

    /// <summary>Tells whether the arrival at this anchor is the first to be said: a run cut and resumed on the same anchor says it once.</summary>
    /// <returns><see langword="true"/> once for each visit of an anchor.</returns>
    public bool TryAnnounce()
    {
        lock (_gate)
        {
            if (_announced == _visit)
                return false;

            _announced = _visit;

            return true;
        }
    }

    /// <summary>Notes that the character goes to the anchor, as a run does again after a cut.</summary>
    public void Depart()
    {
        lock (_gate)
        {
            _arrived = false;
        }
    }

    /// <summary>Goes on to the next anchor: the run was at this one, or passed it.</summary>
    public void Advance()
    {
        lock (_gate)
        {
            if (IsDone)
                return;

            _visit++;
            _played = 0;
            _arrived = false;
        }
    }

    /// <summary>Reads where the run is in one go, for another thread than the run's.</summary>
    /// <returns>The run's place.</returns>
    public RouteCursorSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new RouteCursorSnapshot(Anchor, _arrived, _played, _visit, Laps, Returning, IsDone);
        }
    }
}
