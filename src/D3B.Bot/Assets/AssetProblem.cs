namespace D3B.Bot.Assets;

/// <summary>A file of the project that was not read, and why.</summary>
/// <param name="Path">The file, relative to the project's folder.</param>
/// <param name="Message">Why, in English, for the logs and the problems' panel.</param>
public sealed record AssetProblem(string Path, string Message);
