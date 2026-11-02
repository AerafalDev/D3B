using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace D3B.Bot.Assets;

/// <summary>A profile: a behavior and only the values it changes; the rest follows the behavior as it changes (D300).</summary>
public sealed record ProfileAsset : Asset
{
    /// <summary>Gets the id of the behavior the profile plays.</summary>
    public required string Behavior { get; init; }

    /// <summary>Gets the values the profile changes, by the behavior's public setting, as JSON.</summary>
    /// <remarks>A value no longer declared, or that no longer reads, is passed over: the behavior's own holds.</remarks>
    public ImmutableSortedDictionary<string, JsonNode?> Values { get; init; } = ImmutableSortedDictionary.Create<string, JsonNode?>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override AssetKind Kind => AssetKind.Profile;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Uses()
    {
        return [Behavior];
    }
}
