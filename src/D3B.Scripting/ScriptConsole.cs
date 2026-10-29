namespace D3B.Scripting;

/// <summary>What a script wrote, its last lines kept for the app: the bots sold show each bot's console, and scripts talk to it.</summary>
/// <remarks>Written from the script's step, read from the app's thread: every access takes the lock, and reading copies.</remarks>
public sealed class ScriptConsole
{
    /// <summary>The number of lines kept: older ones are dropped.</summary>
    public const int Capacity = 200;

    private readonly Lock _lock = new();
    private readonly Queue<ScriptLine> _lines = new();
    private long _written;

    /// <summary>Gets the number of lines written since the script started, kept or dropped, so that a reader tells new ones.</summary>
    public long Written
    {
        get
        {
            lock (_lock)
                return _written;
        }
    }

    /// <summary>Gets the lines kept, oldest first.</summary>
    /// <returns>A copy.</returns>
    public IReadOnlyList<ScriptLine> Lines()
    {
        lock (_lock)
            return [.. _lines];
    }

    internal void Write(ScriptLine line)
    {
        lock (_lock)
        {
            if (_lines.Count == Capacity)
                _lines.Dequeue();

            _lines.Enqueue(line);
            _written++;
        }
    }
}
