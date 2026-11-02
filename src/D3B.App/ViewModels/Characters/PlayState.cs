namespace D3B.App.ViewModels.Characters;

/// <summary>Where a character stands, as its card's ring and tint say (D323).</summary>
internal enum PlayState
{
    /// <summary>Connected, running nothing: the ring dim.</summary>
    Stopped,

    /// <summary>Its activity running: the ring green.</summary>
    Playing,

    /// <summary>Its activity waiting to be resumed, the player's hand or a pause from the app: the ring amber.</summary>
    Paused,

    /// <summary>In a fight: the ring red and beating, the card tinted.</summary>
    Fighting,

    /// <summary>No game connection plays it: a line under the heading of those offline.</summary>
    Offline,
}
