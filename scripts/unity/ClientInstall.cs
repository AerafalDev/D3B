namespace D3B.Scripts.Unity;

/// <summary>The installed official client, as the Ankama launcher lays it out.</summary>
internal sealed class ClientInstall
{
    /// <summary>The installation directory, which holds the launcher's <c>manifest.json</c>.</summary>
    public string Root { get; }

    /// <summary>The directory that holds every bundle.</summary>
    public string StreamingAssets => Path.Combine(Root, "Dofus_Data", "StreamingAssets");

    private ClientInstall(string root)
    {
        Root = root;
    }

    /// <summary>Finds the client in <paramref name="directory"/>, or where the launcher installs it by default.</summary>
    /// <exception cref="DirectoryNotFoundException">No client is there.</exception>
    public static ClientInstall Locate(DirectoryInfo? directory)
    {
        var root = directory?.FullName;

        if (root is null)
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // The launcher installs the client for Windows only; elsewhere a copy must be named.
            if (!OperatingSystem.IsWindows() || localApplicationData.Length is 0)
                throw new DirectoryNotFoundException("Name the client's directory with --client.");

            root = Path.Combine(localApplicationData, "Ankama", "Dofus-dofus3");
        }

        var client = new ClientInstall(root);

        if (!Directory.Exists(client.StreamingAssets))
            throw new DirectoryNotFoundException($"No client in {root}: {client.StreamingAssets} does not exist.");

        return client;
    }

    /// <summary>The bundles of a directory under <c>StreamingAssets/Content</c>, in ordinal order of their paths.</summary>
    public IReadOnlyList<string> Bundles(string area, string pattern = "*.bundle")
    {
        var directory = Path.Combine(StreamingAssets, "Content", area);

        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, pattern).Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>The path of a file under the installation directory, with forward slashes, as the manifest names it.</summary>
    public string RelativePath(string fullPath)
    {
        return Path.GetRelativePath(Root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
