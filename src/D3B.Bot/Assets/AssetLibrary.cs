using System.Collections.Immutable;
using System.Globalization;

namespace D3B.Bot.Assets;

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

    private readonly SemaphoreSlim _writing = new(1, 1);
    private volatile Snapshot _snapshot = Snapshot.Empty;
    private IReadOnlyList<Asset> _given = [];

    /// <summary>Gets the project's folder.</summary>
    public string Folder { get; }

    /// <summary>Gets the assets: those given with the app, then the project's, each kind by name.</summary>
    public IReadOnlyList<Asset> Assets => _snapshot.Assets;

    /// <summary>Gets the files of the project that were not read, and why.</summary>
    public IReadOnlyList<AssetProblem> Problems => _snapshot.Problems;

    /// <summary>Gets a number that changes each time the assets change, for a view to tell it must read them again.</summary>
    public int Version => _snapshot.Version;

    /// <summary>Initializes a new instance of the <see cref="AssetLibrary"/> class.</summary>
    /// <param name="settings">Where the project's folder is.</param>
    public AssetLibrary(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Folder = Path.GetFullPath(settings.Path);
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
                            problems.Add(new AssetProblem(path, $"Its id {asset.Id} is another asset's: copied by hand, it takes a new id once saved from the app."));
                        else
                            read.Add(asset);
                    }
                    catch (Exception exception) when (exception is AssetFormatException or IOException or UnauthorizedAccessException)
                    {
                        problems.Add(new AssetProblem(path, exception.Message));
                    }
                }
            }

            _snapshot = Snapshot.Of([.. given, .. read], problems, _snapshot.Version + 1);
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <summary>Saves an asset of the project: a new one in a file of its name at the folder's root, else in its file, renamed with it.</summary>
    /// <typeparam name="T">The asset's record.</typeparam>
    /// <param name="asset">The asset; a new one has an id no other has, such as <see cref="NewId"/>'s.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The asset saved, with its file.</returns>
    /// <exception cref="ArgumentException">The name does not do, or another asset of the kind has it.</exception>
    /// <exception cref="InvalidOperationException">The asset was given with the app, or its id is another kind's.</exception>
    /// <exception cref="IOException">The file was not written.</exception>
    public async Task<T> SaveAsync<T>(T asset, CancellationToken cancellationToken)
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

            var directory = previous?.Path is { } kept ? Path.GetDirectoryName(kept) ?? "" : "";
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

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Where(other => other.Id != asset.Id), saved], _snapshot.Problems, _snapshot.Version + 1);

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
                File.Delete(Path.Combine(Folder, path));

            _snapshot = Snapshot.Of([.. _snapshot.Assets.Where(other => other.Id != id)], _snapshot.Problems, _snapshot.Version + 1);
        }
        finally
        {
            _writing.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _writing.Dispose();
    }

    private sealed record Snapshot(
        IReadOnlyList<Asset> Assets,
        IReadOnlyList<AssetProblem> Problems,
        ImmutableDictionary<string, Asset> ById,
        ImmutableDictionary<string, IReadOnlyList<Asset>> UsedBy,
        int Version)
    {
        public static Snapshot Empty { get; } = new([], [], ImmutableDictionary<string, Asset>.Empty, ImmutableDictionary<string, IReadOnlyList<Asset>>.Empty, 0);

        public static Snapshot Of(IReadOnlyList<Asset> assets, IReadOnlyList<AssetProblem> problems, int version)
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

            return new Snapshot(ordered, problems, ordered.ToImmutableDictionary(asset => asset.Id, StringComparer.Ordinal), usedBy, version);
        }
    }
}
