using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Assets;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Assets;

/// <summary>The assets' panel: the project's folders and their assets, then those given with D3B, read only (D326).</summary>
/// <remarks>
/// <para>
/// The tree is a list of rows, each indented by its depth, made again only once the project, the characters that play its
/// profiles, the language, the search or a fold changed: the rows' state, chosen or renamed, lives in the view model.
/// </para>
/// <para>
/// What a row's menu does goes to the project (<see cref="AssetLibrary"/>), whose writes the next refresh shows; what needs
/// the window, a file to pick or a folder to show, goes through what the window gives.
/// </para>
/// </remarks>
internal sealed partial class AssetsViewModel : ObservableObject
{
    private const int CopyAttempts = 50;

    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Toasts _toasts;
    private readonly ProfilesViewModel _profiles;
    private readonly HashSet<string> _folded = new(StringComparer.OrdinalIgnoreCase);
    private AssetLibrary? _assets;
    private AccountBook? _book;
    private string _shown = "";
    private bool _givenFolded;
    private AssetRowViewModel? _newFolder;

    /// <summary>Gets the tree's rows, in order.</summary>
    public ObservableCollection<AssetRowViewModel> Rows { get; } = [];

    /// <summary>Gets the kinds the Add menu offers, those of a later session disabled.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetKindChoice> Kinds { get; private set; } = [];

    /// <summary>Gets or sets the text the assets are searched by.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets the asset chosen, by its id, if any.</summary>
    [ObservableProperty]
    public partial string? ChosenId { get; private set; }

    /// <summary>Gets the folder chosen, where a new folder or an import goes; empty for the project's root.</summary>
    [ObservableProperty]
    public partial string ChosenFolder { get; private set; } = "";

    /// <summary>Gets whether the project is read: the host runs.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets whether the search finds nothing.</summary>
    [ObservableProperty]
    public partial bool FindsNothing { get; private set; }

    /// <summary>Gets or sets what shows an asset in the inspector.</summary>
    public Action<string>? Choose { get; set; }

    /// <summary>Gets or sets what opens an asset in its tab.</summary>
    public Action<string>? Open { get; set; }

    /// <summary>Gets or sets what shows what uses an asset: the inspector, opened on it.</summary>
    public Action<string>? ShowUsages { get; set; }

    /// <summary>Gets or sets what shows a file in the system's file manager.</summary>
    public Action<string>? Reveal { get; set; }

    /// <summary>Gets or sets what asks where to write an asset's file, from a name it suggests.</summary>
    public Func<string, Task<string?>>? PickExport { get; set; }

    /// <summary>Gets or sets what asks for an asset's file to import.</summary>
    public Func<Task<string?>>? PickImport { get; set; }

    /// <summary>Gets or sets what tells whether an asset's tab has changes not saved.</summary>
    public Func<string, bool>? IsUnsaved { get; set; }

    /// <summary>Gets the project, once the host runs.</summary>
    public AssetLibrary? Assets => _assets;

    /// <summary>Initializes a new instance of the <see cref="AssetsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    /// <param name="profiles">The profiles, whose editors and dialogs a profile's row uses.</param>
    public AssetsViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts, ProfilesViewModel profiles)
    {
        _localizer = localizer;
        _dialogs = dialogs;
        _toasts = toasts;
        _profiles = profiles;
    }

    /// <summary>Tells whether an asset opens in a tab today: a profile; the other kinds' editors come later (D306).</summary>
    /// <param name="row">The row.</param>
    /// <returns>Whether it opens.</returns>
    public static bool CanOpen(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.AssetKind is AssetKind.Profile;
    }

    private static (string Before, string Found, string After) Split(string name, string filter)
    {
        var at = filter.Length == 0 ? -1 : name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase);

        return at < 0 ? (name, "", "") : (name[..at], name.Substring(at, filter.Length), name[(at + filter.Length)..]);
    }

    private static string ParentOf(string folder)
    {
        return Path.GetDirectoryName(folder) ?? "";
    }

    /// <summary>Reads the project again, once it, its players, the language or the search changed.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _assets is null)
        {
            _assets = services.GetService<AssetLibrary>();
            _book = services.GetService<AccountBook>();
        }

        IsAvailable = _assets is not null;

        var unsaved = string.Join(',', _profiles.Open.Where(item => item.IsDirty).Select(item => item.Id));
        var shown = string.Join('|', _assets?.Version, _book?.Version, _localizer.Culture.Name, Filter, string.Join(',', _folded), _givenFolded, ChosenId, ChosenFolder, unsaved);

        if (shown == _shown || _newFolder is not null || Rows.Any(row => row.IsRenaming))
            return;

        _shown = shown;
        Kinds = [.. AssetKinds.All.Select(kind => new AssetKindChoice(kind, _localizer["AssetKind" + kind], AssetKinds.SessionOf(kind)))];
        Build();
    }

    /// <summary>Chooses a row: an asset shows in the inspector, a folder folds or unfolds, the given heading too.</summary>
    /// <param name="row">The row.</param>
    public void Select(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        switch (row.Kind)
        {
            case AssetRowKind.Folder:
                ChosenFolder = row.Folder;
                ChosenId = null;

                if (!_folded.Remove(row.Folder))
                    _folded.Add(row.Folder);

                break;
            case AssetRowKind.Given:
                _givenFolded = !_givenFolded;
                break;
            case AssetRowKind.Asset when row.Id is { } id:
                ChosenId = id;
                ChosenFolder = row.IsGiven ? "" : row.Folder;
                Choose?.Invoke(id);
                break;
        }

        _shown = "";
        Refresh(null);
    }

    /// <summary>Chooses the folder a row is, or is in, where a new folder or an import goes, without folding it.</summary>
    /// <param name="row">The row.</param>
    public void ChooseFolder(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        ChosenFolder = row.IsGiven ? "" : row.Folder;
    }

    /// <summary>Opens a row's asset in its tab: a double click or Enter.</summary>
    /// <param name="row">The row.</param>
    public void OpenRow(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row is { IsAsset: true, Id: { } id } && CanOpen(row))
            Open?.Invoke(id);
    }

    /// <summary>Shows what uses an asset.</summary>
    /// <param name="row">The row.</param>
    public void FindUsages(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Id is { } id)
            ShowUsages?.Invoke(id);
    }

    /// <summary>Starts renaming an asset or a folder of the project in place: F2.</summary>
    /// <param name="row">The row.</param>
    public void StartRename(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!(row.IsAsset || row.IsFolder) || row.IsGiven || row.IsNewFolder)
            return;

        foreach (var other in Rows.Where(other => other.IsRenaming && other != row))
            other.IsRenaming = false;

        row.EditName = row.Name;
        row.IsRenaming = true;
    }

    /// <summary>Gives a row the name typed: an asset is renamed, its file with it; a new folder is made.</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task CommitRenameAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsRenaming)
            return;

        row.IsRenaming = false;

        var name = row.EditName.Trim();

        if (row.IsNewFolder)
        {
            _newFolder = null;
            Rows.Remove(row);

            if (name.Length > 0)
                await WriteAsync(assets => assets.CreateFolderAsync(Path.Combine(row.Folder, name), CancellationToken.None)).ConfigureAwait(true);

            _shown = "";
            Refresh(null);

            return;
        }

        if (row.IsFolder)
        {
            if (name.Length > 0 && name != row.Name)
                await RenameFolderAsync(row, name).ConfigureAwait(true);

            return;
        }

        if (name.Length == 0 || name == row.Name || _assets?.Find(row.Id ?? "") is not { } asset)
            return;

        if (AssetLibrary.NameProblem(name) is not null || _assets.IsTaken(asset.Kind, name, asset.Id))
        {
            _toasts.Show(new Toast(_localizer[AssetLibrary.NameProblem(name) is not null ? "NameInvalid" : "NameTaken"], Tone.Danger));

            return;
        }

        await WriteAsync(assets => assets.SaveAsync(asset with { Name = name }, CancellationToken.None)).ConfigureAwait(true);
    }

    /// <summary>Drops what was typed in place: Escape.</summary>
    /// <param name="row">The row.</param>
    public void CancelRename(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        row.IsRenaming = false;

        if (row.IsNewFolder)
        {
            _newFolder = null;
            Rows.Remove(row);
            _shown = "";
            Refresh(null);
        }
    }

    /// <summary>Copies an asset of the project beside it, under a name of its own: Ctrl D.</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public Task DuplicateAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsGiven || _assets?.Find(row.Id ?? "") is not { } asset)
            return Task.CompletedTask;

        return WriteAsync(assets => assets.SaveAsync(asset with { Id = AssetLibrary.NewId(), Name = CopyName(asset.Kind, asset.Name), Path = null }, CancellationToken.None, row.Folder));
    }

    /// <summary>Copies an asset given with D3B into the project, which then changes it (D296).</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task CopyToProjectAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsGiven || _assets?.Find(row.Id ?? "") is not { } asset)
            return;

        var name = CopyName(asset.Kind, row.Name);
        Asset? copy = null;

        await WriteAsync(async assets => copy = await assets.SaveAsync(asset with { Id = AssetLibrary.NewId(), Name = name, Title = null, Path = null }, CancellationToken.None, ChosenFolder).ConfigureAwait(true)).ConfigureAwait(true);

        if (copy is not null)
        {
            _toasts.Show(new Toast(_localizer.Format("AssetCopied", copy.Name), Tone.Success));
            ChosenId = copy.Id;
            Choose?.Invoke(copy.Id);
        }
    }

    /// <summary>Moves an asset of the project to a folder.</summary>
    /// <param name="row">The row.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <returns>A task that completes once moved.</returns>
    public Task MoveAsync(AssetRowViewModel row, string folder)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.IsGiven || row.Id is not { } id ? Task.CompletedTask : WriteAsync(assets => assets.MoveAsync(id, folder, CancellationToken.None));
    }

    /// <summary>Gets the folders an asset may be moved to: the root, then each folder but its own.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Each folder's path, empty for the root, and its name as the menu says it.</returns>
    public IReadOnlyList<(string Folder, string Name)> FoldersFor(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return [.. AllFolders().Prepend("").Where(folder => !string.Equals(folder, row.Folder, StringComparison.OrdinalIgnoreCase)).Select(folder => (folder, folder.Length == 0 ? _localizer["ProjectRoot"] : folder.Replace(Path.DirectorySeparatorChar, '/')))];
    }

    /// <summary>Writes an asset's file elsewhere, where the user picks.</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task ExportAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Id is not { } id || row.AssetKind is not { } kind || PickExport is null || await PickExport(row.Name + AssetFiles.ExtensionOf(kind)).ConfigureAwait(true) is not { } file)
            return;

        await WriteAsync(assets => assets.ExportAsync(id, file, CancellationToken.None)).ConfigureAwait(true);
        _toasts.Show(new Toast(_localizer.Format("AssetExported", row.Name), Tone.Success));
    }

    /// <summary>Shows an asset's file, or a folder, in the system's file manager.</summary>
    /// <param name="row">The row.</param>
    public void ShowInFolder(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_assets is not { } assets)
            return;

        var path = row.Kind switch
        {
            AssetRowKind.Asset => assets.Find(row.Id ?? "") is { } asset ? assets.FileOf(asset) : null,
            AssetRowKind.Problem => Path.Combine(assets.Folder, row.Folder, row.Name),
            _ => Path.Combine(assets.Folder, row.Folder),
        };

        if (path is not null)
            Reveal?.Invoke(path);
    }

    /// <summary>Asks, then deletes an asset or a folder of the project: Delete. A profile asks as its editor does, its players told.</summary>
    /// <param name="row">The row.</param>
    public void AskDelete(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row is { IsFolder: true, IsNewFolder: false })
        {
            AskDeleteFolder(row);

            return;
        }

        if (row.IsGiven || row.Id is not { } id || _assets?.Find(id) is not { } asset)
            return;

        if (asset is ProfileAsset && _profiles.Find(id) is { } profile)
        {
            profile.AskDeleteCommand.Execute(null);

            return;
        }

        var users = _assets.UsedBy(id).Count;

        _dialogs.Ask(new QuestionViewModel(
            _dialogs,
            _localizer.Format("DeleteAssetTitle", row.Name),
            users == 0 ? null : _localizer.Plural("DeleteAssetUsers", users),
            _localizer["Delete"],
            _localizer["Cancel"],
            () => _ = WriteAsync(assets => assets.DeleteAsync(id, CancellationToken.None)),
            destructive: true));
    }

    /// <summary>Deletes a folder, its own folders and every asset in them, the tabs of its profiles closed and their players left without one.</summary>
    /// <param name="row">The folder's row.</param>
    /// <returns>A task that completes once deleted.</returns>
    public async Task DeleteFolderAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_assets is not { } assets || !row.IsFolder)
            return;

        foreach (var profile in assets.AssetsIn(row.Folder).OfType<ProfileAsset>())
            _profiles.Find(profile.Id)?.Cancel();

        IReadOnlyList<Asset> deleted = [];

        await WriteAsync(async library => deleted = await library.DeleteFolderAsync(row.Folder, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

        // The characters that played a profile deleted are left without one, as deleting it alone leaves them (D99).
        if (_book is { } book)
        {
            foreach (var profile in deleted.OfType<ProfileAsset>())
                await book.LeaveProfileAsync(profile.Id, CancellationToken.None).ConfigureAwait(true);
        }

        if (IsIn(ChosenFolder, row.Folder))
            ChosenFolder = "";

        _shown = "";
        Refresh(null);
    }

    /// <summary>Starts a new asset of a kind D3B makes today: a profile, named and given its activity in a dialog.</summary>
    /// <param name="kind">The kind.</param>
    public void New(AssetKind kind)
    {
        if (kind is AssetKind.Profile && _profiles.NewCommand.CanExecute(null))
            _profiles.NewCommand.Execute(null);
    }

    /// <summary>Starts a folder in the folder chosen, its name typed in place.</summary>
    [RelayCommand]
    public void NewFolder()
    {
        if (!IsAvailable || _newFolder is not null)
            return;

        _folded.Remove(ChosenFolder);
        _shown = "";
        Refresh(null);

        var parent = Rows.FirstOrDefault(row => row.IsFolder && string.Equals(row.Folder, ChosenFolder, StringComparison.OrdinalIgnoreCase));
        var depth = parent is null ? 0 : parent.Depth + 1;
        var at = parent is null ? 0 : Rows.IndexOf(parent) + 1;

        _newFolder = new AssetRowViewModel(AssetRowKind.Folder, "", depth) { Folder = ChosenFolder, IsNewFolder = true, IsExpanded = true, EditName = _localizer["NewFolder"], IsRenaming = true };
        Rows.Insert(at, _newFolder);
    }

    /// <summary>Imports an asset's file into the folder chosen.</summary>
    /// <returns>A task that completes once imported.</returns>
    [RelayCommand]
    public async Task ImportAsync()
    {
        if (!IsAvailable || PickImport is null || await PickImport().ConfigureAwait(true) is not { } file)
            return;

        Asset? imported = null;

        await WriteAsync(async assets => imported = await assets.ImportAsync(file, ChosenFolder, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

        if (imported is not null)
            _toasts.Show(new Toast(_localizer.Format("AssetImported", imported.Name), Tone.Success));
    }

    /// <summary>Unfolds every folder, as the search does.</summary>
    [RelayCommand]
    public void ExpandAll()
    {
        _folded.Clear();
        _givenFolded = false;
        _shown = "";
        Refresh(null);
    }

    /// <summary>Folds every folder.</summary>
    [RelayCommand]
    public void CollapseAll()
    {
        foreach (var folder in AllFolders())
            _folded.Add(folder);

        _givenFolded = true;
        _shown = "";
        Refresh(null);
    }

    private static bool IsIn(string folder, string parent)
    {
        return string.Equals(folder, parent, StringComparison.OrdinalIgnoreCase) || folder.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void AskDeleteFolder(AssetRowViewModel row)
    {
        var count = _assets?.AssetsIn(row.Folder).Count ?? 0;

        _dialogs.Ask(new QuestionViewModel(
            _dialogs,
            _localizer.Format("DeleteFolderTitle", row.Name),
            count == 0 ? _localizer["DeleteFolderEmpty"] : _localizer.Plural("DeleteFolderAssets", count),
            _localizer["Delete"],
            _localizer["Cancel"],
            () => _ = DeleteFolderAsync(row),
            destructive: true));
    }

    private async Task RenameFolderAsync(AssetRowViewModel row, string name)
    {
        if (AssetLibrary.NameProblem(name) is not null)
        {
            _toasts.Show(new Toast(_localizer["NameInvalid"], Tone.Danger));

            return;
        }

        string? renamed = null;

        await WriteAsync(async assets => renamed = await assets.RenameFolderAsync(row.Folder, name, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

        if (renamed is null)
            return;

        // What was folded or chosen under the old name follows the folder.
        foreach (var folded in _folded.Where(folder => IsIn(folder, row.Folder)).ToList())
        {
            _folded.Remove(folded);
            _folded.Add(renamed + folded[row.Folder.Length..]);
        }

        if (IsIn(ChosenFolder, row.Folder))
            ChosenFolder = renamed + ChosenFolder[row.Folder.Length..];

        _shown = "";
        Refresh(null);
    }

    private IEnumerable<string> AllFolders()
    {
        if (_assets is not { } assets)
            return [];

        var folders = new HashSet<string>(assets.Folders, StringComparer.OrdinalIgnoreCase);

        foreach (var path in assets.Assets.Select(asset => asset.Path).Concat(assets.Problems.Select(problem => problem.Path)).OfType<string>())
        {
            for (var folder = ParentOf(path); folder.Length > 0; folder = ParentOf(folder))
                folders.Add(folder);
        }

        return folders.Order(StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true));
    }

    private void Build()
    {
        var rows = new List<AssetRowViewModel>();

        if (_assets is { } assets)
        {
            var filter = Filter.Trim();
            var folders = AllFolders().ToList();
            var own = assets.Assets.Where(asset => !asset.IsGiven).ToList();
            var given = assets.Assets.Where(asset => asset.IsGiven).ToList();

            AddFolder("", 0, folders, own, assets.Problems, filter, rows);

            var givenRows = given.Select(asset => AssetRow(asset, 1, "", filter)).OfType<AssetRowViewModel>().ToList();

            if (givenRows.Count > 0)
            {
                var unfolded = !_givenFolded || filter.Length > 0;

                rows.Add(new AssetRowViewModel(AssetRowKind.Given, _localizer["GivenAssets"], 0) { Count = given.Count.ToString(_localizer.Culture), IsExpanded = unfolded });

                if (unfolded)
                    rows.AddRange(givenRows);
            }

            FindsNothing = filter.Length > 0 && rows.All(row => !row.IsAsset && !row.IsProblem);
        }

        Rows.Clear();

        foreach (var row in rows)
            Rows.Add(row);
    }

    // A folder's rows, its own folders first, then its assets and the files that do not read; with a search, only what holds
    // the text, its folders unfolded to it.
    private int AddFolder(string folder, int depth, List<string> folders, List<Asset> own, IReadOnlyList<AssetProblem> problems, string filter, List<AssetRowViewModel> rows)
    {
        var count = 0;

        foreach (var child in folders.Where(other => string.Equals(ParentOf(other), folder, StringComparison.OrdinalIgnoreCase)))
        {
            var unfolded = filter.Length > 0 || !_folded.Contains(child);
            var at = rows.Count;
            var inner = new List<AssetRowViewModel>();
            var held = AddFolder(child, depth + 1, folders, own, problems, filter, inner);

            if (filter.Length > 0 && inner.Count == 0)
                continue;

            rows.Insert(at, new AssetRowViewModel(AssetRowKind.Folder, Path.GetFileName(child), depth)
            {
                Folder = child,
                Count = held > 0 ? held.ToString(_localizer.Culture) : "",
                IsExpanded = unfolded,
                IsSelected = ChosenId is null && string.Equals(ChosenFolder, child, StringComparison.OrdinalIgnoreCase),
            });

            if (unfolded)
                rows.AddRange(inner);

            count += held;
        }

        foreach (var asset in own.Where(asset => string.Equals(ParentOf(asset.Path ?? ""), folder, StringComparison.OrdinalIgnoreCase)))
        {
            count++;

            if (AssetRow(asset, depth, folder, filter) is { } row)
                rows.Add(row);
        }

        foreach (var problem in problems.Where(problem => string.Equals(ParentOf(problem.Path), folder, StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileName(problem.Path);

            if (filter.Length > 0 && !name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                continue;

            var (before, found, after) = Split(name, filter);

            rows.Add(new AssetRowViewModel(AssetRowKind.Problem, name, depth)
            {
                Folder = folder,
                Problem = _localizer["AssetProblem" + problem.Kind],
                AssetKind = AssetFiles.TryKindOf(name, out var kind, out _) ? kind : null,
                Before = before,
                Found = found,
                After = after,
            });
        }

        return count;
    }

    private AssetRowViewModel? AssetRow(Asset asset, int depth, string folder, string filter)
    {
        var name = asset.Title?.In(_localizer.Culture) ?? asset.Name;

        if (filter.Length > 0 && !name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            return null;

        var users = _assets!.UsedBy(asset.Id).Count + (_book?.Characters.Count(character => character.Profile == asset.Id) ?? 0);
        var (before, found, after) = Split(name, filter);

        return new AssetRowViewModel(AssetRowKind.Asset, name, depth)
        {
            Id = asset.Id,
            AssetKind = asset.Kind,
            Folder = folder,
            IsGiven = asset.IsGiven,
            Count = users > 0 ? users.ToString(_localizer.Culture) : "",
            CountTip = users > 0 ? _localizer.Plural("UsedByCount", users) : null,
            IsUnsaved = IsUnsaved?.Invoke(asset.Id) == true,
            IsSelected = asset.Id == ChosenId,
            Before = before,
            Found = found,
            After = after,
        };
    }

    private string CopyName(AssetKind kind, string name)
    {
        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            var copy = attempt == 1 ? _localizer.Format("CopyOf", name) : _localizer.Format("CopyOfNumbered", name, attempt);

            if (_assets?.IsTaken(kind, copy) != true)
                return copy;
        }

        return name;
    }

    private async Task WriteAsync(Func<AssetLibrary, Task> write)
    {
        if (_assets is not { } assets)
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

        _shown = "";
        Refresh(null);
    }

    partial void OnFilterChanged(string value)
    {
        Refresh(null);
    }
}
