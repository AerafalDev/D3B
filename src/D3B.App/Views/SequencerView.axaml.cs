using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Controls.Sequencer;
using D3B.App.ViewModels.Routes;

namespace D3B.App.Views;

/// <summary>The Sequencer: a route's steps on a ruler, a track for each kind of action, a clip across the steps it covers (D395, D396).</summary>
internal sealed partial class SequencerView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SequencerView"/> class.</summary>
    public SequencerView()
    {
        InitializeComponent();
        Grid.StepPressed += OnStepPressed;
        Grid.ClipPressed += OnClipPressed;
        Grid.TrackPressed += OnTrackPressed;
        Grid.GestureMoved += OnGestureMoved;
        Grid.GestureEnded += OnGestureEnded;
        Grid.ColumnHovered += OnColumnHovered;
        DataContextChanged += OnDataContextChanged;
        Grid.KeyDown += OnGridKeyDown;
    }

    private void OnStepPressed(int step, KeyModifiers modifiers)
    {
        if (DataContext is SequencerViewModel sequencer)
            sequencer.PressStep(step, modifiers);
    }

    private void OnClipPressed(int clip, SequencerEdge edge, int step, KeyModifiers modifiers)
    {
        if (DataContext is SequencerViewModel sequencer)
            sequencer.BeginClip(clip, edge, step, modifiers);
    }

    private void OnTrackPressed(int track, int step)
    {
        if (DataContext is SequencerViewModel sequencer)
        {
            sequencer.ClearClip();
            sequencer.BeginDraw(track, step);
        }
    }

    private void OnGestureMoved(int step)
    {
        (DataContext as SequencerViewModel)?.Drag(step);
    }

    private void OnGestureEnded()
    {
        (DataContext as SequencerViewModel)?.EndGesture();
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is SequencerViewModel sequencer && sequencer.PressKey(e.Key, e.KeyModifiers))
            e.Handled = true;
    }

    private void OnColumnHovered(int step)
    {
        (DataContext as SequencerViewModel)?.Hover(step);
    }

    // The ruler scrolls to the playhead when it moves, so that a long route is followed without a hand; a column already in view stays put.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SequencerViewModel sequencer)
            sequencer.PropertyChanged += (_, args) => OnSequencerChanged(sequencer, args.PropertyName);
    }

    private void OnSequencerChanged(SequencerViewModel sequencer, string? property)
    {
        if (property is nameof(SequencerViewModel.Scene) && sequencer.KeepsInSight && sequencer.Scene.Playheads.Count > 0)
            Grid.BringIntoView(Grid.RectOfStep(sequencer.Scene.Playheads[0].Step));
    }

    private void OnMenuRowClick(object? sender, RoutedEventArgs e)
    {
        FlyoutRows.Close(sender);
    }
}
