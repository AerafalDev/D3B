namespace D3B.App.ViewModels.Assets;

/// <summary>The rows of the assets' tree, and what they say of the project.</summary>
/// <param name="Rows">The rows, in order.</param>
/// <param name="IsProjectEmpty">Whether the project holds nothing of its own.</param>
/// <param name="FindsNothing">Whether the search or the kinds shown leave nothing.</param>
internal sealed record AssetTree(IReadOnlyList<AssetRowViewModel> Rows, bool IsProjectEmpty, bool FindsNothing);
