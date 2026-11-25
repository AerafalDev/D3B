using D3B.Bot.Characters;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Journal;

/// <summary>Writes each bot's journal and the host's problems to <see cref="JournalFiles"/>, every second and as the host stops (D504).</summary>
/// <remarks>
/// <para>
/// The journals in memory are what the bots write (D113): the recorder reads what came since its last pass, as the app's logs page
/// does, so that no bot's loop ever waits for a disk. A second is far less than 2,000 entries take to come, so none is missed,
/// and a crash loses at most that second.
/// </para>
/// <para>
/// It sweeps the old files as it starts and at each new UTC day (<see cref="JournalRetention"/>). A write that fails is logged
/// once, until one succeeds, and its lines are tried again at the next pass. <see cref="Flush"/> may be called from any thread,
/// as a report does before it reads the files.
/// </para>
/// </remarks>
public sealed class JournalRecorder : BackgroundService
{
    private readonly Lock _lock = new();
    private readonly JournalRetention _retention;
    private readonly Bots _bots;
    private readonly HostProblems _problems;
    private readonly TimeProvider _time;
    private readonly ILogger<JournalRecorder> _log;
    private readonly Dictionary<CharacterKey, long> _readOfBots = [];
    private long _readOfHost;
    private DateTime? _sweptOn;
    private bool _failing;

    /// <summary>Gets how often the recorder writes what came.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the files it writes.</summary>
    public JournalFiles Files { get; }

    /// <summary>Initializes a new instance of the <see cref="JournalRecorder"/> class.</summary>
    /// <param name="files">The files it writes.</param>
    /// <param name="retention">How long and how much of them is kept.</param>
    /// <param name="bots">The bots whose journals it writes.</param>
    /// <param name="problems">The host's problems, which it writes too.</param>
    /// <param name="time">The clock of its passes and of the files' age.</param>
    /// <param name="log">Its log.</param>
    public JournalRecorder(JournalFiles files, JournalRetention retention, Bots bots, HostProblems problems, TimeProvider time, ILogger<JournalRecorder> log)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(bots);
        ArgumentNullException.ThrowIfNull(problems);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(log);

        Files = files;
        _retention = retention;
        _bots = bots;
        _problems = problems;
        _time = time;
        _log = log;
    }

    /// <summary>Writes what the bots and the host said since the last pass, and sweeps the old files at a new day.</summary>
    public void Flush()
    {
        lock (_lock)
        {
            try
            {
                Sweep();

                var readOfHost = _readOfHost;

                Files.Append(JournalFiles.HostSource, [.. _problems.Problems.After(ref readOfHost).Select(JournalText.Of)]);
                _readOfHost = readOfHost;

                foreach (var bot in _bots.All)
                {
                    var read = _readOfBots.GetValueOrDefault(bot.Key);

                    Files.Append(JournalFiles.SourceOf(bot.Key), [.. bot.Journal.After(ref read).Select(JournalText.Of)]);
                    _readOfBots[bot.Key] = read;
                }

                _failing = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (!_failing)
                    JournalLog.WriteFailed(_log, Files.Directory, exception);

                _failing = true;
            }
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // The services registered before the recorder stop after it, the relay among them, whose last closings the bots
        // journal: the host's container disposes of the recorder only once every service stopped, before the bots it reads.
        Flush();
        base.Dispose();
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        // What came in the last second, and what the host said as it stopped.
        Flush();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        JournalLog.Writing(_log, Files.Directory);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Flush();
                await Task.Delay(Interval, _time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stops: StopAsync writes the rest.
        }
    }

    private void Sweep()
    {
        var today = _time.GetUtcNow().UtcDateTime.Date;

        if (_sweptOn == today)
            return;

        _sweptOn = today;

        var deleted = _retention.Sweep(Files.Directory, _time);

        if (deleted > 0)
            JournalLog.Swept(_log, deleted, Files.Directory);
    }
}
