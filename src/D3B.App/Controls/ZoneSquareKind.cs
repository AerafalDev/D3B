namespace D3B.App.Controls;

/// <summary>What a map of a zone's mini-map is, which gives its colour (D127, D131).</summary>
internal enum ZoneSquareKind
{
    /// <summary>A map around the zone, outside it, drawn dim for its place.</summary>
    Context,

    /// <summary>A map of the zone's first part, such as its first sub-area, and of every other part but the second's.</summary>
    GroupA,

    /// <summary>A map of the zone's second part.</summary>
    GroupB,

    /// <summary>A map the tour went through.</summary>
    Visited,

    /// <summary>A map the tour still goes through.</summary>
    ToVisit,

    /// <summary>A map seen holding nothing the activity wants, which the tour leaves out.</summary>
    Empty,

    /// <summary>A map the way to failed.</summary>
    Unreachable,
}
