namespace D3B.App.ViewModels.Studio;

/// <summary>A part of the studio that hides and shows again from the title row's toggles, the View menu or its keys (D321, D513).</summary>
internal enum StudioPanel
{
    /// <summary>The side bar, on the left beside the activity bar: the view chosen there, the characters, the library or the world.</summary>
    Side,

    /// <summary>The bottom panel, under the editor: the journal, the trace, the problems, the protocol and the statistics.</summary>
    Bottom,

    /// <summary>The Details panel, the whole height of the right: what is selected, its properties (D397).</summary>
    Details,
}
