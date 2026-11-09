namespace D3B.App.ViewModels.Routes;

/// <summary>What a press held on the Sequencer does as the pointer moves (D395, D396).</summary>
internal enum SequencerGesture
{
    /// <summary>Nothing is held.</summary>
    None,

    /// <summary>A clip is drawn on an empty track from the step pressed.</summary>
    Draw,

    /// <summary>A clip's edge is stretched.</summary>
    Stretch,

    /// <summary>A clip is moved.</summary>
    Move,

    /// <summary>A clip is copied.</summary>
    Copy,
}
