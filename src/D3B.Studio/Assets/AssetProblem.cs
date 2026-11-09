namespace D3B.Studio.Assets;

/// <summary>A file of the project that was not read, and why.</summary>
/// <param name="Path">The file, relative to the project's folder.</param>
/// <param name="Message">Why, in English, for the logs.</param>
public sealed record AssetProblem(string Path, string Message)
{
    /// <summary>Gets why, as the app says it in its language.</summary>
    public AssetProblemKind Kind { get; init; } = AssetProblemKind.Content;
}
