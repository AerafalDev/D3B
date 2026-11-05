namespace D3B.Bot.Routes;

/// <summary>What lies between two anchors of a route (D299).</summary>
public enum RouteLegKind
{
    /// <summary>The world's path leads from one to the other, through the maps between, none when they are neighbours.</summary>
    Path,

    /// <summary>The two anchors are one map: there is nothing to cross.</summary>
    Same,

    /// <summary>No path leads from one to the other, which the editor shows in red.</summary>
    Unreachable,
}
