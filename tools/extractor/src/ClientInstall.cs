namespace D3B.Extractor;

/// <summary>The installed official client, as the Ankama launcher lays it out.</summary>
internal sealed class ClientInstall
{
    /// <summary>The environment variable that names the client's folder, as studies also read it.</summary>
    public const string Variable = "DOFUS_CLIENT";

    /// <summary>Gets the installation folder, which holds <c>Dofus_Data</c>.</summary>
    public string Root { get; }

    /// <summary>Gets the folder that holds the version file and every bundle.</summary>
    public string StreamingAssets => Path.Combine(Root, "Dofus_Data", "StreamingAssets");

    private ClientInstall(string root)
    {
        Root = root;
    }

    /// <summary>
    /// Finds the client in <paramref name="folder"/>, else in the folder <see cref="Variable"/> names, else where the
    /// launcher installs it by default.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">No client is there.</exception>
    public static ClientInstall Locate(string? folder)
    {
        return Locate(folder, Environment.GetEnvironmentVariable);
    }

    /// <summary>Finds the client as <see cref="Locate(string?)"/> does, reading the environment through <paramref name="environment"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">No client is there.</exception>
    public static ClientInstall Locate(string? folder, Func<string, string?> environment)
    {
        var named = environment(Variable);
        var root = folder ?? (string.IsNullOrEmpty(named) ? DefaultRoot() : named);
        var client = new ClientInstall(Path.GetFullPath(root));

        if (!Directory.Exists(client.StreamingAssets))
        {
            throw new DirectoryNotFoundException($"No client in {client.Root}: {client.StreamingAssets} does not exist.");
        }

        return client;
    }

    private static string DefaultRoot()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Where the macOS and Linux launchers install the client is not measured (docs/data.md): there it is named.
        if (!OperatingSystem.IsWindows() || localApplicationData.Length is 0)
        {
            throw new DirectoryNotFoundException($"Name the client's folder with --client or {Variable}.");
        }

        return Path.Combine(localApplicationData, "Ankama", "Dofus-dofus3");
    }
}
