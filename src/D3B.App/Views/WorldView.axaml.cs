using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.World;

namespace D3B.App.Views;

/// <summary>The studio's centre: the world's map, persistent, with the toolbar of its tools floating over it (D395, D397).</summary>
/// <remarks>
/// It hands what the map reports to the viewport's model, which gives it to the route or the zone selected, and the model's
/// wishes, such as showing a step, back to the map. The keys are those of the route editor (D375) and the zone editor
/// (D394), given to the tool in use.
/// </remarks>
internal sealed partial class WorldView : UserControl
{
    private const double ZoomStep = 1.25;
    private const double FastPan = 320;
    private const double SlowPan = 80;
    private const double ChoiceWidth = 270;
    private const double ChoiceHeight = 300;

    private WorldViewportViewModel? _world;
    private RouteEditorViewModel? _route;

    /// <summary>Initializes a new instance of the <see cref="WorldView"/> class.</summary>
    public WorldView()
    {
        InitializeComponent();
        Map.ViewChanged += OnViewChanged;
        Map.HitChanged += OnHitChanged;
        Map.SharedPressed += OnSharedPressed;
        Map.ObjectPressed += OnObjectPressed;
        Map.StationSelected += OnStationSelected;
        Map.StationMoved += OnStationMoved;
        Map.GestureStarted += OnGestureStarted;
        Map.GestureEnded += OnGestureEnded;
        Map.SquareTraced += OnSquareTraced;
        Map.PathPointPinned += OnPathPointPinned;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_world is { } old)
        {
            old.RevealRequested -= Map.CenterOn;
            old.FitRequested -= Map.FitScene;
            old.PropertyChanged -= OnWorldChanged;
        }

        FollowRoute(null);
        _world = DataContext as WorldViewportViewModel;

        if (_world is not { } world)
            return;

        world.RevealRequested += Map.CenterOn;
        world.FitRequested += Map.FitScene;
        world.PropertyChanged += OnWorldChanged;
        world.Zoomed(Map.ZoomPercent);
        FollowRoute(world.Route);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_world is not { } world)
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
            if (e.Key is Key.L && world.HasRoute)
            {
                world.SetTool(WorldTool.Route);
                FocusAdd();
            }
            else
            {
                e.Handled = false;
            }
        }
        else if (modifiers == KeyModifiers.Alt)
        {
            switch (e.Key)
            {
                case Key.Up:
                    world.MoveSelectedStep(-1);

                    break;
                case Key.Down:
                    world.MoveSelectedStep(1);

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
                    world.RemoveSelectedStep();

                    break;
                case Key.V:
                    world.SetTool(WorldTool.Select);

                    break;
                case Key.B:
                    world.SetTool(world.HasZone && !world.HasRoute ? WorldTool.Zone : WorldTool.Route);
                    world.SetZoneTool(WorldMapTool.Trace);

                    break;
                case Key.E when world.HasZone:
                    world.SetZoneTool(WorldMapTool.Erase);

                    break;
                case Key.R when world.HasZone:
                    world.SetZoneTool(WorldMapTool.Region);

                    break;
                case Key.F:
                    Map.FitScene();

                    break;
                case Key.Home:
                    Map.FitWorld();

                    break;
                case Key.Escape:
                    e.Handled = world.Escape();

                    break;
                case Key.PageUp when onMap:
                    world.StepBy(-1);

                    break;
                case Key.PageDown when onMap:
                    world.StepBy(1);

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

    private void FollowRoute(RouteEditorViewModel? route)
    {
        if (_route is { } old)
            old.PropertyChanged -= OnRouteChanged;

        _route = route;

        if (route is not null)
            route.PropertyChanged += OnRouteChanged;
    }

    private void OnWorldChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorldViewportViewModel.Route))
            FollowRoute(_world?.Route);
    }

    private void OnViewChanged()
    {
        _world?.Zoomed(Map.ZoomPercent);
    }

    private void OnHitChanged()
    {
        _world?.Hover(Map.Hit);
    }

    private void OnObjectPressed(WorldMapHit hit)
    {
        _world?.Press(hit);
    }

    private void OnStationSelected(int station, KeyModifiers modifiers)
    {
        _world?.SelectStation(station, modifiers);
    }

    private void OnStationMoved(int station, int x, int y)
    {
        _world?.MoveStation(station, x, y);
    }

    private void OnGestureStarted()
    {
        _world?.BeginGesture();
    }

    private void OnGestureEnded()
    {
        _world?.EndGesture();
    }

    private void OnSquareTraced(int x, int y, bool isDrag)
    {
        _world?.TraceSquare(x, y, isDrag);
    }

    private void OnPathPointPinned(int leg, int x, int y)
    {
        _world?.PinPathPoint(leg, x, y);
    }

    private void OnSharedPressed(int x, int y, Point at)
    {
        _world?.OpenChoice(x, y, at);
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
        // The world's own command is run, not the button's: choosing rebuilds the list of worlds, which takes the row out of the
        // menu, and closing the popup first takes the row out of the tree before its command runs.
        var world = (sender as Control)?.DataContext as WorldChoiceViewModel;

        FlyoutRows.Close(sender);
        world?.ChooseCommand.Execute(null);
    }
}
