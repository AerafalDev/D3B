using D3B.App.Localization;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>Makes the rows of the assets' tree from the project and the state the panel keeps, without keeping any of its own (D326, D358).</summary>
internal sealed class AssetTreeBuilder
{
    private readonly Localizer _localizer;
    private readonly AssetLibrary _assets;
    private readonly AccountBook? _book;
    private readonly AssetTreeState _state;

    /// <summary>Initializes a new instance of the <see cref="AssetTreeBuilder"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="assets">The project.</param>
    /// <param name="book">The characters, whose profiles count among the users of an asset, if they are read.</param>
    /// <param name="state">The search, the folds and the rows chosen.</param>
    public AssetTreeBuilder(Localizer localizer, AssetLibrary assets, AccountBook? book, AssetTreeState state)
    {
        _localizer = localizer;
        _assets = assets;
        _book = book;
        _state = state;
    }

    private static (string Before, string Found, string After) Split(string name, string filter)
    {
        var at = filter.Length == 0 ? -1 : name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase);

        return at < 0 ? (name, "", "") : (name[..at], name.Substring(at, filter.Length), name[(at + filter.Length)..]);
    }

    /// <summary>Makes the rows: the project's folders with what they hold, then the assets given with D3B under their heading.</summary>
    /// <returns>The tree.</returns>
    public AssetTree Build()
    {
        var rows = new List<AssetRowViewModel>();
        var filter = _state.Filter.Trim();
        var searching = filter.Length > 0 || _state.Hidden.Count > 0;
        var folders = AssetPaths.AllFolders(_assets).ToList();
        var own = _assets.Assets.Where(asset => !asset.IsGiven).ToList();
        var given = _assets.Assets.Where(asset => asset.IsGiven).ToList();

        AddFolder("", 0, folders, own, _assets.Problems, filter, searching, rows);

        var givenRows = given.Select(asset => AssetRow(asset, 1, "", filter)).OfType<AssetRowViewModel>().ToList();

        if (givenRows.Count > 0)
        {
            var unfolded = !_state.GivenFolded || searching;

            rows.Add(new AssetRowViewModel(AssetRowKind.Given, _localizer["GivenAssets"], 0) { Count = given.Count.ToString(_localizer.Culture), IsExpanded = unfolded });

            if (unfolded)
                rows.AddRange(givenRows);
        }

        return new AssetTree(
            rows,
            IsProjectEmpty: own.Count == 0 && folders.Count == 0 && _assets.Problems.Count == 0,
            FindsNothing: searching && rows.All(row => !row.IsAsset && !row.IsProblem));
    }

    // A folder's rows, its own folders first, then its assets and the files that do not read; with a search or kinds
    // hidden, only what holds the text, its folders unfolded to it.
    private int AddFolder(string folder, int depth, List<string> folders, List<Asset> own, IReadOnlyList<AssetProblem> problems, string filter, bool searching, List<AssetRowViewModel> rows)
    {
        var count = 0;

        foreach (var child in folders.Where(other => string.Equals(AssetPaths.ParentOf(other), folder, StringComparison.OrdinalIgnoreCase)))
        {
            var unfolded = searching || !_state.Folded.Contains(child);
            var inner = new List<AssetRowViewModel>();
            var held = AddFolder(child, depth + 1, folders, own, problems, filter, searching, inner);
            var name = Path.GetFileName(child);

            if (searching && inner.Count == 0 && !(filter.Length > 0 && name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
                continue;

            var (before, found, after) = Split(name, filter);
            var key = AssetPaths.FolderKey + child;

            rows.Add(new AssetRowViewModel(AssetRowKind.Folder, name, depth)
            {
                Key = key,
                Folder = child,
                Count = held > 0 ? held.ToString(_localizer.Culture) : "",
                CountTip = held > 0 ? _localizer.Plural("FolderHolds", held) : null,
                Tip = child.Replace(Path.DirectorySeparatorChar, '/'),
                IsExpanded = unfolded,
                IsSelected = _state.Selected.Contains(key),
                IsFocused = string.Equals(_state.FocusKey, key, StringComparison.OrdinalIgnoreCase),
                IsCut = _state.Cut.Contains(key),
                Before = before,
                Found = found,
                After = after,
            });

            if (unfolded)
                rows.AddRange(inner);

            count += held;
        }

        foreach (var asset in own.Where(asset => string.Equals(AssetPaths.ParentOf(asset.Path ?? ""), folder, StringComparison.OrdinalIgnoreCase)))
        {
            count++;

            if (AssetRow(asset, depth, folder, filter) is { } row)
                rows.Add(row);
        }

        foreach (var problem in problems.Where(problem => string.Equals(AssetPaths.ParentOf(problem.Path), folder, StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileName(problem.Path);

            if (filter.Length > 0 && !name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                continue;

            var (before, found, after) = Split(name, filter);
            var key = AssetPaths.ProblemKey + problem.Path;

            rows.Add(new AssetRowViewModel(AssetRowKind.Problem, name, depth)
            {
                Key = key,
                Folder = folder,
                Problem = _localizer["AssetProblem" + problem.Kind],
                Tip = _localizer["AssetProblem" + problem.Kind],
                AssetKind = AssetFiles.TryKindOf(name, out var kind, out _) ? kind : null,
                IsSelected = _state.Selected.Contains(key),
                IsFocused = string.Equals(_state.FocusKey, key, StringComparison.OrdinalIgnoreCase),
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

        if (_state.Hidden.Contains(asset.Kind) || (filter.Length > 0 && !name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
            return null;

        var users = _assets.UsedBy(asset.Id).Count + (_book?.Characters.Count(character => character.Profile == asset.Id) ?? 0);
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
            IsUnsaved = _state.IsUnsaved?.Invoke(asset.Id) == true,
            IsSelected = _state.Selected.Contains(asset.Id),
            IsFocused = string.Equals(_state.FocusKey, asset.Id, StringComparison.OrdinalIgnoreCase),
            IsCut = _state.Cut.Contains(asset.Id),
            Before = before,
            Found = found,
            After = after,
        };
    }
}
