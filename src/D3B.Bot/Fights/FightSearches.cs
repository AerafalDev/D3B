using System.Collections.Concurrent;

namespace D3B.Bot.Fights;

/// <summary>The threads the fight's AI searches on, apart from the thread pool, shared by every bot of the host (D508).</summary>
/// <remarks>
/// <para>
/// A turn's search is CPU-bound and lasts up to its budget, about a second, and the bot searches again after each of the
/// server's answers. Run on the thread pool, a few bots searching at once held every one of its threads on a machine of few
/// cores, and the relay's reads and writes, which run there, waited behind them; Kestrel said its heartbeat ran late.
/// </para>
/// <para>
/// Here, a bounded number of dedicated threads take the searches in the order asked, below the normal priority, so that the
/// relay, the studio and the game clients keep their share of the cores while the AI thinks. What awaits a search goes on
/// on the thread pool, never on the search's thread.
/// </para>
/// </remarks>
public sealed class FightSearches : IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread[] _threads;
    private volatile bool _disposed;

    /// <summary>Gets how many threads the host's searches share: half the cores, one at least.</summary>
    public static int DefaultCount => Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Initializes a new instance of the <see cref="FightSearches"/> class, its threads started.</summary>
    /// <param name="count">How many searches run at once, each on a thread of its own.</param>
    public FightSearches(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        _threads = new Thread[count];

        for (var index = 0; index < count; index++)
        {
            _threads[index] = new Thread(Serve) { IsBackground = true, Name = $"D3B fight search {index + 1}", Priority = ThreadPriority.BelowNormal };
            _threads[index].Start();
        }
    }

    /// <summary>Runs a search on one of the threads, once one is free.</summary>
    /// <typeparam name="T">What it finds.</typeparam>
    /// <param name="search">The search, CPU-bound and synchronous.</param>
    /// <param name="cancellationToken">Cancels the wait: a search not begun never runs, and one running is let finish, its result dropped.</param>
    /// <returns>What it found.</returns>
    public Task<T> RunAsync<T>(Func<T> search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var found = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = cancellationToken.Register(() => found.TrySetCanceled(cancellationToken));

        try
        {
            _queue.Add(() =>
            {
                using (cancelled)
                {
                    if (found.Task.IsCompleted)
                        return;

                    try
                    {
                        found.TrySetResult(search());
                    }
                    catch (Exception exception)
                    {
                        found.TrySetException(exception);
                    }
                }
            }, CancellationToken.None);
        }
        catch (InvalidOperationException) when (_disposed)
        {
            cancelled.Dispose();

            throw new ObjectDisposedException(nameof(FightSearches));
        }

        return found.Task;
    }

    /// <summary>Stops the threads once the searches asked are done.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queue.CompleteAdding();

        foreach (var thread in _threads)
            thread.Join();

        _queue.Dispose();
    }

    private void Serve()
    {
        foreach (var search in _queue.GetConsumingEnumerable())
            search();
    }
}
