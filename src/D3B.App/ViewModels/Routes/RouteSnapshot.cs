using System.Collections.Immutable;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Routes;

/// <summary>The route as the editor can take it back: its anchors and how it goes through them.</summary>
/// <param name="Anchors">The anchors, in order.</param>
/// <param name="Mode">How the route goes through them.</param>
/// <param name="Unreachable">What it does with a map no path reaches.</param>
internal readonly record struct RouteSnapshot(ImmutableArray<RouteAnchor> Anchors, RouteMode Mode, RouteUnreachable Unreachable)
{
    /// <summary>Gets whether another snapshot holds the same anchors, in the same order, and the same course.</summary>
    /// <param name="other">The other snapshot.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    public bool Equals(RouteSnapshot other)
    {
        return Mode == other.Mode && Unreachable == other.Unreachable && Anchors.AsSpan().SequenceEqual(other.Anchors.AsSpan());
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Anchors.Length, Mode, Unreachable);
    }
}
