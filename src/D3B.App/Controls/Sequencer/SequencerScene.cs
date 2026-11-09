namespace D3B.App.Controls.Sequencer;

/// <summary>What the Sequencer draws: the route's steps and a track for each kind of action it holds (D395, D396).</summary>
/// <param name="Steps">The ruler's steps.</param>
/// <param name="Tracks">The tracks, under the ruler and the movement row.</param>
internal sealed record SequencerScene(IReadOnlyList<SequencerStep> Steps, IReadOnlyList<SequencerTrack> Tracks)
{
    /// <summary>Gets the scene of no route.</summary>
    public static SequencerScene Empty { get; } = new([], []);

    /// <summary>Gets the height of everything under the ruler.</summary>
    public double BodyHeight => SequencerLayout.MovementHeight + Tracks.Sum(track => track.Height);
}
