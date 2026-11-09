namespace D3B.App.ViewModels.Studio;

/// <summary>A panel of the studio that closes by its × and opens again from the Window menu (D321).</summary>
internal enum StudioPanel
{
    /// <summary>The Outliner, at the top of the left column: what the world holds, characters, routes and zones (D397).</summary>
    Outliner,

    /// <summary>The project's assets, the Content browser, under the Outliner.</summary>
    Assets,

    /// <summary>The bottom panel, under the editor: the journal, the trace, the problems, the protocol and the statistics.</summary>
    Bottom,

    /// <summary>The Details panel, the whole height of the right: what is selected, its properties (D397).</summary>
    Details,
}
