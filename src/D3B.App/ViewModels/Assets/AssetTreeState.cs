using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>What the tree is made from besides the project: the search, the kinds hidden, the folds, the rows chosen, cut and focused.</summary>
/// <param name="Filter">The text the assets are searched by.</param>
/// <param name="Hidden">The kinds hidden.</param>
/// <param name="Folded">The folders folded.</param>
/// <param name="GivenFolded">Whether the assets given with D3B are folded.</param>
/// <param name="Selected">The keys of the rows chosen.</param>
/// <param name="Cut">The keys of the rows cut.</param>
/// <param name="FocusKey">The key of the row the keyboard is on.</param>
/// <param name="IsUnsaved">What tells whether an asset's tab has changes not saved.</param>
internal sealed record AssetTreeState(
    string Filter,
    IReadOnlySet<AssetKind> Hidden,
    IReadOnlySet<string> Folded,
    bool GivenFolded,
    IReadOnlySet<string> Selected,
    IReadOnlySet<string> Cut,
    string? FocusKey,
    Func<string, bool>? IsUnsaved);
