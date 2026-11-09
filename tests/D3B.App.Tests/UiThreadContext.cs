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

    public override void Send(SendOrPostCallback d, object? state)
    {
        using var done = new ManualResetEventSlim();
        Exception? failure = null;

        Post(
            _ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    done.Set();
                }
            },
            null);
        done.Wait();

        if (failure is not null)
            throw new InvalidOperationException("A callback sent to the test UI thread failed.", failure);
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
}
