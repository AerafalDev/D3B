using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.Studio;

/// <summary>A line of the search's results: what it found, the text found lit, and whether the arrows are on it (D328).</summary>
internal sealed partial class SearchResult : ObservableObject
{
    /// <summary>Gets what was found.</summary>
    public SearchItem Item { get; }

    /// <summary>Gets its position among every result, which the arrows move through.</summary>
    public int Index { get; }

    /// <summary>Gets the name before the text found.</summary>
    public string Before { get; }

    /// <summary>Gets the text found in the name, lit.</summary>
    public string Match { get; }

    /// <summary>Gets the name after the text found.</summary>
    public string After { get; }

    /// <summary>Gets whether the result has a shortcut, which shows instead of Enter.</summary>
    public bool HasKeys => Item.Keys.Length > 0;

    /// <summary>Gets or sets whether the arrows are on it: Enter chooses it.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SearchResult"/> class.</summary>
    /// <param name="item">What was found.</param>
    /// <param name="index">Its position among every result.</param>
    /// <param name="query">The text looked for, found in its name or not at all.</param>
    public SearchResult(SearchItem item, int index, string query)
    {
        Item = item;
        Index = index;

        var at = query.Length > 0 ? item.Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) : -1;

        (Before, Match, After) = at < 0 ? (item.Title, "", "") : (item.Title[..at], item.Title.Substring(at, query.Length), item.Title[(at + query.Length)..]);
    }
}
