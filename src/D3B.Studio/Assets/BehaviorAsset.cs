using System.Collections.Immutable;
using D3B.Studio.Behaviors;

namespace D3B.Studio.Assets;

/// <summary>A behavior: a layer Always and a stack of states by priority over short actions, with its public settings and variables (D297, D307).</summary>
public sealed record BehaviorAsset : Asset
{
    /// <summary>Gets its public settings, which its profiles change (D300).</summary>
    public ImmutableArray<BehaviorParameter> Parameters { get; init; } = [];

    /// <summary>Gets its variables (D298).</summary>
    public ImmutableArray<BehaviorVariable> Variables { get; init; } = [];

    /// <summary>Gets the layer Always: the states that keep the character safe, chosen before the others when they hold.</summary>
    public ImmutableArray<BehaviorState> Always { get; init; } = [];

    /// <summary>Gets its reactions to events, of the layer Always.</summary>
    public ImmutableArray<BehaviorRule> Rules { get; init; } = [];

    /// <summary>Gets its states, by priority: the first whose conditions hold is chosen.</summary>
    public ImmutableArray<BehaviorState> States { get; init; } = [];

    /// <inheritdoc/>
    public override AssetKind Kind => AssetKind.Behavior;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Uses()
    {
        // A behavior as a node of another, or a list as a value, comes with the sessions that make them (D306); the routes it follows are known (D386).
        return BehaviorSequences.RoutesOf(this);
    }
}
