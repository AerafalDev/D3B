using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Zones;

namespace D3B.App.Views;

/// <summary>A zone's tab: the world's map filling the editor, with its tools floating over it (D394).</summary>
/// <remarks>It hands what the map reports to the zone's model, and the model's wishes, such as showing the zone whole, back to the map.</remarks>
internal sealed partial class ZoneView : UserControl
{
    private const double ZoomStep = 1.25;
    private const double FastPan = 320;
    private const double SlowPan = 80;

    private ZoneEditorViewModel? _zone;

    /// <summary>Initializes a new instance of the <see cref="ZoneView"/> class.</summary>
    public ZoneView()
    {
        InitializeComponent();
        Map.ViewChanged += OnViewChanged;
        Map.HitChanged += OnHitChanged;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_zone is { } old)
        {
            old.FitRequested -= Map.FitScene;
            Map.GestureStarted -= old.BeginGesture;
            Map.GestureEnded -= old.EndGesture;
            Map.SquareTraced -= old.TraceSquare;
        }

        _zone = DataContext as ZoneEditorViewModel;

        if (_zone is not { } zone)
            return;

        zone.FitRequested += Map.FitScene;
        Map.GestureStarted += zone.BeginGesture;
        Map.GestureEnded += zone.EndGesture;
        Map.SquareTraced += zone.TraceSquare;
        zone.Zoomed(Map.ZoomPercent);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_zone is not { } zone || e.Source is TextBox)
            return;

        var modifiers = e.KeyModifiers;
        var step = modifiers.HasFlag(KeyModifiers.Shift) ? FastPan : SlowPan;

        e.Handled = true;

        // Undo and Redo are the window's (Edit).
        if (e.Key is Key.Add or Key.OemPlus && modifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            Map.ZoomBy(ZoomStep);
        }
        else if (e.Key is Key.Subtract or Key.OemMinus && modifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            Map.ZoomBy(1 / ZoomStep);
        }
        else if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            e.Handled = false;
        }
        else
        {
            var onMap = e.Source == Map;

            switch (e.Key)
            {
                case Key.V:
                    zone.Tool = WorldMapTool.Select;

                    break;
                case Key.B:
                    zone.Tool = WorldMapTool.Trace;

                    break;
                case Key.E:
                    zone.Tool = WorldMapTool.Erase;

                    break;
                case Key.R:
                    zone.Tool = WorldMapTool.Region;

                    break;
                case Key.F:
                    Map.FitScene();

                    break;
                case Key.Home:
                    Map.FitWorld();

                    break;
                case Key.Escape:
                    zone.Tool = WorldMapTool.Select;

                    break;
                case Key.Left when onMap:
                    Map.PanBy(step, 0);

                    break;
                case Key.Right when onMap:
                    Map.PanBy(-step, 0);

                    break;
                case Key.Up when onMap:
                    Map.PanBy(0, step);

                    break;
                case Key.Down when onMap:
                    Map.PanBy(0, -step);

                    break;
                default:
                    e.Handled = false;

                    break;
            }
        }
    }

    private void OnViewChanged()
    {
        _zone?.Zoomed(Map.ZoomPercent);
    }

    private void OnHitChanged()
    {
        _zone?.Hover(Map.Hit);
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e)
    {
        Map.ZoomBy(ZoomStep);
    }

    private void OnZoomOut(object? sender, RoutedEventArgs e)
    {
        Map.ZoomBy(1 / ZoomStep);
    }

    private void OnFit(object? sender, RoutedEventArgs e)
    {
        Map.FitWorld();
    }

    private void OnWorldChosen(object? sender, RoutedEventArgs e)
    {
        // The world's own command is run, not the button's: choosing rebuilds the list of worlds, which takes the row out of the
        // menu, and closing the popup first takes the row out of the tree before its command runs.
        var world = (sender as Control)?.DataContext as WorldChoiceViewModel;

        FlyoutRows.Close(sender);
        world?.ChooseCommand.Execute(null);
    }
}
