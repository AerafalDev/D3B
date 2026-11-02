using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;

namespace D3B.App.ViewModels.Studio;

/// <summary>Search everywhere, Ctrl K: the recent items first, then the assets, characters, commands and settings, grouped, the text found lit (D328).</summary>
/// <remarks>
/// <para>
/// A scope is chosen by a click or Tab, or by the query's first sign, <c>&gt;</c> for the commands, <c>@</c> for the
/// characters; the arrows move through the results, Enter chooses, Escape closes. Looking everywhere, each group shows its
/// first few, the rest by its scope. While nothing is typed, everywhere shows the recent items, the assets and the
/// characters, and leaves the commands and settings to their scopes.
/// </para>
/// <para>
/// The maps by their coordinates or name wait for a tab that opens a map, a character's in 7c3 or the world's in 7d (D334).
/// </para>
/// </remarks>
internal sealed partial class SearchViewModel : ObservableObject
{
    private const int FewEverywhere = 4;
    private const int ManyInScope = 30;
    private const int RecentCount = 5;

    private readonly Localizer _localizer;
    private readonly Func<IEnumerable<SearchItem>> _items;
    private readonly List<string> _recent = [];
    private List<SearchResult> _results = [];

    /// <summary>Gets the scopes, in the order Tab goes through them.</summary>
    public IReadOnlyList<SearchScope> Scopes { get; } = Enum.GetValues<SearchScope>();

    /// <summary>Gets the groups of results.</summary>
    public ObservableCollection<SearchGroup> Groups { get; } = [];

    /// <summary>Gets whether the search shows, over the studio.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Gets or sets the text typed.</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    /// <summary>Gets or sets the scope chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownScope), nameof(Chips))]
    public partial SearchScope Scope { get; set; }

    /// <summary>Gets whether nothing matches what is typed, which the search says.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Gets what the search says when nothing matches.</summary>
    [ObservableProperty]
    public partial string EmptyText { get; private set; } = "";

    /// <summary>Gets the scope the results come from: the one chosen, or the one the query's first sign names.</summary>
    public SearchScope ShownScope => Prefixed(Query).Scope ?? Scope;

    /// <summary>Gets the scopes as the search's bar shows them, the one whose results show lit.</summary>
    public IReadOnlyList<SearchScopeChip> Chips => Scopes.Select(scope => new SearchScopeChip(scope, NameOf(scope), scope == ShownScope)).ToList();

    /// <summary>Initializes a new instance of the <see cref="SearchViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="items">Gives what the search may find, as it is when the search runs.</param>
    public SearchViewModel(Localizer localizer, Func<IEnumerable<SearchItem>> items)
    {
        _localizer = localizer;
        _items = items;
    }

    /// <summary>Gives the scope a scope's name says, in the app's language.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>Its name.</returns>
    public string NameOf(SearchScope scope)
    {
        return _localizer["SearchScope" + scope];
    }

    /// <summary>Opens the search, empty, on a scope.</summary>
    /// <param name="scope">The scope, everywhere by default.</param>
    [RelayCommand]
    public void Open(SearchScope scope = SearchScope.All)
    {
        Scope = scope;
        Query = "";
        IsOpen = true;
        Search();
    }

    /// <summary>Closes the search.</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>Chooses a scope, as a click on it does.</summary>
    /// <param name="scope">The scope.</param>
    [RelayCommand]
    public void Choose(SearchScope scope)
    {
        // A prefix names its own scope: chosen by a click instead, the prefix goes.
        Query = Prefixed(Query).Text;
        Scope = scope;
    }

    /// <summary>Goes to the next scope, or the previous, as Tab and Shift Tab do.</summary>
    /// <param name="forward">Whether to the next.</param>
    public void Cycle(bool forward)
    {
        var index = Array.IndexOf(Scopes.ToArray(), ShownScope);

        Choose(Scopes[(index + (forward ? 1 : Scopes.Count - 1)) % Scopes.Count]);
    }

    /// <summary>Moves the arrows' result down or up, staying within the results.</summary>
    /// <param name="by">How many lines, down when positive.</param>
    public void Move(int by)
    {
        if (_results.Count == 0)
            return;

        var active = _results.FindIndex(result => result.IsActive);

        Activate(Math.Clamp(active + by, 0, _results.Count - 1));
    }

    /// <summary>Puts the arrows on a result, as the pointer over it does.</summary>
    /// <param name="result">The result.</param>
    public void Activate(SearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Activate(result.Index);
    }

    /// <summary>Chooses the result the arrows are on, as Enter does.</summary>
    public void RunActive()
    {
        if (_results.FirstOrDefault(result => result.IsActive) is { } active)
            Run(active);
    }

    /// <summary>Chooses a result: the search closes, then it opens or plays.</summary>
    /// <param name="result">The result.</param>
    [RelayCommand]
    public void Run(SearchResult? result)
    {
        if (result is null)
            return;

        Close();

        // A command is not an item to come back to; an asset, a character or a setting is.
        if (result.Item.Scope is not SearchScope.Commands)
        {
            _recent.Remove(result.Item.Key);
            _recent.Insert(0, result.Item.Key);

            if (_recent.Count > RecentCount)
                _recent.RemoveAt(RecentCount);
        }

        result.Item.Run();
    }

    private static (SearchScope? Scope, string Text) Prefixed(string query)
    {
        var text = query.TrimStart();

        return text switch
        {
            ['>', .. var rest] => (SearchScope.Commands, rest.Trim()),
            ['@', .. var rest] => (SearchScope.Characters, rest.Trim()),
            _ => (null, text.Trim()),
        };
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(ShownScope));
        OnPropertyChanged(nameof(Chips));
        Search();
    }

    partial void OnScopeChanged(SearchScope value)
    {
        Search();
    }

    private void Search()
    {
        if (!IsOpen)
            return;

        var (prefixed, text) = Prefixed(Query);
        var scope = prefixed ?? Scope;
        var items = _items().ToList();
        var groups = new List<(string Title, List<SearchItem> Found, int Total)>();

        if (scope is SearchScope.All && text.Length == 0)
        {
            var recent = _recent.Select(key => items.FirstOrDefault(item => item.Key == key)).OfType<SearchItem>().ToList();

            if (recent.Count > 0)
                groups.Add((_localizer["SearchRecent"], recent, recent.Count));
        }

        foreach (var kind in Scopes.Where(kind => kind is not SearchScope.All && (scope == kind || scope is SearchScope.All)))
        {
            // Everywhere and with nothing typed, the commands and settings would only push the rest down.
            if (scope is SearchScope.All && text.Length == 0 && kind is SearchScope.Commands or SearchScope.Settings)
                continue;

            // The maps are thousands: they wait for a letter, whatever the scope.
            if (text.Length == 0 && kind is SearchScope.Maps)
                continue;

            var found = items.Where(item => item.Scope == kind && (text.Length == 0 || item.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase))).ToList();

            if (found.Count > 0)
                groups.Add((NameOf(kind), found.Take(scope is SearchScope.All ? FewEverywhere : ManyInScope).ToList(), found.Count));
        }

        var index = 0;

        _results = [];
        Groups.Clear();

        foreach (var (title, found, total) in groups)
        {
            var results = found.Select(item => new SearchResult(item, index++, text)).ToList();

            _results.AddRange(results);
            Groups.Add(new SearchGroup(title, results, total > found.Count ? _localizer.Format("SearchMore", total - found.Count) : ""));
        }

        IsEmpty = _results.Count == 0;
        EmptyText = text.Length > 0 ? _localizer.Format("SearchNothingFor", text) : _localizer["SearchNothing"];

        if (_results.Count > 0)
            Activate(0);
    }

    private void Activate(int index)
    {
        foreach (var result in _results)
            result.IsActive = result.Index == index;
    }
}
