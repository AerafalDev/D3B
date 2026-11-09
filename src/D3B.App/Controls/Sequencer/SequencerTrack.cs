using Avalonia.Media;
using D3B.App.Controls.WorldMaps;

namespace D3B.App.Controls.Sequencer;

/// <summary>A track of the Sequencer: the clips of one kind of action, in as many lanes as they overlap (D395, D396).</summary>
/// <param name="Node">The action's node id.</param>
/// <param name="Name">The kind's name, in the app's language.</param>
/// <param name="Kind">The colour the kind is drawn in, as the map's dots are.</param>
/// <param name="Lanes">How many rows the track has, at least one.</param>
/// <param name="Clips">The clips.</param>
internal sealed record SequencerTrack(string Node, string Name, WorldMapActionKind Kind, int Lanes, IReadOnlyList<SequencerClip> Clips)
{
    /// <summary>Gets the colour of the track's kind.</summary>
    public IBrush Brush => new SolidColorBrush(WorldMapBackdropLayer.ColorOf(Kind));

    /// <summary>Gets the track's height, in the pixels of <see cref="SequencerLayout"/>.</summary>
    public double Height => (Lanes * SequencerLayout.LaneHeight) + SequencerLayout.TrackGap;
}
