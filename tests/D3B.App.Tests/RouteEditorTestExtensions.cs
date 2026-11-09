using D3B.App.ViewModels.Routes;

namespace D3B.App.Tests;

/// <summary>What the tests of the route editor do to a step the way the user does it: draw a clip of the kind on it in the Sequencer.</summary>
internal static class RouteEditorTestExtensions
{
    /// <summary>Puts an action on the step chosen, as a press on its track's cell for that step would.</summary>
    /// <param name="editor">The route's editor, with a step chosen.</param>
    /// <param name="node">The action's node id.</param>
    public static void AddAction(this RouteEditorViewModel editor, string node)
    {
        var sequencer = editor.Sequencer;

        sequencer.AddTrack(node);

        var track = sequencer.Scene.Tracks.ToList().FindIndex(candidate => candidate.Node == node);

        sequencer.BeginDraw(track, editor.SelectedIndex);
        sequencer.EndGesture();
    }
}
