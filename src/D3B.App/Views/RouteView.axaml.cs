using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Routes;

namespace D3B.App.Views;

/// <summary>A route's tab: the world's map filling the editor, with its tools floating over it (D371).</summary>
/// <remarks>It hands what the map reports to the route's model, and the model's wishes, such as showing a step, back to the map.</remarks>
internal sealed partial class RouteView : UserControl
{
    private const double ZoomStep = 1.25;
    private const double FastPan = 320;
    private const double SlowPan = 80;
    private const double ChoiceWidth = 270;
    private const double ChoiceHeight = 300;

    private RouteEditorViewModel? _route;

    /// <summary>Initializes a new instance of the <see cref="RouteView"/> class.</summary>
    public RouteView()
    {
        InitializeComponent();
        Map.ViewChanged += OnViewChanged;
        Map.HitChanged += OnHitChanged;
        Map.SharedPressed += OnSharedPressed;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_route is { } old)
        {
            old.RevealRequested -= Map.CenterOn;
            old.FitRequested -= Map.FitScene;
            old.PropertyChanged -= OnRouteChanged;
            Map.StationSelected -= old.SelectStation;
            Map.StationMoved -= old.MoveStation;
            Map.GestureStarted -= old.BeginGesture;
            Map.GestureEnded -= old.EndGesture;
            Map.SquareTraced -= old.TraceSquare;
            Map.PathPointPinned -= old.PinPathPoint;
        }

        _route = DataContext as RouteEditorViewModel;

        if (_route is not { } route)
            return;

        route.RevealRequested += Map.CenterOn;
        route.FitRequested += Map.FitScene;
        route.PropertyChanged += OnRouteChanged;
        Map.StationSelected += route.SelectStation;
        Map.StationMoved += route.MoveStation;
        Map.GestureStarted += route.BeginGesture;
        Map.GestureEnded += route.EndGesture;
        Map.SquareTraced += route.TraceSquare;
        Map.PathPointPinned += route.PinPathPoint;
        route.Zoomed(Map.ZoomPercent);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_route is not { } route)
            return;

        // Typing in the box is the box's; Escape gives the keys back to the map. Undo and Redo are the window's (Edit).
        if (e.Source is TextBox)
        {
            if (e.Key is Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                Map.Focus();
                e.Handled = true;
            }

            return;
        }

        var modifiers = e.KeyModifiers;
        var onMap = e.Source == Map;
        var step = modifiers.HasFlag(KeyModifiers.Shift) ? FastPan : SlowPan;

        e.Handled = true;

        if (modifiers == KeyModifiers.Control)
        {
            if (e.Key is Key.L)
                FocusAdd();
            else
                e.Handled = false;
        }
        else if (modifiers == KeyModifiers.Alt)
        {
            switch (e.Key)
            {
                case Key.Up:
                    route.MoveSelected(-1);

                    break;
                case Key.Down:
                    route.MoveSelected(1);

                    break;
                default:
                    e.Handled = false;

                    break;
            }
        }
        else if (e.Key is Key.Add or Key.OemPlus)
        {
            Map.ZoomBy(ZoomStep);
        }
        else if (e.Key is Key.Subtract or Key.OemMinus)
        {
            Map.ZoomBy(1 / ZoomStep);
        }
        else if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            e.Handled = false;
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete or Key.Back:
                    route.RemoveSelected();

                    break;
                case Key.V:
                    route.Tool = WorldMapTool.Select;

                    break;
                case Key.B:
                    route.Tool = WorldMapTool.Trace;

                    break;
                case Key.F:
                    Map.FitScene();

                    break;
                case Key.Home:
                    Map.FitWorld();

                    break;
                case Key.Escape when route.HasChoice:
                    route.CloseChoiceCommand.Execute(null);

                    break;
                case Key.Escape when route.Tool is WorldMapTool.Trace:
                    route.Tool = WorldMapTool.Select;

                    break;
                case Key.PageUp when onMap:
                    route.StepBy(-1);

                    break;
                case Key.PageDown when onMap:
                    route.StepBy(1);

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

    private void FocusAdd()
    {
        AddBox.Focus();
        AddBox.SelectAll();
    }

    private void OnViewChanged()
    {
        _route?.Zoomed(Map.ZoomPercent);
    }

    private void OnHitChanged()
    {
        _route?.Hover(Map.Hit);
    }

    private void OnSharedPressed(int x, int y, Point at)
    {
        _route?.OpenChoice(x, y, at);
    }

    private void OnRouteChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(RouteEditorViewModel.Choice) || _route?.Choice is not { } choice)
            return;

        // Near the point pressed, else under the toolbar; always inside the map.
        var x = choice.At is { } at ? at.X + 10 : (Bounds.Width - ChoiceWidth) / 2;
        var y = choice.At is { } pressed ? pressed.Y + 10 : 56;

        ChoicePopup.Margin = new Thickness(Math.Max(8, Math.Min(x, Bounds.Width - ChoiceWidth - 8)), Math.Max(8, Math.Min(y, Bounds.Height - ChoiceHeight - 8)), 0, 0);
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
        FlyoutRows.Close(sender);
    }
}
