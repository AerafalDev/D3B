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

    /// <summary>Tells whether rows may be moved to a folder: the project's own, never a folder into itself or where it is.</summary>
    /// <param name="assets">The project, if it is read.</param>
    /// <param name="keys">The rows' keys.</param>
    /// <param name="folder">The folder, empty for the project's root.</param>
    /// <returns>Whether at least one goes somewhere new.</returns>
    public static bool CanMove(AssetLibrary? assets, IReadOnlyCollection<string> keys, string folder)
    {
        return keys.Any(key => key.StartsWith(FolderKey, StringComparison.Ordinal)
            ? !IsIn(folder, key[FolderKey.Length..]) && !string.Equals(ParentOf(key[FolderKey.Length..]), folder, StringComparison.OrdinalIgnoreCase)
            : assets?.Find(key) is { IsGiven: false, Path: { } path } && !string.Equals(ParentOf(path), folder, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets a row's path, relative to the project and with slashes.</summary>
    /// <param name="assets">The project, if it is read.</param>
    /// <param name="row">The row.</param>
    /// <returns>The path, or <see langword="null"/> for a row that has none.</returns>
    public static string? RelativePathOf(AssetLibrary? assets, AssetRowViewModel row)
    {
        return row.Kind switch
        {
            AssetRowKind.Asset => assets?.Find(row.Id ?? "")?.Path?.Replace(Path.DirectorySeparatorChar, '/'),
            AssetRowKind.Problem => Path.Combine(row.Folder, row.Name).Replace(Path.DirectorySeparatorChar, '/'),
            AssetRowKind.Folder => row.Folder.Replace(Path.DirectorySeparatorChar, '/'),
            _ => null,
        };
    }

    /// <summary>Gets the file or folder on the disk a row stands for, to show it in the file manager.</summary>
    /// <param name="assets">The project.</param>
    /// <param name="row">The row, or <see langword="null"/> for the project's own folder.</param>
    /// <returns>The path, or <see langword="null"/> for an asset no file holds.</returns>
    public static string? OnDiskOf(AssetLibrary assets, AssetRowViewModel? row)
    {
        return row is null ? assets.Folder : row.Kind switch
        {
            AssetRowKind.Asset => assets.Find(row.Id ?? "") is { } asset ? assets.FileOf(asset) : null,
            AssetRowKind.Problem => Path.Combine(assets.Folder, row.Folder, row.Name),
            _ => Path.Combine(assets.Folder, row.Folder),
        };
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
