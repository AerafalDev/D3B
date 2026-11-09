using System.Globalization;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>The project's folders as the assets' tree reads them: paths relative to the project, with the system's separator.</summary>
internal static class AssetPaths
{
    /// <summary>The start of the key of a folder's row.</summary>
    public const string FolderKey = "folder:";

    /// <summary>The start of the key of a row for a file that does not read.</summary>
    public const string ProblemKey = "problem:";

    /// <summary>Gets the folder a folder or an asset's path is in.</summary>
    /// <param name="folder">The path.</param>
    /// <returns>The parent's path, empty for the project's root.</returns>
    public static string ParentOf(string folder)
    {
        return Path.GetDirectoryName(folder) ?? "";
    }

    /// <summary>Tells whether a folder is another, or inside it; every folder is inside the root.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="parent">The folder it may be in, empty for the root.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsIn(string folder, string parent)
    {
        return parent.Length == 0 || string.Equals(folder, parent, StringComparison.OrdinalIgnoreCase) || folder.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Gets every folder of the project, those the library lists and those its assets and files that do not read stand in.</summary>
    /// <param name="assets">The project, if it is read.</param>
    /// <returns>The folders, ordered by name.</returns>
    public static IEnumerable<string> AllFolders(AssetLibrary? assets)
    {
        if (assets is null)
            return [];

        var folders = new HashSet<string>(assets.Folders, StringComparer.OrdinalIgnoreCase);

        foreach (var path in assets.Assets.Select(asset => asset.Path).Concat(assets.Problems.Select(problem => problem.Path)).OfType<string>())
        {
            for (var folder = ParentOf(path); folder.Length > 0; folder = ParentOf(folder))
                folders.Add(folder);
        }

        return folders.Order(StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true));
    }
}
