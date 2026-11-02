using D3B.Bot.Localization;

namespace D3B.Bot.Assets;

/// <summary>An asset of the project: one JSON file, referenced by a stable id apart from its name (D296).</summary>
/// <remarks>
/// The name is the file's, so that renaming breaks nothing that references the asset by its id. An asset given with
/// the app is read only, and its name is said in the app's language.
/// </remarks>
public abstract record Asset
{
    /// <summary>Gets the asset's id, which never changes once made.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the asset's name: its file's, without the extension.</summary>
    public required string Name { get; init; }

    /// <summary>Gets what the asset is.</summary>
    public abstract AssetKind Kind { get; }

    /// <summary>Gets the client build whose ids the asset's maps are, when it names maps.</summary>
    public string? Client { get; init; }

    /// <summary>Gets the asset's file, relative to the project's folder; none for an asset given with the app.</summary>
    public string? Path { get; init; }

    /// <summary>Gets the name of an asset given with the app, in the app's language; none for the project's own.</summary>
    public LocalizedText? Title { get; init; }

    /// <summary>Gets whether the asset was given with the app: read only, copied to be changed (D296).</summary>
    public bool IsGiven => Title is not null;

    /// <summary>Gets the ids of the assets this one references.</summary>
    /// <returns>The ids, each once.</returns>
    public abstract IReadOnlyList<string> Uses();
}
