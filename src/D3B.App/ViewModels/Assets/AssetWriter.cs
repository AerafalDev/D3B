using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>Writes to the project for the assets' panel, and says in a toast what cannot be written (D326, D358).</summary>
/// <remarks>
/// It keeps no row and no choice: the panel gives it assets, ids and folders, and is told after each write, so that it
/// shows the project again.
/// </remarks>
internal sealed class AssetWriter
{
    private const int CopyAttempts = 50;

    private readonly Localizer _localizer;
    private readonly Toasts _toasts;
    private readonly Func<AssetLibrary?> _library;
    private readonly Action _written;

    /// <summary>Initializes a new instance of the <see cref="AssetWriter"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    /// <param name="library">What gives the project, once the host runs.</param>
    /// <param name="written">What the panel does after each write, tried or not: shows the project again.</param>
    public AssetWriter(Localizer localizer, Toasts toasts, Func<AssetLibrary?> library, Action written)
    {
        _localizer = localizer;
        _toasts = toasts;
        _library = library;
        _written = written;
    }

    /// <summary>Writes to the project; a file that cannot be written is said in a toast, and the panel shows the project again either way.</summary>
    /// <param name="write">What to write.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task WriteAsync(Func<AssetLibrary, Task> write)
    {
        if (_library() is not { } assets)
            return;

        try
        {
            await write(assets).ConfigureAwait(true);
        }
        catch (AssetFormatException exception)
        {
            _toasts.Show(new Toast(_localizer.Format("AssetWriteFailed", _localizer["AssetProblem" + exception.Kind]), Tone.Danger));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _toasts.Show(new Toast(_localizer.Format("AssetWriteFailed", exception.GetBaseException().Message), Tone.Danger));
        }

        _written();
    }

    /// <summary>Gives an asset a name, its file with it: a name that cannot be, empty, the same, taken or not a file's, is said and left.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="name">The name typed.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task RenameAssetAsync(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        name = name.Trim();

        if (_library()?.Find(id) is not { IsGiven: false } asset || name.Length == 0 || name == asset.Name)
            return;

        if (AssetLibrary.NameProblem(name) is not null || _library()!.IsTaken(asset.Kind, name, asset.Id))
        {
            _toasts.Show(new Toast(_localizer[AssetLibrary.NameProblem(name) is not null ? "NameInvalid" : "NameTaken"], Tone.Danger));

            return;
        }

        await WriteAsync(assets => assets.SaveAsync(asset with { Name = name }, CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>Gives a folder a name, the assets in it with it: a name that cannot be is said and left.</summary>
    /// <param name="folder">The folder's path.</param>
    /// <param name="name">The name typed.</param>
    /// <returns>A task that completes with the folder's new path, or <see langword="null"/> when it was not renamed.</returns>
    public async Task<string?> RenameFolderAsync(string folder, string name)
    {
        if (AssetLibrary.NameProblem(name) is not null)
        {
            _toasts.Show(new Toast(_localizer["NameInvalid"], Tone.Danger));

            return null;
        }

        string? renamed = null;

        await WriteAsync(async assets => renamed = await assets.RenameFolderAsync(folder, name, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

        return renamed;
    }

    /// <summary>Copies assets of the project beside each, under names of their own.</summary>
    /// <param name="originals">The assets to copy.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task DuplicateAsync(IReadOnlyList<Asset> originals)
    {
        foreach (var asset in originals)
            await WriteAsync(assets => assets.SaveAsync(asset with { Id = AssetLibrary.NewId(), Name = CopyName(asset.Kind, asset.Name), Path = null }, CancellationToken.None, AssetPaths.ParentOf(asset.Path ?? ""))).ConfigureAwait(true);
    }

    /// <summary>Copies an asset given with D3B into a folder of the project, which then changes it (D296), and says so.</summary>
    /// <param name="asset">The asset given.</param>
    /// <param name="name">The name its row shows, from which the copy's name is made.</param>
    /// <param name="folder">The folder, empty for the root.</param>
    /// <returns>A task that completes with the copy, or <see langword="null"/> when it was not written.</returns>
    public async Task<Asset?> CopyToProjectAsync(Asset asset, string name, string folder)
    {
        var copyName = CopyName(asset.Kind, name);
        Asset? copy = null;

        await WriteAsync(async assets => copy = await assets.SaveAsync(asset with { Id = AssetLibrary.NewId(), Name = copyName, Title = null, Path = null }, CancellationToken.None, folder).ConfigureAwait(true)).ConfigureAwait(true);

        if (copy is not null)
            _toasts.Show(new Toast(_localizer.Format("AssetCopied", copy.Name), Tone.Success));

        return copy;
    }

    /// <summary>Writes an asset's file elsewhere, and says so.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="name">The name its row shows.</param>
    /// <param name="file">The file to write.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task ExportAsync(string id, string name, string file)
    {
        await WriteAsync(assets => assets.ExportAsync(id, file, CancellationToken.None)).ConfigureAwait(true);
        _toasts.Show(new Toast(_localizer.Format("AssetExported", name), Tone.Success));
    }

    /// <summary>Imports assets' files into a folder, and says so.</summary>
    /// <param name="files">The files.</param>
    /// <param name="folder">The folder, empty for the root.</param>
    /// <returns>A task that completes once imported.</returns>
    public async Task ImportFilesAsync(IReadOnlyList<string> files, string folder)
    {
        var imported = new List<Asset>();

        foreach (var file in files)
            await WriteAsync(async assets => imported.Add(await assets.ImportAsync(file, folder, CancellationToken.None).ConfigureAwait(true))).ConfigureAwait(true);

        if (imported.Count > 0)
            _toasts.Show(new Toast(imported.Count == 1 ? _localizer.Format("AssetImported", imported[0].Name) : _localizer.Plural("AssetsImported", imported.Count), Tone.Success));
    }

    /// <summary>Moves assets and folders of the project, by their rows' keys, to a folder; what cannot go there stays.</summary>
    /// <param name="keys">The rows' keys.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <param name="followed">What is told, as soon as a folder is moved, where it was and where it is now.</param>
    /// <returns>A task that completes once moved.</returns>
    public async Task MoveAsync(IReadOnlyCollection<string> keys, string folder, Action<string, string> followed)
    {
        // A folder chosen with what it holds moves alone, what it holds going with it.
        var folders = keys.Where(key => key.StartsWith(AssetPaths.FolderKey, StringComparison.Ordinal)).Select(key => key[AssetPaths.FolderKey.Length..]).ToList();

        foreach (var key in keys.Where(key => AssetPaths.CanMove(_library(), [key], folder)))
        {
            if (key.StartsWith(AssetPaths.FolderKey, StringComparison.Ordinal))
            {
                var moved = key[AssetPaths.FolderKey.Length..];

                if (folders.Any(other => !string.Equals(other, moved, StringComparison.OrdinalIgnoreCase) && AssetPaths.IsIn(moved, other)))
                    continue;

                string? placed = null;

                await WriteAsync(async assets => placed = await assets.MoveFolderAsync(moved, folder, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

                if (placed is not null)
                    followed(moved, placed);
            }
            else if (_library()?.Find(key) is { Path: { } path } && !folders.Any(other => AssetPaths.IsIn(AssetPaths.ParentOf(path), other)))
            {
                await WriteAsync(assets => assets.MoveAsync(key, folder, CancellationToken.None)).ConfigureAwait(true);
            }
        }
    }

    /// <summary>Deletes assets and folders of the project; the editors of the profiles deleted are left, and the characters that played them are left without one (D99).</summary>
    /// <param name="targets">The rows to delete.</param>
    /// <param name="profiles">The profiles, whose editors close.</param>
    /// <param name="book">The characters, if they are read.</param>
    /// <returns>A task that completes once deleted.</returns>
    public async Task DeleteAsync(IReadOnlyList<AssetRowViewModel> targets, ProfilesViewModel profiles, AccountBook? book)
    {
        if (_library() is not { } library)
            return;

        var deleted = new List<Asset>();

        foreach (var target in targets.Where(item => item.IsFolder))
        {
            foreach (var profile in library.AssetsIn(target.Folder).OfType<ProfileAsset>())
                profiles.Find(profile.Id)?.Cancel();

            await WriteAsync(async assets => deleted.AddRange(await assets.DeleteFolderAsync(target.Folder, CancellationToken.None).ConfigureAwait(true))).ConfigureAwait(true);
        }

        foreach (var target in targets.Where(item => item.IsAsset && library.Find(item.Id!) is not null))
        {
            var asset = library.Find(target.Id!)!;

            profiles.Find(asset.Id)?.Cancel();
            await WriteAsync(assets => assets.DeleteAsync(asset.Id, CancellationToken.None)).ConfigureAwait(true);
            deleted.Add(asset);
        }

        // The characters that played a profile deleted are left without one, as deleting it alone leaves them (D99).
        if (book is not null)
        {
            foreach (var profile in deleted.OfType<ProfileAsset>())
                await book.LeaveProfileAsync(profile.Id, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private string CopyName(AssetKind kind, string name)
    {
        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            var copy = attempt == 1 ? _localizer.Format("CopyOf", name) : _localizer.Format("CopyOfNumbered", name, attempt);

            if (_library()?.IsTaken(kind, copy) != true)
                return copy;
        }

        return name;
    }
}
