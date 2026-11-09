using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Assets;

/// <summary>The assets' panel: the project's folders and their assets, then those given with D3B, read only (D326, D358).</summary>
/// <remarks>
/// <para>
/// The tree is a list of rows, each indented by its depth, made again only once the project, the characters that play its
/// profiles, the language, the search, the kinds shown, a fold or the selection changed: the rows' state lives here, keyed
/// by each row's <see cref="AssetRowViewModel.Key"/>, so that a tree made again keeps what was chosen.
/// </para>
/// <para>
/// Several rows are chosen at once, by Ctrl and Shift; what acts on them, delete, move, cut and paste, a drag, acts on all
/// the project's own among them. Writes go to the project (<see cref="AssetLibrary"/>), whose next refresh shows them; what
/// needs the window, a file to pick, a folder to show or the clipboard, goes through what the window gives.
/// </para>
/// </remarks>
internal sealed partial class AssetsViewModel : ObservableObject
{
    private const int CopyAttempts = 50;
    private const string FolderKey = AssetPaths.FolderKey;
    private const string ProblemKey = AssetPaths.ProblemKey;

    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Toasts _toasts;
    private readonly ProfilesViewModel _profiles;
    private readonly AssetWriter _writer;
    private readonly HashSet<string> _folded = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _cut = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<AssetKind> _hidden = [];
    private AssetLibrary? _assets;
    private AccountBook? _book;
    private string _shown = "";
    private bool _givenFolded;
    private string? _anchor;
    private AssetRowViewModel? _newFolder;

    /// <summary>Gets the tree's rows, in order.</summary>
    public ObservableCollection<AssetRowViewModel> Rows { get; } = [];

    /// <summary>Gets the kinds the Add menu offers, those of a later session disabled.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetKindChoice> Kinds { get; private set; } = [];

    /// <summary>Gets or sets the text the assets are searched by.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets the row the keyboard is on, the last one chosen, by its key.</summary>
    [ObservableProperty]
    public partial string? FocusKey { get; private set; }

    /// <summary>Gets the folder where a new asset, folder or import goes: the one chosen or the chosen asset's; empty for the root.</summary>
    [ObservableProperty]
    public partial string ChosenFolder { get; private set; } = "";

    /// <summary>Gets whether the project is read: the host runs.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets whether the search or the kinds shown leave nothing.</summary>
    [ObservableProperty]
    public partial bool FindsNothing { get; private set; }

    /// <summary>Gets whether the project holds nothing of its own yet.</summary>
    [ObservableProperty]
    public partial bool IsProjectEmpty { get; private set; }

    /// <summary>Gets whether some kinds are hidden, which the filter's button says.</summary>
    [ObservableProperty]
    public partial bool IsFiltered { get; private set; }

    /// <summary>Gets the asset chosen alone, by its id, if one is.</summary>
    public string? ChosenId => _selected.Count == 1 && _selected.First() is { } key && !key.StartsWith(FolderKey, StringComparison.Ordinal) && !key.StartsWith(ProblemKey, StringComparison.Ordinal) ? key : null;

    /// <summary>Gets whether something cut waits to be pasted.</summary>
    public bool HasCut => _cut.Count > 0;

    /// <summary>Gets or sets what shows an asset in the inspector.</summary>
    public Action<string>? Choose { get; set; }

    /// <summary>Gets or sets what opens an asset in its tab.</summary>
    public Action<string>? Open { get; set; }

    /// <summary>Gets or sets what shows what uses an asset: the inspector, opened on it.</summary>
    public Action<string>? ShowUsages { get; set; }

    /// <summary>Gets or sets what shows a file or folder in the system's file manager.</summary>
    public Action<string>? Reveal { get; set; }

    /// <summary>Gets or sets what puts a text in the clipboard.</summary>
    public Action<string>? CopyText { get; set; }

    /// <summary>Gets or sets what asks where to write an asset's file, from a name it suggests.</summary>
    public Func<string, Task<string?>>? PickExport { get; set; }

    /// <summary>Gets or sets what asks for assets' files to import.</summary>
    public Func<Task<IReadOnlyList<string>>>? PickImport { get; set; }

    /// <summary>Gets or sets what tells whether an asset's tab has changes not saved.</summary>
    public Func<string, bool>? IsUnsaved { get; set; }

    /// <summary>Gets or sets what tells the asset the editor's tab shows, if it shows one.</summary>
    public Func<string?>? OpenAssetId { get; set; }

    /// <summary>Gets or sets the routes, whose editors a route's row opens and whose new ones this makes (D371).</summary>
    public Routes.RoutesViewModel? Routes { get; set; }

    /// <summary>Gets or sets the zones, whose editors a zone's row opens and whose new ones this makes (D394).</summary>
    public Zones.ZonesViewModel? Zones { get; set; }

    /// <summary>Gets or sets the lists of steps, the behaviors this page makes and opens (D386).</summary>
    public Sequences.SequencesViewModel? Sequences { get; set; }

    /// <summary>Gets or sets what remembers the folders folded, for the next start.</summary>
    public Action<IReadOnlyCollection<string>>? FoldsChanged { get; set; }

    /// <summary>Gets every folder of the project, relative to its own, folded ones included.</summary>
    public IReadOnlyList<string> AllFolderPaths => [.. AssetPaths.AllFolders(_assets)];

    /// <summary>Gets the project, once the host runs.</summary>
    public AssetLibrary? Assets => _assets;

    /// <summary>Initializes a new instance of the <see cref="AssetsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    /// <param name="profiles">The profiles, whose editors and dialogs a profile's row uses.</param>
    /// <param name="folded">The folders folded when the app was left.</param>
    public AssetsViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts, ProfilesViewModel profiles, IEnumerable<string>? folded = null)
    {
        _localizer = localizer;
        _dialogs = dialogs;
        _toasts = toasts;
        _profiles = profiles;
        _writer = new AssetWriter(localizer, toasts, () => _assets, Rebuild);

        foreach (var folder in folded ?? [])
            _folded.Add(folder);
    }

    /// <summary>Tells whether an asset opens in a tab today: a profile or a route; the other kinds' editors come later (D306).</summary>
    /// <param name="row">The row.</param>
    /// <returns>Whether it opens.</returns>
    public static bool CanOpen(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.AssetKind is AssetKind.Profile or AssetKind.Route or AssetKind.Behavior or AssetKind.Zone;
    }

    /// <summary>Reads the project again, once it, its players, the language, the search or the selection changed.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _assets is null)
        {
            _assets = services.GetService<AssetLibrary>();
            _book = services.GetService<AccountBook>();
        }

        IsAvailable = _assets is not null;

        var unsaved = string.Join(',', _profiles.Open.Where(item => item.IsDirty).Select(item => item.Id).Concat(Routes?.Open.Where(item => item.IsDirty).Select(item => item.Id) ?? []).Concat(Zones?.Open.Where(item => item.IsDirty).Select(item => item.Id) ?? []));
        var shown = string.Join('|', _assets?.Version, _book?.Version, _localizer.Culture.Name, Filter, string.Join(',', _folded), _givenFolded, string.Join(',', _selected), string.Join(',', _cut), string.Join(',', _hidden), FocusKey, unsaved);

        // A name being typed keeps its row: the tree waits for it.
        if (shown == _shown || _newFolder is not null || Rows.Any(row => row.IsRenaming))
            return;

        _shown = shown;
        Kinds = [.. AssetKinds.All.Select(kind => new AssetKindChoice(kind, _localizer["AssetKind" + kind], AssetKinds.SessionOf(kind)))];
        Build();
    }

    /// <summary>Chooses a row by a click: alone, added to the others with Ctrl, or the range from the last with Shift.</summary>
    /// <param name="row">The row.</param>
    /// <param name="add">Whether Ctrl is held.</param>
    /// <param name="range">Whether Shift is held.</param>
    public void Click(AssetRowViewModel row, bool add = false, bool range = false)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsGivenHeading)
        {
            _givenFolded = !_givenFolded;
            Rebuild();

            return;
        }

        if (range && _anchor is { } anchor && IndexOf(anchor) is var from and >= 0 && Rows.IndexOf(row) is var to and >= 0)
        {
            if (!add)
                _selected.Clear();

            foreach (var item in Rows.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1).Where(item => item.IsSelectable))
                _selected.Add(item.Key);
        }
        else if (add)
        {
            if (!_selected.Remove(row.Key))
                _selected.Add(row.Key);

            _anchor = row.Key;
        }
        else
        {
            _selected.Clear();
            _selected.Add(row.Key);
            _anchor = row.Key;
        }

        Focus(row);
    }

    /// <summary>Chooses nothing: a click on the tree's empty space, so that a new folder or import goes to the root.</summary>
    public void ClearSelection()
    {
        _selected.Clear();
        _anchor = null;
        FocusKey = null;
        ChosenFolder = "";
        Rebuild();
    }

    /// <summary>Folds or unfolds a folder, or the given assets' heading.</summary>
    /// <param name="row">The row.</param>
    public void Toggle(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsGivenHeading)
            _givenFolded = !_givenFolded;
        else if (row.IsFolder && !_folded.Remove(row.Folder))
            _folded.Add(row.Folder);

        RememberFolds();
        Rebuild();
    }

    /// <summary>Moves the keyboard to the row above or below, the selection with it, or growing with Shift.</summary>
    /// <param name="step">-1 for up, 1 for down; more for a page.</param>
    /// <param name="range">Whether Shift is held.</param>
    public void MoveFocus(int step, bool range = false)
    {
        var rows = Rows.Where(row => row.IsSelectable || row.IsGivenHeading).ToList();

        if (rows.Count == 0)
            return;

        var at = FocusKey is { } key ? rows.FindIndex(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase)) : -1;
        var next = rows[Math.Clamp(at < 0 ? (step > 0 ? 0 : rows.Count - 1) : at + step, 0, rows.Count - 1)];

        Click(next, range: range);
    }

    /// <summary>Unfolds the folder the keyboard is on, or goes to its first row once unfolded: the right arrow.</summary>
    public void Expand()
    {
        if (Focused() is not { } row)
            return;

        if ((row.IsFolder && !row.IsExpanded) || (row.IsGivenHeading && _givenFolded))
            Toggle(row);
        else if (row.IsFolder || row.IsGivenHeading)
            MoveFocus(1);
    }

    /// <summary>Folds the folder the keyboard is on, or goes to the folder it is in: the left arrow.</summary>
    public void Collapse()
    {
        if (Focused() is not { } row)
            return;

        if (row.IsFolder && row.IsExpanded)
        {
            Toggle(row);

            return;
        }

        var parent = row.IsFolder ? AssetPaths.ParentOf(row.Folder) : row.Folder;

        if (Rows.FirstOrDefault(item => item.IsFolder && string.Equals(item.Folder, parent, StringComparison.OrdinalIgnoreCase)) is { } folder && !row.IsGiven)
            Click(folder);
    }

    /// <summary>Opens a row: an asset in its tab, a folder folded or unfolded; a kind without an editor yet says so.</summary>
    /// <param name="row">The row.</param>
    public void OpenRow(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsFolder || row.IsGivenHeading)
        {
            Toggle(row);

            return;
        }

        if (row is not { IsAsset: true, Id: { } id, AssetKind: { } kind })
            return;

        if (CanOpen(row))
            Open?.Invoke(id);
        else
            _toasts.Show(new Toast(_localizer.Format("AssetEditorLater", _localizer["AssetKind" + kind], AssetKinds.SessionOf(kind)), Tone.Info));
    }

    /// <summary>Opens what the keyboard is on: Enter.</summary>
    public void OpenFocused()
    {
        if (Focused() is { } row)
            OpenRow(row);
    }

    /// <summary>Shows an asset in the tree, its folders unfolded, chosen alone: the open tab's, located.</summary>
    /// <param name="id">The asset's id.</param>
    public void Locate(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_assets?.Find(id) is not { } asset)
            return;

        if (asset.IsGiven)
            _givenFolded = false;

        for (var folder = AssetPaths.ParentOf(asset.Path ?? ""); folder.Length > 0; folder = AssetPaths.ParentOf(folder))
            _folded.Remove(folder);

        Filter = "";
        _hidden.Clear();
        _selected.Clear();
        _selected.Add(id);
        _anchor = id;
        FocusKey = id;
        ChosenFolder = asset.IsGiven ? "" : AssetPaths.ParentOf(asset.Path ?? "");
        RememberFolds();
        Rebuild();
        Choose?.Invoke(id);
    }

    /// <summary>Shows the asset of the editor's tab in the tree.</summary>
    [RelayCommand]
    public void LocateOpen()
    {
        if (OpenAssetId?.Invoke() is { } id)
            Locate(id);
    }

    /// <summary>Shows what uses an asset.</summary>
    /// <param name="row">The row.</param>
    public void FindUsages(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Id is { } id)
            ShowUsages?.Invoke(id);
    }

    /// <summary>Puts a row's path, relative to the project, in the clipboard.</summary>
    /// <param name="row">The row.</param>
    public void CopyPath(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (PathOf(row) is { } path)
            CopyText?.Invoke(path);
    }

    /// <summary>Starts renaming the asset or folder the keyboard is on, or a row, in place: F2.</summary>
    /// <param name="row">The row, or <see langword="null"/> for the one the keyboard is on.</param>
    public void StartRename(AssetRowViewModel? row = null)
    {
        row ??= Focused();

        if (row is null || !(row.IsAsset || row.IsFolder) || row.IsGiven || row.IsNewFolder)
            return;

        foreach (var other in Rows.Where(other => other.IsRenaming && other != row))
            other.IsRenaming = false;

        row.EditName = row.Name;
        row.IsRenaming = true;
    }

    /// <summary>Gives a row the name typed: an asset is renamed, its file with it; a folder is renamed or made.</summary>
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
            {
                var made = Path.Combine(row.Folder, name);

                await _writer.WriteAsync(assets => assets.CreateFolderAsync(made, CancellationToken.None)).ConfigureAwait(true);
                SelectOnly(FolderKey + made, made);
            }

            Rebuild();

            return;
        }

        if (row.IsFolder)
        {
            if (name.Length > 0 && name != row.Name)
            {
                if (await _writer.RenameFolderAsync(row.Folder, name).ConfigureAwait(true) is { } renamed)
                    Follow(row.Folder, renamed);
            }

            Rebuild();

            return;
        }

        if (name.Length == 0 || name == row.Name || _assets?.Find(row.Id ?? "") is null)
        {
            Rebuild();

            return;
        }

        await RenameAssetAsync(row.Id!, name).ConfigureAwait(true);
        Rebuild();
    }

    /// <summary>Gives an asset a name, its file with it, as the tree and the inspector's name field do: a name that cannot be, empty, the same, taken or not a file's, is said and left.</summary>
    /// <param name="id">The asset's id.</param>
    /// <param name="name">The name typed.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task RenameAssetAsync(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        await _writer.RenameAssetAsync(id, name).ConfigureAwait(true);
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
        }

        Rebuild();
    }

    /// <summary>Copies the assets chosen, of the project, beside each, under names of their own: Ctrl D.</summary>
    /// <param name="row">A row to copy, or <see langword="null"/> for those chosen.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task DuplicateAsync(AssetRowViewModel? row = null)
    {
        await _writer.DuplicateAsync([.. Targets(row).Where(item => item.IsAsset && !item.IsGiven).Select(item => _assets?.Find(item.Id ?? "")).OfType<Asset>()]).ConfigureAwait(true);
    }

    /// <summary>Copies an asset given with D3B into the folder chosen, which then changes it (D296).</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task CopyToProjectAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.IsGiven || _assets?.Find(row.Id ?? "") is not { } asset)
            return;

        if (await _writer.CopyToProjectAsync(asset, row.Name, ChosenFolder).ConfigureAwait(true) is { } copy)
            Locate(copy.Id);
    }

    /// <summary>Moves an asset or a folder of the project to a folder.</summary>
    /// <param name="row">The row.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <returns>A task that completes once moved.</returns>
    public Task MoveAsync(AssetRowViewModel row, string folder)
    {
        ArgumentNullException.ThrowIfNull(row);

        return MoveAsync([row.Key], folder);
    }

    /// <summary>Moves assets and folders of the project, by their rows' keys, to a folder; what cannot go there stays.</summary>
    /// <param name="keys">The rows' keys.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <returns>A task that completes once moved.</returns>
    public async Task MoveAsync(IReadOnlyCollection<string> keys, string folder)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(folder);

        // A folder chosen with what it holds moves alone, what it holds going with it.
        var folders = keys.Where(key => key.StartsWith(FolderKey, StringComparison.Ordinal)).Select(key => key[FolderKey.Length..]).ToList();

        foreach (var key in keys.Where(key => CanMove([key], folder)))
        {
            if (key.StartsWith(FolderKey, StringComparison.Ordinal))
            {
                var moved = key[FolderKey.Length..];

                if (folders.Any(other => !string.Equals(other, moved, StringComparison.OrdinalIgnoreCase) && AssetPaths.IsIn(moved, other)))
                    continue;

                string? placed = null;

                await _writer.WriteAsync(async assets => placed = await assets.MoveFolderAsync(moved, folder, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

                if (placed is not null)
                    Follow(moved, placed);
            }
            else if (_assets?.Find(key) is { Path: { } path } && !folders.Any(other => AssetPaths.IsIn(AssetPaths.ParentOf(path), other)))
            {
                await _writer.WriteAsync(assets => assets.MoveAsync(key, folder, CancellationToken.None)).ConfigureAwait(true);
            }
        }

        _folded.Remove(folder);
        RememberFolds();
        Rebuild();
    }

    /// <summary>Tells whether rows may be moved to a folder: the project's own, never a folder into itself or where it is.</summary>
    /// <param name="keys">The rows' keys.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <returns>Whether at least one goes somewhere new.</returns>
    public bool CanMove(IReadOnlyCollection<string> keys, string folder)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(folder);

        return keys.Any(key => key.StartsWith(FolderKey, StringComparison.Ordinal)
            ? !AssetPaths.IsIn(folder, key[FolderKey.Length..]) && !string.Equals(AssetPaths.ParentOf(key[FolderKey.Length..]), folder, StringComparison.OrdinalIgnoreCase)
            : _assets?.Find(key) is { IsGiven: false, Path: { } path } && !string.Equals(AssetPaths.ParentOf(path), folder, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets the keys of the rows a drag from a row takes: those chosen when it is one of them, else it alone.</summary>
    /// <param name="row">The row the drag starts from.</param>
    /// <returns>The keys, of the project's own rows.</returns>
    public IReadOnlyList<string> DragKeys(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return [.. Targets(row).Where(item => item.IsMovable).Select(item => item.Key)];
    }

    /// <summary>Marks the folder a drag is over, or none.</summary>
    /// <param name="folder">The folder's row, or <see langword="null"/> when the drag left the tree.</param>
    public void MarkDrop(AssetRowViewModel? folder)
    {
        foreach (var row in Rows)
            row.IsDropTarget = row == folder;
    }

    /// <summary>Cuts the rows chosen, to paste them in another folder: Ctrl X.</summary>
    public void Cut()
    {
        _cut.Clear();

        foreach (var row in Targets(null).Where(item => item.IsMovable))
            _cut.Add(row.Key);

        OnPropertyChanged(nameof(HasCut));
        Rebuild();
    }

    /// <summary>Moves what was cut into the folder chosen: Ctrl V.</summary>
    /// <returns>A task that completes once moved.</returns>
    public async Task PasteAsync()
    {
        if (_cut.Count == 0)
            return;

        var keys = _cut.ToList();

        _cut.Clear();
        OnPropertyChanged(nameof(HasCut));
        await MoveAsync(keys, ChosenFolder).ConfigureAwait(true);
    }

    /// <summary>Gets the folders an asset or folder may be moved to: the root, then each folder but where it is.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Each folder's path, empty for the root, and its name as the menu says it.</returns>
    public IReadOnlyList<(string Folder, string Name)> FoldersFor(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return [.. AssetPaths.AllFolders(_assets).Prepend("").Where(folder => CanMove([row.Key], folder)).Select(folder => (folder, folder.Length == 0 ? _localizer["ProjectRoot"] : folder.Replace(Path.DirectorySeparatorChar, '/')))];
    }

    /// <summary>Writes an asset's file elsewhere, where the user picks.</summary>
    /// <param name="row">The row.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task ExportAsync(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Id is not { } id || row.AssetKind is not { } kind || PickExport is null || await PickExport(row.Name + AssetFiles.ExtensionOf(kind)).ConfigureAwait(true) is not { } file)
            return;

        await _writer.ExportAsync(id, row.Name, file).ConfigureAwait(true);
    }

    /// <summary>Shows an asset's file, or a folder, in the system's file manager; nothing chosen, the project's own folder.</summary>
    /// <param name="row">The row, or <see langword="null"/> for the project's folder.</param>
    public void ShowInFolder(AssetRowViewModel? row)
    {
        if (_assets is not { } assets)
            return;

        var path = row is null ? assets.Folder : row.Kind switch
        {
            AssetRowKind.Asset => assets.Find(row.Id ?? "") is { } asset ? assets.FileOf(asset) : null,
            AssetRowKind.Problem => Path.Combine(assets.Folder, row.Folder, row.Name),
            _ => Path.Combine(assets.Folder, row.Folder),
        };

        if (path is not null)
            Reveal?.Invoke(path);
    }

    /// <summary>Asks, then deletes the rows chosen of the project, or one row: Delete. A profile alone asks as its editor does.</summary>
    /// <param name="row">A row, or <see langword="null"/> for those chosen.</param>
    public void AskDelete(AssetRowViewModel? row = null)
    {
        var targets = Targets(row).Where(item => item.IsMovable).ToList();

        if (targets.Count == 0)
            return;

        if (targets is [{ IsAsset: true, Id: { } id }] && _profiles.Find(id) is { } profile)
        {
            profile.AskDeleteCommand.Execute(null);

            return;
        }

        var held = targets.Where(item => item.IsFolder).Sum(item => _assets?.AssetsIn(item.Folder).Count ?? 0);
        var title = targets is [var single]
            ? _localizer.Format(single.IsFolder ? "DeleteFolderTitle" : "DeleteAssetTitle", single.Name)
            : _localizer.Plural("DeleteItemsTitle", targets.Count);
        var users = targets.Where(item => item.IsAsset).Sum(item => _assets?.UsedBy(item.Id!).Count ?? 0);
        var detail = string.Join(" ", new[]
        {
            held > 0 ? _localizer.Plural("DeleteFolderAssets", held) : targets.All(item => item.IsFolder) ? _localizer["DeleteFolderEmpty"] : null,
            users > 0 ? _localizer.Plural("DeleteAssetUsers", users) : null,
            _localizer["DeleteToRecycleBin"],
        }.OfType<string>());

        _dialogs.Ask(new QuestionViewModel(_dialogs, title, detail, _localizer["Delete"], _localizer["Cancel"], () => _ = DeleteAsync(targets), destructive: true));
    }

    /// <summary>Starts a new asset of a kind D3B makes today in the folder chosen: a profile, named and given its activity in a dialog, or an empty route opened in its tab.</summary>
    /// <param name="kind">The kind.</param>
    public void New(AssetKind kind)
    {
        if (kind is AssetKind.Profile)
            _profiles.NewIn(ChosenFolder);
        else if (kind is AssetKind.Route && Routes is { IsAvailable: true } routes)
            _ = routes.CreateAsync(ChosenFolder);
        else if (kind is AssetKind.Behavior && Sequences is { IsAvailable: true } sequences)
            _ = sequences.CreateAsync(ChosenFolder);
        else if (kind is AssetKind.Zone && Zones is { IsAvailable: true } zones)
            _ = zones.CreateAsync(ChosenFolder);
    }

    /// <summary>Starts a folder in the folder chosen, or at the root when nothing is, its name typed in place.</summary>
    [RelayCommand]
    public void NewFolder()
    {
        if (!IsAvailable || _newFolder is not null)
            return;

        var parent = ChosenFolder;

        _folded.Remove(parent);
        Rebuild();

        var row = Rows.FirstOrDefault(item => item.IsFolder && string.Equals(item.Folder, parent, StringComparison.OrdinalIgnoreCase));
        var depth = row is null ? 0 : row.Depth + 1;
        var at = row is null ? 0 : Rows.IndexOf(row) + 1;

        _newFolder = new AssetRowViewModel(AssetRowKind.Folder, "", depth) { Folder = parent, IsNewFolder = true, IsExpanded = true, EditName = FreeFolderName(parent), IsRenaming = true };
        Rows.Insert(at, _newFolder);
    }

    /// <summary>Imports assets' files into the folder chosen, picked by the user.</summary>
    /// <returns>A task that completes once imported.</returns>
    [RelayCommand]
    public async Task ImportAsync()
    {
        if (IsAvailable && PickImport is not null && await PickImport().ConfigureAwait(true) is { Count: > 0 } files)
            await ImportFilesAsync(files, ChosenFolder).ConfigureAwait(true);
    }

    /// <summary>Imports assets' files into a folder, as a drop from the system's file manager does.</summary>
    /// <param name="files">The files.</param>
    /// <param name="folder">The folder, empty for the root.</param>
    /// <returns>A task that completes once imported.</returns>
    public async Task ImportFilesAsync(IReadOnlyList<string> files, string folder)
    {
        ArgumentNullException.ThrowIfNull(files);

        await _writer.ImportFilesAsync(files, folder).ConfigureAwait(true);
    }

    /// <summary>Shows or hides a kind.</summary>
    /// <param name="kind">The kind.</param>
    public void ToggleKind(AssetKind kind)
    {
        if (!_hidden.Remove(kind))
            _hidden.Add(kind);

        IsFiltered = _hidden.Count > 0;
        Rebuild();
    }

    /// <summary>Tells whether a kind shows.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Whether it shows.</returns>
    public bool Shows(AssetKind kind)
    {
        return !_hidden.Contains(kind);
    }

    /// <summary>Shows every kind again.</summary>
    [RelayCommand]
    public void ShowAllKinds()
    {
        _hidden.Clear();
        IsFiltered = false;
        Rebuild();
    }

    /// <summary>Unfolds every folder.</summary>
    [RelayCommand]
    public void ExpandAll()
    {
        _folded.Clear();
        _givenFolded = false;
        RememberFolds();
        Rebuild();
    }

    /// <summary>Folds every folder.</summary>
    [RelayCommand]
    public void CollapseAll()
    {
        foreach (var folder in AssetPaths.AllFolders(_assets))
            _folded.Add(folder);

        _givenFolded = true;
        RememberFolds();
        Rebuild();
    }

    /// <summary>Reads the project's folder again from the disk, as a change made beside D3B asks.</summary>
    /// <returns>A task that completes once read.</returns>
    [RelayCommand]
    public async Task ReloadAsync()
    {
        await _writer.WriteAsync(assets => assets.ReloadAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    private AssetRowViewModel? Focused()
    {
        return FocusKey is { } key ? Rows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase)) : null;
    }

    private int IndexOf(string key)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            if (string.Equals(Rows[index].Key, key, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private void Focus(AssetRowViewModel row)
    {
        FocusKey = row.Key;
        ChosenFolder = row.IsGiven ? "" : row.IsFolder ? row.Folder : row.Folder;

        if (ChosenId is { } id)
            Choose?.Invoke(id);

        Rebuild();
    }

    private void SelectOnly(string key, string folder)
    {
        _selected.Clear();
        _selected.Add(key);
        _anchor = key;
        FocusKey = key;
        ChosenFolder = folder;
    }

    // The rows an action acts on: those chosen when the row is one of them, or when none is given; else the row alone.
    private List<AssetRowViewModel> Targets(AssetRowViewModel? row)
    {
        if (row is not null && !_selected.Contains(row.Key))
            return [row];

        return [.. Rows.Where(item => _selected.Contains(item.Key))];
    }

    private string? PathOf(AssetRowViewModel row)
    {
        return row.Kind switch
        {
            AssetRowKind.Asset => _assets?.Find(row.Id ?? "")?.Path?.Replace(Path.DirectorySeparatorChar, '/'),
            AssetRowKind.Problem => Path.Combine(row.Folder, row.Name).Replace(Path.DirectorySeparatorChar, '/'),
            AssetRowKind.Folder => row.Folder.Replace(Path.DirectorySeparatorChar, '/'),
            _ => null,
        };
    }

    private async Task DeleteAsync(IReadOnlyList<AssetRowViewModel> targets)
    {
        if (_assets is null)
            return;

        await _writer.DeleteAsync(targets, _profiles, _book).ConfigureAwait(true);

        _selected.Clear();
        FocusKey = null;
        ChosenFolder = "";
        Rebuild();
    }

    // What was folded, chosen or cut under a folder's old place follows it to its new one.
    private void Follow(string from, string to)
    {
        static string Moved(string folder, string from, string to)
        {
            return AssetPaths.IsIn(folder, from) && from.Length > 0 ? to + folder[from.Length..] : folder;
        }

        foreach (var folded in _folded.Where(folder => AssetPaths.IsIn(folder, from)).ToList())
        {
            _folded.Remove(folded);
            _folded.Add(Moved(folded, from, to));
        }

        foreach (var key in _selected.Where(key => key.StartsWith(FolderKey, StringComparison.Ordinal) && AssetPaths.IsIn(key[FolderKey.Length..], from)).ToList())
        {
            _selected.Remove(key);
            _selected.Add(FolderKey + Moved(key[FolderKey.Length..], from, to));
        }

        if (FocusKey is { } focus && focus.StartsWith(FolderKey, StringComparison.Ordinal) && AssetPaths.IsIn(focus[FolderKey.Length..], from))
            FocusKey = FolderKey + Moved(focus[FolderKey.Length..], from, to);

        if (AssetPaths.IsIn(ChosenFolder, from) && ChosenFolder.Length > 0)
            ChosenFolder = Moved(ChosenFolder, from, to);

        RememberFolds();
    }

    private string FreeFolderName(string parent)
    {
        var taken = AssetPaths.AllFolders(_assets).Where(folder => string.Equals(AssetPaths.ParentOf(folder), parent, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = _localizer["NewFolder"];

        for (var attempt = 2; taken.Contains(name) && attempt < CopyAttempts; attempt++)
            name = $"{_localizer["NewFolder"]} {attempt}";

        return name;
    }

    private void RememberFolds()
    {
        FoldsChanged?.Invoke(_folded);
    }

    private void Rebuild()
    {
        _shown = "";
        Refresh(null);
    }

    private void Build()
    {
        var tree = _assets is { } assets
            ? new AssetTreeBuilder(_localizer, assets, _book, new AssetTreeState(Filter, _hidden, _folded, _givenFolded, _selected, _cut, FocusKey, IsUnsaved)).Build()
            : null;

        if (tree is not null)
        {
            IsProjectEmpty = tree.IsProjectEmpty;
            FindsNothing = tree.FindsNothing;
        }

        Rows.Clear();

        foreach (var row in tree?.Rows ?? [])
            Rows.Add(row);
    }

    partial void OnFilterChanged(string value)
    {
        Refresh(null);
    }
}
