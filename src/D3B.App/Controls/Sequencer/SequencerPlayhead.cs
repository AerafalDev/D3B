using Avalonia.Media;

namespace D3B.App.Controls.Sequencer;

/// <summary>A character that follows the route, as the ruler marks it: the column of the step it goes to (D419).</summary>
/// <param name="Step">The step's place in the route, from 0.</param>
/// <param name="Initials">The letters of the marker.</param>
/// <param name="Color">The marker's colour, the character's breed's.</param>
/// <param name="Arrived">Whether the character is on the step's map, the column then filled; outlined while it walks there.</param>
internal sealed record SequencerPlayhead(int Step, string Initials, Color Color, bool Arrived);
