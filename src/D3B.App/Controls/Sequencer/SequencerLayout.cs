namespace D3B.App.Controls.Sequencer;

/// <summary>The Sequencer's measures, in pixels, which the grid and the track names beside it share so that their rows line up (D395).</summary>
internal static class SequencerLayout
{
    /// <summary>The ruler's height: a step's number over its coordinates.</summary>
    public const double RulerHeight = 44;

    /// <summary>The height of the movement row, which says each step's zone.</summary>
    public const double MovementHeight = 30;

    /// <summary>The height of a lane of a track.</summary>
    public const double LaneHeight = 28;

    /// <summary>The space under the lanes of a track.</summary>
    public const double TrackGap = 6;

    /// <summary>The narrowest a step's column is drawn, so that a long route scrolls instead of crushing.</summary>
    public const double MinColumn = 56;

    /// <summary>The width of the names' column.</summary>
    public const double HeaderWidth = 132;
}
