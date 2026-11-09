using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    }

    private void OnStepPressed(int step, KeyModifiers modifiers)
    {
        if (DataContext is SequencerViewModel sequencer)
            sequencer.PressStep(step, modifiers);
    }

    private void OnClipPressed(int clip)
    {
        if (DataContext is SequencerViewModel sequencer)
            sequencer.PressClip(clip);
    }

    private void OnMenuRowClick(object? sender, RoutedEventArgs e)
    {
        FlyoutRows.Close(sender);
    }
}
