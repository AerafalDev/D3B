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
        Map.SquareTraced += route.TraceSquare;
        Map.PathPointPinned += route.PinPathPoint;
        route.Zoomed(Map.ZoomPercent);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_route is not { } route || e.Source is TextBox || e.KeyModifiers != KeyModifiers.None)
            return;

        switch (e.Key)
        {
            case Key.Delete:
                route.RemoveSelected();
                e.Handled = true;

                break;
            case Key.V:
                route.Tool = WorldMapTool.Select;
                e.Handled = true;

                break;
            case Key.B:
                route.Tool = WorldMapTool.Trace;
                e.Handled = true;

                break;
            case Key.Escape when route.HasChoice:
                route.CloseChoiceCommand.Execute(null);
                e.Handled = true;

                break;
        }
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
