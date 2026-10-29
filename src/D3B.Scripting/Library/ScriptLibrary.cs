using D3B.Bot.Activities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Scripting.Library;

/// <summary>The scripts of a folder, listed as activities while the host runs, and listed again whenever a file changes (D108).</summary>
/// <remarks>
/// <para>
/// Every <c>.lua</c> file of the folder and its folders is a script, but the API's definitions D3B writes there
/// (<see cref="ScriptDefinitions"/>). A script is the activity <c>script:</c> and its path from the folder, such as
/// <c>script:metiers/paysan.lua</c>, which a profile names as it names a module's (D99); the app shows its path without
/// the extension. Each is read (<see cref="ScriptReader"/>): its settings, or why it does not read.
/// </para>
/// <para>
/// The folder is watched: once its files have been quiet for <see cref="Quiet"/>, every script is read again, and the
/// list replaced whole, so that a start takes the text as last saved. An activity already running keeps the text it
/// started with. Nothing here stops the host: a folder that cannot be made or read is logged, and lists nothing.
/// </para>
/// </remarks>
public sealed class ScriptLibrary : IActivitySource, IHostedService, IDisposable
{
    /// <summary>The prefix of a script's activity id.</summary>
    public const string IdPrefix = "script:";

    /// <summary>How long the folder stays quiet before the scripts are read again: an editor saves a file in several writes.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly ILogger<ScriptLibrary> _log;
    private readonly SemaphoreSlim _reading = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private IReadOnlyList<ActivityDefinition> _activities = [];
    private IReadOnlyDictionary<string, ScriptException> _problems = new Dictionary<string, ScriptException>();
    private int _version;
    private FileSystemWatcher? _watcher;
    private Timer? _quiet;

    /// <summary>Gets the scripts' folder.</summary>
    public string Directory { get; }

    /// <inheritdoc/>
    public int Version => Volatile.Read(ref _version);

    /// <inheritdoc/>
    public IReadOnlyList<ActivityDefinition> Activities => Volatile.Read(ref _activities);

    /// <summary>Gets the scripts that do not read, by activity id, and why.</summary>
    public IReadOnlyDictionary<string, ScriptException> Problems => Volatile.Read(ref _problems);

    /// <summary>Initializes a new instance of the <see cref="ScriptLibrary"/> class.</summary>
    /// <param name="settings">Where the scripts are.</param>
    /// <param name="log">The library's log.</param>
    public ScriptLibrary(ScriptingSettings settings, ILogger<ScriptLibrary> log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);

        Directory = settings.Directory;
        _log = log;
    }

    /// <summary>The activity id of a script, from its path in the folder.</summary>
    /// <param name="relativePath">Its path from the folder.</param>
    /// <returns><c>script:</c> and the path, its separators <c>/</c>.</returns>
    public static string IdOf(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return IdPrefix + relativePath.Replace('\\', '/');
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            ScriptDefinitions.Write(Directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ScriptLog.FolderFailed(_log, Directory, exception);

            return;
        }

        await ReadAsync(cancellationToken).ConfigureAwait(false);

        _quiet = new Timer(_ => _ = ReadAgainAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watcher = new FileSystemWatcher(Directory, "*.lua")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        _watcher?.Dispose();
        _quiet?.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>Reads every script again, now.</summary>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>A task that completes once the list is replaced.</returns>
    public async Task ReadAsync(CancellationToken cancellationToken)
    {
        await _reading.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var activities = new List<ActivityDefinition>();
            var problems = new Dictionary<string, ScriptException>(StringComparer.Ordinal);

            foreach (var path in Files())
            {
                var relative = Path.GetRelativePath(Directory, path);
                var id = IdOf(relative);
                var name = Path.ChangeExtension(relative, null).Replace('\\', '/');

                if (await ReadTextAsync(path, cancellationToken).ConfigureAwait(false) is not { } source)
                    continue;

                var (activity, error) = await ScriptReader.ReadAsync(id, new LuaScript(name, source), cancellationToken).ConfigureAwait(false);

                activities.Add(activity);

                if (error is not null)
                {
                    problems[id] = error;
                    ScriptLog.ScriptBroken(_log, name, error.Message);
                }
            }

            Volatile.Write(ref _activities, activities);
            Volatile.Write(ref _problems, problems);
            Interlocked.Increment(ref _version);
            ScriptLog.Listed(_log, activities.Count, problems.Count, Directory);
        }
        finally
        {
            _reading.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher?.Dispose();
        _quiet?.Dispose();
        _reading.Dispose();
        _stopping.Dispose();
    }

    private static async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        // An editor may still hold the file it saves: it is tried again a few times, then left for the next change.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private IEnumerable<string> Files()
    {
        var definitions = Path.Combine(Directory, ScriptDefinitions.FileName);

        return System.IO.Directory.EnumerateFiles(Directory, "*.lua", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, definitions, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (string.Equals(e.Name, ScriptDefinitions.FileName, StringComparison.OrdinalIgnoreCase))
            return;

        // Every change moves the reading back, until the folder is quiet.
        _quiet?.Change(Quiet, Timeout.InfiniteTimeSpan);
    }

    private async Task ReadAgainAsync()
    {
        try
        {
            await ReadAsync(_stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The host stops.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ScriptLog.FolderFailed(_log, Directory, exception);
        }
    }
}
