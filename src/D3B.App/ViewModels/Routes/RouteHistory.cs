namespace D3B.App.ViewModels.Routes;

/// <summary>What a route editor can take back and do again: the states the route went through, a gesture counted as one (D299).</summary>
internal sealed class RouteHistory
{
    private const int Depth = 200;

    private readonly List<RouteSnapshot> _undo = [];
    private readonly List<RouteSnapshot> _redo = [];
    private bool _inGesture;
    private bool _gestureRecorded;

    /// <summary>Initializes a new instance of the <see cref="RouteHistory"/> class.</summary>
    /// <param name="current">The route as it is.</param>
    public RouteHistory(RouteSnapshot current)
    {
        Current = current;
    }

    /// <summary>Gets the route as the history last saw it.</summary>
    public RouteSnapshot Current { get; private set; }

    /// <summary>Gets whether there is a state to go back to.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether there is a state taken back to do again.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Starts a gesture, a press and what it does until it is let go: the changes it makes are one to undo.</summary>
    public void BeginGesture()
    {
        _inGesture = true;
        _gestureRecorded = false;
    }

    /// <summary>Ends the gesture.</summary>
    public void EndGesture()
    {
        _inGesture = false;
        _gestureRecorded = false;
    }

    /// <summary>Forgets what was done: the route is another, and undoing onto it would make a mixture.</summary>
    /// <param name="current">The route as it is.</param>
    public void Reset(RouteSnapshot current)
    {
        _undo.Clear();
        _redo.Clear();
        Current = current;
    }

    /// <summary>Sets the route the history holds, once the editor took a state back.</summary>
    /// <param name="current">The route as it is.</param>
    public void Adopt(RouteSnapshot current)
    {
        Current = current;
    }

    /// <summary>Notes the route as it is now.</summary>
    /// <param name="now">The route.</param>
    /// <param name="before">The route as it was, when it differs.</param>
    /// <returns><see langword="true"/> when the route differs from the last one seen, which is then the one to undo to.</returns>
    public bool Record(RouteSnapshot now, out RouteSnapshot before)
    {
        before = Current;

        if (now.Equals(Current))
            return false;

        // A gesture, a drag across many maps, is one change to take back, recorded at its first.
        if (!_inGesture || !_gestureRecorded)
        {
            _undo.Add(Current);

            if (_undo.Count > Depth)
                _undo.RemoveAt(0);

            _gestureRecorded = _inGesture;
        }

        _redo.Clear();
        Current = now;

        return true;
    }

    /// <summary>Takes the last change back.</summary>
    /// <param name="restore">Sets the editor to the state to go back to; the history forgets that state once it did.</param>
    public void Undo(Action<RouteSnapshot> restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        if (_undo.Count == 0)
            return;

        _redo.Add(Current);
        restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    /// <summary>Does the last change taken back again.</summary>
    /// <param name="restore">Sets the editor to the state to go to; the history forgets that state once it did.</param>
    public void Redo(Action<RouteSnapshot> restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        if (_redo.Count == 0)
            return;

        _undo.Add(Current);
        restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }
}
