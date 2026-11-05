using System.Text.Json.Nodes;

namespace D3B.Bot.Assets;

/// <summary>An asset of a kind a later session of step 7 makes: what its file holds, kept as it is (D306).</summary>
/// <remarks>So that a project made by a later version of D3B, or by hand, loses nothing the app does not read yet.</remarks>
public sealed record PlannedAsset : Asset
{
    private readonly AssetKind _kind;

    /// <summary>Gets what the file holds but its id, kind, format and client build.</summary>
    public JsonObject Content { get; init; } = [];

    /// <inheritdoc/>
    public override AssetKind Kind => _kind;

    /// <summary>Initializes a new instance of the <see cref="PlannedAsset"/> class.</summary>
    /// <param name="kind">What the asset is: any kind but a behavior, a profile or a route.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is a behavior, a profile or a route.</exception>
    public PlannedAsset(AssetKind kind)
    {
        if (kind is AssetKind.Behavior or AssetKind.Profile or AssetKind.Route)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Behaviors, profiles and routes have their own records.");

        _kind = kind;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> Uses()
    {
        return [];
    }
}
