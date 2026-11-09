namespace D3B.App.Controls.Sequencer;

/// <summary>A step of the route as the Sequencer's ruler shows it (D395).</summary>
/// <param name="Number">The step's number, from 1.</param>
/// <param name="Place">The map's coordinates.</param>
/// <param name="Zone">The map's zone, which the movement row says.</param>
internal sealed record SequencerStep(string Number, string Place, string Zone);
