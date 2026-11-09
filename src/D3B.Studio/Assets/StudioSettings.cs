namespace D3B.Studio.Assets;

/// <summary>Where the project's assets are: a folder, <c>%LOCALAPPDATA%\D3B\studio</c> by default, or <c>Studio:Path</c> (D296).</summary>
public sealed class StudioSettings
{
    /// <summary>The configuration's key that names the project's folder.</summary>
    public const string PathKey = "Studio:Path";

    /// <summary>The configuration's key that sends what the studio deletes to the system's recycle bin.</summary>
    public const string RecycleKey = "Studio:Recycle";

    /// <summary>The configuration's key that has the project read again as its folder changes beside D3B.</summary>
    public const string WatchKey = "Studio:Watch";

    /// <summary>Gets the project's folder by default.</summary>
    public static string DefaultPath { get; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "studio");

    /// <summary>Gets the project's folder, whole.</summary>
    public required string Path { get; init; }

    /// <summary>Gets whether a file or folder deleted goes to the system's recycle bin, on Windows, rather than gone: the app's choice, never the tests'.</summary>
    public bool Recycles { get; init; }

    /// <summary>Gets whether the project is read again as its folder changes beside D3B, a file added or deleted in the file manager.</summary>
    public bool Watches { get; init; }
}
