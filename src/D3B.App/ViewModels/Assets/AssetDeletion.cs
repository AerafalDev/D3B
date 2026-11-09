using D3B.App.Localization;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>Words the question asked before rows of the project are deleted.</summary>
internal static class AssetDeletion
{
    /// <summary>Says what deleting rows takes with it: the assets a folder holds, the users of an asset, and that it goes to the recycle bin.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="assets">The project, if it is read.</param>
    /// <param name="targets">The rows to delete.</param>
    /// <returns>The question's title and its detail.</returns>
    public static (string Title, string Detail) Describe(Localizer localizer, AssetLibrary? assets, IReadOnlyList<AssetRowViewModel> targets)
    {
        var held = targets.Where(item => item.IsFolder).Sum(item => assets?.AssetsIn(item.Folder).Count ?? 0);
        var title = targets is [var single]
            ? localizer.Format(single.IsFolder ? "DeleteFolderTitle" : "DeleteAssetTitle", single.Name)
            : localizer.Plural("DeleteItemsTitle", targets.Count);
        var users = targets.Where(item => item.IsAsset).Sum(item => assets?.UsedBy(item.Id!).Count ?? 0);
        var detail = string.Join(" ", new[]
        {
            held > 0 ? localizer.Plural("DeleteFolderAssets", held) : targets.All(item => item.IsFolder) ? localizer["DeleteFolderEmpty"] : null,
            users > 0 ? localizer.Plural("DeleteAssetUsers", users) : null,
            localizer["DeleteToRecycleBin"],
        }.OfType<string>());

        return (title, detail);
    }
}
