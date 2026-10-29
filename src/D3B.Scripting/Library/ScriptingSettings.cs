namespace D3B.Scripting.Library;

/// <summary>Where the host finds its scripts (D108).</summary>
public sealed class ScriptingSettings
{
    /// <summary>The configuration key naming the scripts' directory.</summary>
    public const string DirectoryKey = "Scripting:Directory";

    /// <summary>Gets the default directory: <c>%LOCALAPPDATA%\D3B\scripts</c>, beside the database, the packs and the captures.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "scripts");

    /// <summary>Gets the directory of the scripts, each a <c>.lua</c> file, in it or in a folder of it.</summary>
    public required string Directory { get; init; }
}
