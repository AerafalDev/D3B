namespace D3B.App.Controls.Sequencer;

/// <summary>A clip as the Sequencer draws it: an action across the steps it covers (D395, D396).</summary>
/// <param name="Id">The clip's place in the scene, which the control and the view model name it by.</param>
/// <param name="First">The first step it covers, from 0.</param>
/// <param name="Last">The last step it covers.</param>
/// <param name="Lane">The row of its track it stands on, from 0.</param>
/// <param name="Text">The action's sentence, written on the clip when it is wide enough.</param>
/// <param name="HasProblems">Whether a value of the action is missing or wrong.</param>
internal sealed record SequencerClip(int Id, int First, int Last, int Lane, string Text, bool HasProblems);
