namespace D3B.Bot.Assets;

/// <summary>Where the project's assets are: a folder, <c>%LOCALAPPDATA%\D3B\studio</c> by default, or <c>Studio:Path</c> (D296).</summary>
public sealed class StudioSettings
{
    /// <summary>The configuration's key that names the project's folder.</summary>
    public const string PathKey = "Studio:Path";

    /// <summary>Gets the project's folder by default.</summary>
    public static string DefaultPath { get; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "studio");

    /// <summary>Gets the project's folder, whole.</summary>
    public required string Path { get; init; }
}
