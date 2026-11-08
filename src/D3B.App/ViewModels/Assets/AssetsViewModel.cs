using System.Collections.ObjectModel;
using System.Globalization;
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
    private const string FolderKey = "folder:";
    private const string ProblemKey = "problem:";

    private readonly Localizer _localizer;
    private readonly Dialogs _dialogs;
    private readonly Toasts _toasts;
    private readonly ProfilesViewModel _profiles;
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

    /// <summary>Gets or sets the lists of steps, the behaviors this page makes and opens (D386).</summary>
    public Sequences.SequencesViewModel? Sequences { get; set; }

    /// <summary>Gets or sets what remembers the folders folded, for the next start.</summary>
    public Action<IReadOnlyCollection<string>>? FoldsChanged { get; set; }

    /// <summary>Gets every folder of the project, relative to its own, folded ones included.</summary>
    public IReadOnlyList<string> AllFolderPaths => [.. AllFolders()];

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

        foreach (var folder in folded ?? [])
            _folded.Add(folder);
    }

    /// <summary>Tells whether an asset opens in a tab today: a profile or a route; the other kinds' editors come later (D306).</summary>
    /// <param name="row">The row.</param>
    /// <returns>Whether it opens.</returns>
    public static bool CanOpen(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.AssetKind is AssetKind.Profile or AssetKind.Route or AssetKind.Behavior;
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

    private static bool IsIn(string folder, string parent)
    {
        return parent.Length == 0 || string.Equals(folder, parent, StringComparison.OrdinalIgnoreCase) || folder.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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

        var unsaved = string.Join(',', _profiles.Open.Where(item => item.IsDirty).Select(item => item.Id).Concat(Routes?.Open.Where(item => item.IsDirty).Select(item => item.Id) ?? []));
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

        var parent = row.IsFolder ? ParentOf(row.Folder) : row.Folder;

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

        for (var folder = ParentOf(asset.Path ?? ""); folder.Length > 0; folder = ParentOf(folder))
            _folded.Remove(folder);

        Filter = "";
        _hidden.Clear();
        _selected.Clear();
        _selected.Add(id);
        _anchor = id;
        FocusKey = id;
        ChosenFolder = asset.IsGiven ? "" : ParentOf(asset.Path ?? "");
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

                await WriteAsync(assets => assets.CreateFolderAsync(made, CancellationToken.None)).ConfigureAwait(true);
                SelectOnly(FolderKey + made, made);
            }

            Rebuild();

            return;
        }

        if (row.IsFolder)
        {
            if (name.Length > 0 && name != row.Name)
                await RenameFolderAsync(row, name).ConfigureAwait(true);

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

        name = name.Trim();

        if (_assets?.Find(id) is not { IsGiven: false } asset || name.Length == 0 || name == asset.Name)
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
        }

        Rebuild();
    }

    /// <summary>Copies the assets chosen, of the project, beside each, under names of their own: Ctrl D.</summary>
    /// <param name="row">A row to copy, or <see langword="null"/> for those chosen.</param>
    /// <returns>A task that completes once written.</returns>
    public async Task DuplicateAsync(AssetRowViewModel? row = null)
    {
        foreach (var asset in Targets(row).Where(item => item.IsAsset && !item.IsGiven).Select(item => _assets?.Find(item.Id ?? "")).OfType<Asset>().ToList())
            await WriteAsync(assets => assets.SaveAsync(asset with { Id = AssetLibrary.NewId(), Name = CopyName(asset.Kind, asset.Name), Path = null }, CancellationToken.None, ParentOf(asset.Path ?? ""))).ConfigureAwait(true);
    }

    /// <summary>Copies an asset given with D3B into the folder chosen, which then changes it (D296).</summary>
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
            Locate(copy.Id);
        }
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

                if (folders.Any(other => !string.Equals(other, moved, StringComparison.OrdinalIgnoreCase) && IsIn(moved, other)))
                    continue;

                string? placed = null;

                await WriteAsync(async assets => placed = await assets.MoveFolderAsync(moved, folder, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);

                if (placed is not null)
                    Follow(moved, placed);
            }
            else if (_assets?.Find(key) is { Path: { } path } && !folders.Any(other => IsIn(ParentOf(path), other)))
            {
                await WriteAsync(assets => assets.MoveAsync(key, folder, CancellationToken.None)).ConfigureAwait(true);
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
            ? !IsIn(folder, key[FolderKey.Length..]) && !string.Equals(ParentOf(key[FolderKey.Length..]), folder, StringComparison.OrdinalIgnoreCase)
            : _assets?.Find(key) is { IsGiven: false, Path: { } path } && !string.Equals(ParentOf(path), folder, StringComparison.OrdinalIgnoreCase));
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

        return [.. AllFolders().Prepend("").Where(folder => CanMove([row.Key], folder)).Select(folder => (folder, folder.Length == 0 ? _localizer["ProjectRoot"] : folder.Replace(Path.DirectorySeparatorChar, '/')))];
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

        var imported = new List<Asset>();

        foreach (var file in files)
            await WriteAsync(async assets => imported.Add(await assets.ImportAsync(file, folder, CancellationToken.None).ConfigureAwait(true))).ConfigureAwait(true);

        if (imported.Count > 0)
            _toasts.Show(new Toast(imported.Count == 1 ? _localizer.Format("AssetImported", imported[0].Name) : _localizer.Plural("AssetsImported", imported.Count), Tone.Success));
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
        foreach (var folder in AllFolders())
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
        await WriteAsync(assets => assets.ReloadAsync(CancellationToken.None)).ConfigureAwait(true);
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

        var deleted = new List<Asset>();

        foreach (var target in targets.Where(item => item.IsFolder))
        {
            foreach (var profile in _assets.AssetsIn(target.Folder).OfType<ProfileAsset>())
                _profiles.Find(profile.Id)?.Cancel();

            await WriteAsync(async library => deleted.AddRange(await library.DeleteFolderAsync(target.Folder, CancellationToken.None).ConfigureAwait(true))).ConfigureAwait(true);
        }

        foreach (var target in targets.Where(item => item.IsAsset && _assets.Find(item.Id!) is not null))
        {
            var asset = _assets.Find(target.Id!)!;

            _profiles.Find(asset.Id)?.Cancel();
            await WriteAsync(library => library.DeleteAsync(asset.Id, CancellationToken.None)).ConfigureAwait(true);
            deleted.Add(asset);
        }

        // The characters that played a profile deleted are left without one, as deleting it alone leaves them (D99).
        if (_book is { } book)
        {
            foreach (var profile in deleted.OfType<ProfileAsset>())
                await book.LeaveProfileAsync(profile.Id, CancellationToken.None).ConfigureAwait(true);
        }

        _selected.Clear();
        FocusKey = null;
        ChosenFolder = "";
        Rebuild();
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

        if (renamed is not null)
            Follow(row.Folder, renamed);
    }

    // What was folded, chosen or cut under a folder's old place follows it to its new one.
    private void Follow(string from, string to)
    {
        static string Moved(string folder, string from, string to)
        {
            return IsIn(folder, from) && from.Length > 0 ? to + folder[from.Length..] : folder;
        }

        foreach (var folded in _folded.Where(folder => IsIn(folder, from)).ToList())
        {
            _folded.Remove(folded);
            _folded.Add(Moved(folded, from, to));
        }

        foreach (var key in _selected.Where(key => key.StartsWith(FolderKey, StringComparison.Ordinal) && IsIn(key[FolderKey.Length..], from)).ToList())
        {
            _selected.Remove(key);
            _selected.Add(FolderKey + Moved(key[FolderKey.Length..], from, to));
        }

        if (FocusKey is { } focus && focus.StartsWith(FolderKey, StringComparison.Ordinal) && IsIn(focus[FolderKey.Length..], from))
            FocusKey = FolderKey + Moved(focus[FolderKey.Length..], from, to);

        if (IsIn(ChosenFolder, from) && ChosenFolder.Length > 0)
            ChosenFolder = Moved(ChosenFolder, from, to);

        RememberFolds();
    }

    private string FreeFolderName(string parent)
    {
        var taken = AllFolders().Where(folder => string.Equals(ParentOf(folder), parent, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            var searching = filter.Length > 0 || _hidden.Count > 0;
            var folders = AllFolders().ToList();
            var own = assets.Assets.Where(asset => !asset.IsGiven).ToList();
            var given = assets.Assets.Where(asset => asset.IsGiven).ToList();

            AddFolder("", 0, folders, own, assets.Problems, filter, searching, rows);

            var givenRows = given.Select(asset => AssetRow(asset, 1, "", filter)).OfType<AssetRowViewModel>().ToList();

            if (givenRows.Count > 0)
            {
                var unfolded = !_givenFolded || searching;

                rows.Add(new AssetRowViewModel(AssetRowKind.Given, _localizer["GivenAssets"], 0) { Count = given.Count.ToString(_localizer.Culture), IsExpanded = unfolded });

                if (unfolded)
                    rows.AddRange(givenRows);
            }

            IsProjectEmpty = own.Count == 0 && folders.Count == 0 && assets.Problems.Count == 0;
            FindsNothing = searching && rows.All(row => !row.IsAsset && !row.IsProblem);
        }

        Rows.Clear();

        foreach (var row in rows)
            Rows.Add(row);
    }

    // A folder's rows, its own folders first, then its assets and the files that do not read; with a search or kinds
    // hidden, only what holds the text, its folders unfolded to it.
    private int AddFolder(string folder, int depth, List<string> folders, List<Asset> own, IReadOnlyList<AssetProblem> problems, string filter, bool searching, List<AssetRowViewModel> rows)
    {
        var count = 0;

        foreach (var child in folders.Where(other => string.Equals(ParentOf(other), folder, StringComparison.OrdinalIgnoreCase)))
        {
            var unfolded = searching || !_folded.Contains(child);
            var inner = new List<AssetRowViewModel>();
            var held = AddFolder(child, depth + 1, folders, own, problems, filter, searching, inner);
            var name = Path.GetFileName(child);

            if (searching && inner.Count == 0 && !(filter.Length > 0 && name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            var (before, found, after) = Split(name, filter);
            var key = FolderKey + child;

            rows.Add(new AssetRowViewModel(AssetRowKind.Folder, name, depth)
            {
                Key = key,
                Folder = child,
                Count = held > 0 ? held.ToString(_localizer.Culture) : "",
                CountTip = held > 0 ? _localizer.Plural("FolderHolds", held) : null,
                Tip = child.Replace(Path.DirectorySeparatorChar, '/'),
                IsExpanded = unfolded,
                IsSelected = _selected.Contains(key),
                IsFocused = string.Equals(FocusKey, key, StringComparison.OrdinalIgnoreCase),
                IsCut = _cut.Contains(key),
                Before = before,
                Found = found,
                After = after,
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
            var key = ProblemKey + problem.Path;

            rows.Add(new AssetRowViewModel(AssetRowKind.Problem, name, depth)
            {
                Key = key,
                Folder = folder,
                Problem = _localizer["AssetProblem" + problem.Kind],
                Tip = _localizer["AssetProblem" + problem.Kind],
                AssetKind = AssetFiles.TryKindOf(name, out var kind, out _) ? kind : null,
                IsSelected = _selected.Contains(key),
                IsFocused = string.Equals(FocusKey, key, StringComparison.OrdinalIgnoreCase),
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

        if (_hidden.Contains(asset.Kind) || (filter.Length > 0 && !name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
            return null;

        var users = _assets!.UsedBy(asset.Id).Count + (_book?.Characters.Count(character => character.Profile == asset.Id) ?? 0);
        var (before, found, after) = Split(name, filter);
        var kind = _localizer["AssetKind" + asset.Kind];

        return new AssetRowViewModel(AssetRowKind.Asset, name, depth)
        {
            Key = asset.Id,
            Id = asset.Id,
            AssetKind = asset.Kind,
            Folder = folder,
            IsGiven = asset.IsGiven,
            Count = users > 0 ? users.ToString(_localizer.Culture) : "",
            CountTip = users > 0 ? _localizer.Plural("UsedByCount", users) : null,
            Tip = asset.IsGiven ? $"{kind} · {_localizer["GivenAssets"]}" : $"{kind} · {asset.Path?.Replace(Path.DirectorySeparatorChar, '/')}",
            IsUnsaved = IsUnsaved?.Invoke(asset.Id) == true,
            IsSelected = _selected.Contains(asset.Id),
            IsFocused = string.Equals(FocusKey, asset.Id, StringComparison.OrdinalIgnoreCase),
            IsCut = _cut.Contains(asset.Id),
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

        Rebuild();
    }

    partial void OnFilterChanged(string value)
    {
        Refresh(null);
    }
}
