namespace D3B.Hooks;

/// <summary>Finds the project a source file belongs to.</summary>
internal static class ProjectFile
{
    /// <summary>
    /// The <c>.csproj</c> nearest above <paramref name="path"/>, relative to <paramref name="root"/>, or
    /// <see langword="null"/> for a file no project owns, such as a script, which is a file-based app.
    /// </summary>
    public static string? Find(string root, string path)
    {
        var rootDirectory = Path.GetFullPath(root);
        var directory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(rootDirectory, path)));
        while (directory is not null && directory.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var project = Directory.EnumerateFiles(directory, "*.csproj").FirstOrDefault();
            if (project is not null)
            {
                return Path.GetRelativePath(rootDirectory, project).Replace('\\', '/');
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
