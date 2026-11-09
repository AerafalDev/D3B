namespace D3B.App.Controls.Sequencer;

/// <summary>Which part of a clip a press landed on (D395, D396).</summary>
internal enum SequencerEdge
{
    /// <summary>The body: a drag moves the clip, or copies it with Shift.</summary>
    None,

    /// <summary>The left edge: a drag changes the first step.</summary>
    Left,

    /// <summary>The right edge: a drag changes the last step.</summary>
    Right,
}
