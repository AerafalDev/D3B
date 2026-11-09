namespace D3B.App.ViewModels.Studio;

/// <summary>What an editor can take back and do again: states it was in, the oldest forgotten past a depth.</summary>
/// <typeparam name="T">The editor's state, compared by value.</typeparam>
internal sealed class UndoHistory<T>
{
    private readonly List<T> _undo = [];
    private readonly List<T> _redo = [];
    private readonly Func<T, T, bool> _same;
    private readonly int _depth;
    private string? _run;

    /// <summary>Gets the state the editor is in now.</summary>
    public T Current { get; private set; }

    /// <summary>Gets whether a state is kept for Undo to go back to.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets whether a state was undone that Redo goes forward to.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Initializes a new instance of the <see cref="UndoHistory{T}"/> class.</summary>
    /// <param name="initial">The state the editor starts in.</param>
    /// <param name="same">Tells whether two states are the same.</param>
    /// <param name="depth">How many states are kept.</param>
    public UndoHistory(T initial, Func<T, T, bool> same, int depth = 200)
    {
        ArgumentNullException.ThrowIfNull(same);

        Current = initial;
        _same = same;
        _depth = depth;
    }

    /// <summary>Notes that the editor is in another state.</summary>
    /// <param name="now">The state.</param>
    /// <param name="run">
    /// What the change belongs to, such as the field typed in: changes that follow each other with the same run are one to take
    /// back, so that a word typed is not a step per letter. <see langword="null"/> for a change of its own.
    /// </param>
    /// <returns>Whether the state differs from the last.</returns>
    public bool Record(T now, string? run = null)
    {
        if (_same(now, Current))
            return false;

        if (run is null || run != _run)
        {
            _undo.Add(Current);

            if (_undo.Count > _depth)
                _undo.RemoveAt(0);
        }

        _run = run;
        _redo.Clear();
        Current = now;

        return true;
    }

    /// <summary>Goes back one state.</summary>
    /// <param name="state">The state gone back to.</param>
    /// <returns>Whether there was one.</returns>
    public bool TryUndo(out T state)
    {
        state = Current;

        if (_undo.Count == 0)
            return false;

        _redo.Add(Current);
        Current = state = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _run = null;

        return true;
    }

    /// <summary>Goes forward one state.</summary>
    /// <param name="state">The state gone forward to.</param>
    /// <returns>Whether there was one.</returns>
    public bool TryRedo(out T state)
    {
        state = Current;

        if (_redo.Count == 0)
            return false;

        _undo.Add(Current);
        Current = state = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _run = null;

        return true;
    }

    /// <summary>Forgets what was done and starts again from a state, such as one the file changed to under the editor.</summary>
    /// <param name="state">The state.</param>
    public void Reset(T state)
    {
        _undo.Clear();
        _redo.Clear();
        _run = null;
        Current = state;
    }
}
