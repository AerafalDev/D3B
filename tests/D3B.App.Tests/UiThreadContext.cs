using System.Collections.Concurrent;

namespace D3B.App.Tests;

/// <summary>
/// A thread of its own that runs, one after the other, whatever is posted to it: the studio's UI thread, for a view model that
/// goes back to it after an await. A test that has no such thread resumes a view model's awaits on the thread pool, beside its
/// own steps, which the studio never does.
/// </summary>
internal sealed class UiThreadContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

    public UiThreadContext()
    {
        new Thread(Pump) { IsBackground = true, Name = "Test UI thread" }.Start();
    }

    /// <summary>Moves what the caller does after an await of it onto the UI thread, where it stays through every await that follows.</summary>
    /// <returns>An awaiter that resumes on the UI thread.</returns>
    public Entry EnterAsync()
    {
        return new Entry(this);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        try
        {
            _queue.Add((d, state));
        }
        catch (InvalidOperationException)
        {
            // The test is over and its thread gone: what the runner still resumes here goes on the pool.
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
    }

    private void Pump()
    {
        SetSynchronizationContext(this);

        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            callback(state);
    }

    // A task would not do: its continuation may not run inline on a thread that has a context, so it would go to the pool.
    internal readonly struct Entry : System.Runtime.CompilerServices.INotifyCompletion
    {
        private readonly UiThreadContext _context;

        public Entry(UiThreadContext context)
        {
            _context = context;
        }

        public bool IsCompleted
        {
            get
            {
                return Current == _context;
            }
        }

        public Entry GetAwaiter()
        {
            return this;
        }

        public void OnCompleted(Action continuation)
        {
            _context.Post(_ => continuation(), null);
        }

        public void GetResult()
        {
        }
    }
}
