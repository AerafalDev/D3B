namespace D3B.App.ViewModels.Studio;

/// <summary>A group of the search's results under its heading, and how many more Tab shows by its scope (D328).</summary>
/// <param name="Title">The group's heading.</param>
/// <param name="Results">Its results.</param>
/// <param name="More">What it says of the results not shown, else empty.</param>
internal sealed record SearchGroup(string Title, IReadOnlyList<SearchResult> Results, string More)
{
    /// <summary>Gets whether some results are not shown.</summary>
    public bool HasMore => More.Length > 0;
}
