namespace D3B.App.ViewModels.Studio;

/// <summary>Where the run block's character stands, which says the buttons it shows (D322).</summary>
internal enum RunState
{
    /// <summary>No character chosen: play shows, disabled.</summary>
    None,

    /// <summary>The character runs nothing, or is not connected: play.</summary>
    Stopped,

    /// <summary>The character runs its activity, a fight included: pause and stop.</summary>
    Playing,

    /// <summary>The character's activity waits until it is resumed: play and stop.</summary>
    Paused,
}
