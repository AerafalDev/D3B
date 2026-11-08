using System.Collections.Immutable;
using System.Globalization;

namespace D3B.Studio.Assets;

/// <summary>The project: the assets of its folder, one JSON file each, and those given with the app, by their stable ids (D296).</summary>
/// <remarks>
/// <para>
/// The app reads what is in memory, every refresh, without the disk; each write goes to the disk first, then into memory,
/// and counts a new version, as D3B's books do (D100). Writes go one at a time.
/// </para>
/// <para>
/// A file is an asset when its extension names a kind (<see cref="AssetFiles.ExtensionOf"/>), in the folder or any of its
/// own, which the user arranges as they like. One that does not read, or whose id another file or a given asset already
/// has, is left out and said (<see cref="Problems"/>), so that an asset copied by hand never takes another's place.
/// </para>
/// <para>
/// The given assets are read only: they are copied to be changed (D296). The folder is made only once something is
/// written in it, so that a host that writes nothing leaves no folder behind.
/// </para>
/// </remarks>
public sealed class AssetLibrary : IDisposable
{
    /// <summary>The greatest length of an asset's name.</summary>
    public const int NameLength = 60;

    // A burst of changes, a folder pasted in the file manager, is read once it is over.
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(400);

    private readonly SemaphoreSlim _writing = new(1, 1);
    private readonly bool _recycles;
    private readonly bool _watches;
    private readonly Lock _watching = new();
    private FileSystemWatcher? _watcher;
    private Timer? _reading;
    private volatile Snapshot _snapshot = Snapshot.Empty;
    private IReadOnlyList<Asset> _given = [];

    /// <summary>Gets the project's folder.</summary>
    public string Folder { get; }

    /// <summary>Gets the assets: those given with the app, then the project's, each kind by name.</summary>
    public IReadOnlyList<Asset> Assets => _snapshot.Assets;

    /// <summary>Gets the files of the project that were not read, and why.</summary>
    public IReadOnlyList<AssetProblem> Problems => _snapshot.Problems;

    /// <summary>Gets the project's folders, relative to its own, empty ones included, by name (D326).</summary>
    public IReadOnlyList<string> Folders => _snapshot.Folders;

    /// <summary>Gets a number that changes each time the assets change, for a view to tell it must read them again.</summary>
    public int Version => _snapshot.Version;

    /// <summary>Initializes a new instance of the <see cref="AssetLibrary"/> class.</summary>
    /// <param name="settings">Where the project's folder is.</param>
    public AssetLibrary(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Folder = Path.GetFullPath(settings.Path);
        _recycles = settings.Recycles;
        _watches = settings.Watches;
    }

    /// <summary>Gets an id for a new asset, never another's.</summary>
    /// <returns>The id.</returns>
    public static string NewId()
    {
        return Guid.CreateVersion7().ToString();
    }

    /// <summary>Tells what is wrong with a name for an asset, if anything: blank, too long, or what no file is named.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Why the name does not do, in English; <see langword="null"/> when it does.</returns>
    public static string? NameProblem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        if (trimmed.Length == 0)
            return "An asset's name is not blank.";

        if (trimmed.Length > NameLength)
            return $"An asset's name is {NameLength} characters at most.";

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed.EndsWith('.'))
            return "An asset's name is what a file may be named: no \\ / : * ? \" < > |, nor a dot at its end.";

        return null;
    }

    private static bool IsIn(string path, string folder)
    {
        return path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Finds an asset by its id.</summary>
    /// <param name="id">The asset's id.</param>
    /// <returns>The asset, or <see langword="null"/>.</returns>
    public Asset? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _snapshot.ById.GetValueOrDefault(id);
    }

    /// <summary>Finds an asset of a kind by its id.</summary>
    /// <typeparam name="T">The asset's record, such as <see cref="ProfileAsset"/>.</typeparam>
    /// <param name="id">The asset's id.</param>
    /// <returns>The asset, or <see langword="null"/> when none of the record has the id.</returns>
    public T? Find<T>(string id)
        where T : Asset
    {
        return Find(id) as T;
    }

    /// <summary>Gets the assets of a record, in <see cref="Assets"/>'s order.</summary>
    /// <typeparam name="T">The asset's record, such as <see cref="ProfileAsset"/>.</typeparam>
    /// <returns>The assets.</returns>
    public IReadOnlyList<T> OfType<T>()
        where T : Asset
    {
        return [.. Assets.OfType<T>()];
    }

    /// <summary>Gets the assets that use one: "used by" (D296).</summary>
    /// <param name="id">The asset's id.</param>
    /// <returns>The assets that reference it, in <see cref="Assets"/>'s order.</returns>
    public IReadOnlyList<Asset> UsedBy(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _snapshot.UsedBy.GetValueOrDefault(id, []);
    }

    /// <summary>Tells whether a name is another asset's of the same kind, whatever its case.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="name">The name.</param>
    /// <param name="exceptId">The asset the name is for, which may keep its own.</param>
    /// <returns>Whether another asset of the kind has it.</returns>
    public bool IsTaken(AssetKind kind, string name, string? exceptId = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        return Assets.Any(asset => asset.Kind == kind && asset.Id != exceptId && string.Equals(asset.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the project's folder again, with the assets given with the app.</summary>
    /// <param name="given">The assets given with the app, read only, whose ids no file may take.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once read.</returns>
    public async Task LoadAsync(IReadOnlyList<Asset> given, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(given);

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _given = given;

            var read = new List<Asset>();
            var problems = new List<AssetProblem>();
            var ids = given.Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);

            if (Directory.Exists(Folder))
            {
                foreach (var file in Directory.EnumerateFiles(Folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (!AssetFiles.TryKindOf(Path.GetFileName(file), out var kind, out var name))
                        continue;

                    var path = Path.GetRelativePath(Folder, file);

                    try
                    {
                        var asset = AssetFiles.Read(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), name, path, kind);

                        if (!ids.Add(asset.Id))
                            problems.Add(new AssetProblem(path, $"Its id {asset.Id} is another asset's: copied by hand, it takes a new id once saved from the app.") { Kind = AssetProblemKind.IdTaken });
                        else
                            read.Add(asset);
                    }
                    catch (Exception exception) when (exception is AssetFormatException or IOException or UnauthorizedAccessException)
                    {
                        problems.Add(new AssetProblem(path, exception.Message) { Kind = exception is AssetFormatException format ? format.Kind : AssetProblemKind.Unreadable });
                    }
                }
            }

            _snapshot = Snapshot.Of([.. given, .. read], problems, FoldersOn(), _snapshot.Version + 1);
            Watch();
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Saves an asset of the project: a new one in a file of its name in a folder, the root by default, else in its file, renamed with it.</summary>
    /// <typeparam name="T">The asset's record.</typeparam>
    /// <param name="asset">The asset; a new one has an id no other has, such as <see cref="NewId"/>'s.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <param name="folder">The folder a new asset is saved in, relative to the project's; one already saved stays in its own.</param>
    /// <returns>The asset saved, with its file.</returns>
    /// <exception cref="ArgumentException">The name does not do, or another asset of the kind has it.</exception>
    /// <exception cref="InvalidOperationException">The asset was given with the app, or its id is another kind's.</exception>
    /// <exception cref="IOException">The file was not written.</exception>
    public async Task<T> SaveAsync<T>(T asset, CancellationToken cancellationToken, string? folder = null)
        where T : Asset
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.IsGiven || _given.Any(given => given.Id == asset.Id))
            throw new InvalidOperationException($"The asset {asset.Id} was given with the app: it is copied to be changed.");

        if (NameProblem(asset.Name) is { } problem)
            throw new ArgumentException(problem, nameof(asset));

        var name = asset.Name.Trim();

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var previous = _snapshot.ById.GetValueOrDefault(asset.Id);

            if (previous is not null && previous.Kind != asset.Kind)
                throw new InvalidOperationException($"The id {asset.Id} is a {previous.Kind}'s.");

            if (_snapshot.Assets.Any(other => other.Kind == asset.Kind && other.Id != asset.Id && string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"A {asset.Kind} is already named {name}.", nameof(asset));

            var directory = previous?.Path is { } kept ? Path.GetDirectoryName(kept) ?? "" : Inside(folder);
            var path = Path.Combine(directory, name + AssetFiles.ExtensionOf(asset.Kind));
            var saved = asset with { Name = name, Path = path };
            var file = Path.Combine(Folder, path);

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            // Written beside it, then moved over it, so that a crash never leaves half a file.
            var temporary = file + ".tmp";

            await File.WriteAllTextAsync(temporary, AssetFiles.Write(saved), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, file, overwrite: true);

            if (previous?.Path is { } old && !string.Equals(old, path, StringComparison.OrdinalIgnoreCase))
                File.Delete(Path.Combine(Folder, old));

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Where(other => other.Id != asset.Id), saved], _snapshot.Problems, FoldersOn(), _snapshot.Version + 1);
            Watch();

            return saved;
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Deletes an asset of the project, its file with it; what referenced it is left referencing nothing.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once deleted.</returns>
    /// <exception cref="InvalidOperationException">The asset was given with the app.</exception>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_snapshot.ById.GetValueOrDefault(id) is not { } asset)
                return;

            if (asset.IsGiven)
                throw new InvalidOperationException($"The asset {id} was given with the app.");

            if (asset.Path is { } path)
                Discard(Path.Combine(Folder, path), directory: false);

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Where(other => other.Id != id)], _snapshot.Problems, _snapshot.Folders, _snapshot.Version + 1);
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Makes a folder in the project, and those it is in.</summary>
    /// <param name="folder">The folder, relative to the project's.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once made.</returns>
    /// <exception cref="ArgumentException">A part of the folder's name is no name a file may have, or it leaves the project.</exception>
    public async Task CreateFolderAsync(string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var inside = Inside(folder);

        if (inside.Length == 0 || inside.Split(Path.DirectorySeparatorChar).Any(part => NameProblem(part) is not null))
            throw new ArgumentException($"{folder} is no folder of the project.", nameof(folder));

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(Path.Combine(Folder, inside));
            _snapshot = Snapshot.Of(_snapshot.Assets, _snapshot.Problems, FoldersOn(), _snapshot.Version + 1);
            Watch();
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Renames a folder of the project, the assets in it keeping their ids and names.</summary>
    /// <param name="folder">The folder, relative to the project's.</param>
    /// <param name="name">Its new name, a name a file may have.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The folder renamed, relative to the project's.</returns>
    /// <exception cref="ArgumentException">The name does not do, or the folder is the project's own.</exception>
    /// <exception cref="InvalidOperationException">Another file or folder of the name is there already.</exception>
    public async Task<string> RenameFolderAsync(string folder, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(name);

        var inside = Inside(folder);
        var trimmed = name.Trim();

        if (inside.Length == 0)
            throw new ArgumentException("The project's own folder is not renamed.", nameof(folder));

        if (NameProblem(trimmed) is { } problem)
            throw new ArgumentException(problem, nameof(name));

        return await PlaceFolderAsync(inside, Path.Combine(Path.GetDirectoryName(inside) ?? "", trimmed), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Moves a folder of the project into another, or to the project's root, the assets in it keeping their ids and names.</summary>
    /// <param name="folder">The folder, relative to the project's.</param>
    /// <param name="parent">The folder it goes into, relative to the project's; empty for the root.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The folder moved, relative to the project's.</returns>
    /// <exception cref="ArgumentException">The folder is the project's own, or the parent is the folder or one of its own.</exception>
    /// <exception cref="InvalidOperationException">Another file or folder of its name is in the parent already.</exception>
    public async Task<string> MoveFolderAsync(string folder, string parent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(parent);

        var inside = Inside(folder);
        var into = Inside(parent);

        if (inside.Length == 0)
            throw new ArgumentException("The project's own folder is not moved.", nameof(folder));

        if (string.Equals(into, inside, StringComparison.OrdinalIgnoreCase) || IsIn(into, inside))
            throw new ArgumentException($"{folder} is not moved into itself.", nameof(parent));

        return await PlaceFolderAsync(inside, Path.Combine(into, Path.GetFileName(inside)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a folder of the project, its own folders and every file in them; what referenced its assets is left referencing nothing.</summary>
    /// <param name="folder">The folder, relative to the project's.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The assets deleted with it.</returns>
    /// <exception cref="ArgumentException">The folder is the project's own.</exception>
    public async Task<IReadOnlyList<Asset>> DeleteFolderAsync(string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var inside = Inside(folder);

        if (inside.Length == 0)
            throw new ArgumentException("The project's own folder is not deleted.", nameof(folder));

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var deleted = _snapshot.Assets.Where(asset => asset.Path is { } path && IsIn(path, inside)).ToList();
            var directory = Path.Combine(Folder, inside);

            if (Directory.Exists(directory))
                Discard(directory, directory: true);

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Except(deleted)], [.. _snapshot.Problems.Where(item => !IsIn(item.Path, inside))], FoldersOn(), _snapshot.Version + 1);

            return deleted;
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Gets the assets of the project in a folder or one of its own.</summary>
    /// <param name="folder">The folder, relative to the project's.</param>
    /// <returns>The assets, in <see cref="Assets"/>'s order.</returns>
    public IReadOnlyList<Asset> AssetsIn(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var inside = Inside(folder);

        return [.. Assets.Where(asset => asset.Path is { } path && IsIn(path, inside))];
    }

    /// <summary>Moves an asset of the project to another folder, its file with it, its id and name kept.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The asset moved.</returns>
    /// <exception cref="InvalidOperationException">The asset is no asset of the project, or a file of its name is in the folder already.</exception>
    public async Task<Asset> MoveAsync(string id, string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(folder);

        var inside = Inside(folder);

        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_snapshot.ById.GetValueOrDefault(id) is not { Path: { } path } asset)
                throw new InvalidOperationException($"The asset {id} is no asset of the project.");

            var moved = Path.Combine(inside, Path.GetFileName(path));

            if (string.Equals(moved, path, StringComparison.OrdinalIgnoreCase))
                return asset;

            var target = Path.Combine(Folder, moved);

            if (File.Exists(target))
                throw new InvalidOperationException($"{moved} is another file's.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(Path.Combine(Folder, path), target);

            var saved = asset with { Path = moved };

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Where(other => other.Id != id), saved], _snapshot.Problems, FoldersOn(), _snapshot.Version + 1);

            return saved;
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Imports an asset's file into a folder of the project: a copy, under a new id when its own is another asset's already.</summary>
    /// <param name="file">The file, its name saying its kind, such as <c>Mine.profile.json</c>.</param>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    /// <param name="cancellationToken">Stops the read and the write.</param>
    /// <returns>The asset imported.</returns>
    /// <exception cref="AssetFormatException">The file is no asset's, or does not read.</exception>
    /// <exception cref="ArgumentException">Another asset of the kind has its name.</exception>
    public async Task<Asset> ImportAsync(string file, string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!AssetFiles.TryKindOf(Path.GetFileName(file), out var kind, out var name))
            throw new AssetFormatException($"{Path.GetFileName(file)} is no asset's file: its extension names no kind.") { Kind = AssetProblemKind.NotAsset };

        var asset = AssetFiles.Read(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), name, Path.GetFileName(file), kind) with { Path = null };

        if (Find(asset.Id) is not null)
            asset = asset with { Id = NewId() };

        return await SaveAsync(asset, cancellationToken, folder).ConfigureAwait(false);
    }

    /// <summary>Writes an asset's file elsewhere, as the project keeps it, a given asset included.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="file">Where to write it.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once written.</returns>
    /// <exception cref="InvalidOperationException">No asset has the id.</exception>
    public async Task ExportAsync(string id, string file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var asset = Find(id) ?? throw new InvalidOperationException($"No asset has the id {id}.");

        await File.WriteAllTextAsync(file, AssetFiles.Write(asset), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the path of an asset's file on the disk, for the system's file manager.</summary>
    /// <param name="asset">The asset.</param>
    /// <returns>The file's full path, or none for a given asset.</returns>
    public string? FileOf(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return asset.Path is { } path ? Path.Combine(Folder, path) : null;
    }

    /// <summary>Reads the project's folder again, with the assets given with the app it was read with.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once read.</returns>
    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(_given, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_watching)
        {
            _watcher?.Dispose();
            _watcher = null;
            _reading?.Dispose();
            _reading = null;
        }

        _writing.Dispose();
    }

    private async Task<string> PlaceFolderAsync(string inside, string placed, CancellationToken cancellationToken)
    {
        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (string.Equals(placed, inside, StringComparison.Ordinal))
                return inside;

            var target = Path.Combine(Folder, placed);

            // A change of case alone is a move Windows makes, the folder being the same.
            if (!string.Equals(placed, inside, StringComparison.OrdinalIgnoreCase) && (Directory.Exists(target) || File.Exists(target)))
                throw new InvalidOperationException($"{placed} is there already.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(Path.Combine(Folder, inside), target);

            var assets = _snapshot.Assets.Select(asset => asset.Path is { } path && IsIn(path, inside) ? asset with { Path = placed + path[inside.Length..] } : asset).ToList();
            var problems = _snapshot.Problems.Select(item => IsIn(item.Path, inside) ? item with { Path = placed + item.Path[inside.Length..] } : item).ToList();

            _snapshot = Snapshot.Of(assets, problems, FoldersOn(), _snapshot.Version + 1);

            return placed;
        }
        finally
        {
            _writing.Release();
        }
    }

    private void Discard(string path, bool directory)
    {
        if (_recycles && OperatingSystem.IsWindows())
        {
            if (directory)
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        else if (directory)
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    // The folder is watched once it exists, which the first write makes; a change, D3B's own included, reads it again.
    private void Watch()
    {
        if (!_watches || !Directory.Exists(Folder))
            return;

        lock (_watching)
        {
            if (_watcher is not null)
                return;

            _reading = new Timer(_ => _ = ReadAgainAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(Folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
            _watcher.Created += OnFolderChanged;
            _watcher.Deleted += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.Changed += OnFolderChanged;
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        // A file half written beside its asset's, then moved over it, is no change of its own.
        if (e.Name?.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) == true)
            return;

        lock (_watching)
            _reading?.Change(_settle, Timeout.InfiniteTimeSpan);
    }

    private async Task ReadAgainAsync()
    {
        try
        {
            await ReloadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The host stopped as the folder changed.
        }
    }

    // A folder said with either separator, relative to the project's, which never leaves it.
    private string Inside(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return "";

        var full = Path.GetFullPath(Path.Combine(Folder, folder.Trim().Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(Folder, full);

        if (relative == ".")
            return "";

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ArgumentException($"{folder} is out of the project.", nameof(folder));

        return relative;
    }

    private IReadOnlyList<string> FoldersOn()
    {
        if (!Directory.Exists(Folder))
            return [];

        return [.. Directory.EnumerateDirectories(Folder, "*", SearchOption.AllDirectories)
            .Select(directory => Path.GetRelativePath(Folder, directory))
            .Order(StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))];
    }

    private sealed record Snapshot(
        IReadOnlyList<Asset> Assets,
        IReadOnlyList<AssetProblem> Problems,
        IReadOnlyList<string> Folders,
        ImmutableDictionary<string, Asset> ById,
        ImmutableDictionary<string, IReadOnlyList<Asset>> UsedBy,
        int Version)
    {
        public static Snapshot Empty { get; } = new([], [], [], ImmutableDictionary<string, Asset>.Empty, ImmutableDictionary<string, IReadOnlyList<Asset>>.Empty, 0);

        public static Snapshot Of(IReadOnlyList<Asset> assets, IReadOnlyList<AssetProblem> problems, IReadOnlyList<string> folders, int version)
        {
            var ordered = assets
                .OrderBy(asset => asset.IsGiven ? 0 : 1)
                .ThenBy(asset => asset.Kind)
                .ThenBy(asset => asset.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
                .ToList();
            var usedBy = ordered
                .SelectMany(asset => asset.Uses().Distinct().Select(used => (Used: used, By: asset)))
                .GroupBy(use => use.Used, StringComparer.Ordinal)
                .ToImmutableDictionary(group => group.Key, group => (IReadOnlyList<Asset>)[.. group.Select(use => use.By)], StringComparer.Ordinal);

            return new Snapshot(ordered, problems, folders, ordered.ToImmutableDictionary(asset => asset.Id, StringComparer.Ordinal), usedBy, version);
        }
    }
}
