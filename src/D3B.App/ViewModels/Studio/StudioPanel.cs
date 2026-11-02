namespace D3B.App.ViewModels.Studio;

/// <summary>A panel of the studio that closes by its × and opens again from the Window menu (D321).</summary>
internal enum StudioPanel
{
    /// <summary>The characters, at the top of the left column.</summary>
    Characters,

    /// <summary>The project's assets, under the characters.</summary>
    Assets,

    /// <summary>The bottom panel, under the editor: the journal, the trace, the problems, the protocol and the statistics.</summary>
    Bottom,

    /// <summary>The inspector, the whole height of the right.</summary>
    Inspector,
}
