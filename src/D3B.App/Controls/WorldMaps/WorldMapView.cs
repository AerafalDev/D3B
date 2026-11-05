using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D3B.Data.WorldMaps;

namespace D3B.App.Controls.WorldMaps;

/// <summary>The world's map, drawn from the client's own images: zoom and pan, the route over it, and the maps pressed (D308, D371).</summary>
/// <remarks>
/// <para>
/// The image is cut into JPEG tiles at several zooms (D369). The view draws the zoom the scale asks for (<see cref="WorldMapCatalog.LevelFor"/>)
/// from the tiles it has, and loads the others on the pool's threads, decoded off the interface's thread and kept in a bounded
/// cache; under them it always draws the world at its coarsest zoom, kept whole, and the zoom drawn just before, so that a wheel turn
/// never shows a hole. The wheel eases the scale towards its target, one frame at a time, around the pointer.
/// </para>
/// <para>
/// It draws and tells what was pressed; it knows nothing of routes. The editor's model hears <see cref="StationSelected"/>,
/// <see cref="StationMoved"/>, <see cref="SquareTraced"/>, <see cref="PathPointPinned"/> and <see cref="SharedPressed"/>, and gives
/// back a <see cref="WorldMapScene"/>.
/// </para>
/// </remarks>
internal sealed class WorldMapView : Control
{
    private const long TileBudget = 192L * 1024 * 1024;
    private const int Workers = 3;
    private const double FitMargin = 28;
    private const double KeptOfImage = 160;
    private const double Prefetch = 256;
    private const double GridFrom = 14;
    private const double WheelStep = 1.22;
    private const double Ease = 14;
    private const double MaxSceneFit = 0.8;

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<WorldMapSource?> SourceProperty = AvaloniaProperty.Register<WorldMapView, WorldMapSource?>(nameof(Source));

    /// <summary>Defines the <see cref="World"/> property.</summary>
    public static readonly StyledProperty<int> WorldProperty = AvaloniaProperty.Register<WorldMapView, int>(nameof(World));

    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<WorldMapScene> SceneProperty = AvaloniaProperty.Register<WorldMapView, WorldMapScene>(nameof(Scene), WorldMapScene.Empty);

    /// <summary>Defines the <see cref="Tool"/> property.</summary>
    public static readonly StyledProperty<WorldMapTool> ToolProperty = AvaloniaProperty.Register<WorldMapView, WorldMapTool>(nameof(Tool));

    /// <summary>Defines the <see cref="ShowGrid"/> property.</summary>
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<WorldMapView, bool>(nameof(ShowGrid), true);

    /// <summary>Defines the <see cref="ShowShared"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSharedProperty = AvaloniaProperty.Register<WorldMapView, bool>(nameof(ShowShared), true);

    private static readonly IBrush _sea = new SolidColorBrush(Color.Parse("#0C1A1C"));
    private static readonly IPen _gridPen = new Pen(new SolidColorBrush(Colors.Black, 0.28), 1);
    private static readonly IPen _hoverPen = new Pen(new SolidColorBrush(Colors.White, 0.85), 2);
    private static readonly IPen _tracePen = new Pen(new SolidColorBrush(Color.Parse("#5FD3C0")), 2);
    private static readonly IBrush _badge = new SolidColorBrush(Color.Parse("#1B1B1F"));
    private static readonly IBrush _amber = new SolidColorBrush(Color.Parse("#E9AD3C"));

    private readonly WorldMapViewport _viewport = new();
    private WorldMapSource? _source;
    private TileStore<Bitmap>? _store;
    private WorldMap _world;
    private bool _hasWorld;
    private WorldMapLevel[] _levels = [];
    private WorldMapSquare[] _shared = [];
    private WorldMapLevel _fallback;
    private WorldMapLevel _drawn;
    private bool _needsFit = true;
    private int _repaintQueued;
    private WorldMapHit _hit = WorldMapHit.None;
    private Drag _drag;
    private Point _dragFrom;
    private int _dragStation = -1;
    private (int X, int Y) _lastSquare;
    private double _targetScale = 1;
    private Point _anchor;
    private bool _easing;
    private TimeSpan _lastFrame;

    /// <summary>Gets or sets where the maps and their tiles are read from.</summary>
    public WorldMapSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets the world shown, one layer at a time (D299).</summary>
    public int World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    /// <summary>Gets or sets the route drawn over the image.</summary>
    public WorldMapScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets what a press does.</summary>
    public WorldMapTool Tool
    {
        get => GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    /// <summary>Gets or sets whether the outline of each map shows.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Gets or sets whether the mark of coordinates that several maps share shows.</summary>
    public bool ShowShared
    {
        get => GetValue(ShowSharedProperty);
        set => SetValue(ShowSharedProperty, value);
    }

    /// <summary>Gets what lies under the pointer.</summary>
    public WorldMapHit Hit => _hit;

    /// <summary>Gets the zoom, as a percentage of the client's full size.</summary>
    public double ZoomPercent => _viewport.Scale * 100;

    static WorldMapView()
    {
        AffectsRender<WorldMapView>(SceneProperty, ShowGridProperty, ShowSharedProperty, ToolProperty);
        ClipToBoundsProperty.OverrideDefaultValue<WorldMapView>(true);
        FocusableProperty.OverrideDefaultValue<WorldMapView>(true);
    }

    /// <summary>Initializes a new instance of the <see cref="WorldMapView"/> class.</summary>
    public WorldMapView()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    /// <summary>Raised when the zoom or the place of the image changed.</summary>
    public event Action? ViewChanged;

    /// <summary>Raised when what lies under the pointer changed.</summary>
    public event Action? HitChanged;

    /// <summary>Raised when a station is pressed, with its place in the scene.</summary>
    public event Action<int>? StationSelected;

    /// <summary>Raised when a station is dragged onto other coordinates, with its place in the scene and the coordinates.</summary>
    public event Action<int, int, int>? StationMoved;

    /// <summary>Raised when a station has been dragged and let go.</summary>
    public event Action? StationMoveEnded;

    /// <summary>Raised by the trace tool, on the coordinates pressed and on each one a drag crosses, which the last argument says.</summary>
    public event Action<int, int, bool>? SquareTraced;

    /// <summary>Raised when a point of the path is pressed, with its leg's place and the coordinates.</summary>
    public event Action<int, int, int>? PathPointPinned;

    /// <summary>Raised when the mark of shared coordinates is pressed, with the coordinates and the point pressed.</summary>
    public event Action<int, int, Point>? SharedPressed;

    /// <summary>Zooms around the middle of the view, by a factor.</summary>
    /// <param name="factor">More than 1 zooms in.</param>
    public void ZoomBy(double factor)
    {
        ZoomTowards(new Point(_viewport.Width / 2, _viewport.Height / 2), _targetScale * factor);
    }

    /// <summary>Shows the whole image.</summary>
    public void FitWorld()
    {
        if (!_hasWorld)
            return;

        _viewport.Fit(0, 0, _world.Width, _world.Height, FitMargin);
        Settle();
    }

    /// <summary>Shows the route whole, else the whole image.</summary>
    public void FitScene()
    {
        if (!_hasWorld)
            return;

        var stations = Scene.Stations;

        if (stations.Count == 0)
        {
            FitWorld();

            return;
        }

        var left = stations.Min(station => _world.PixelOf(station.X, station.Y).X);
        var top = stations.Min(station => _world.PixelOf(station.X, station.Y).Y);
        var right = stations.Max(station => _world.PixelOf(station.X, station.Y).X) + _world.MapWidth;
        var bottom = stations.Max(station => _world.PixelOf(station.X, station.Y).Y) + _world.MapHeight;
        var pad = 2 * Math.Max(_world.MapWidth, _world.MapHeight);

        _viewport.Fit(left - pad, top - pad, right - left + (2 * pad), bottom - top + (2 * pad), FitMargin);

        if (_viewport.Scale > MaxSceneFit)
            _viewport.ZoomAt(_viewport.Width / 2, _viewport.Height / 2, MaxSceneFit);

        Settle();
    }

    /// <summary>Puts the map at coordinates in the middle of the view, at the same zoom.</summary>
    /// <param name="x">The map's horizontal coordinate.</param>
    /// <param name="y">The map's vertical coordinate.</param>
    public void CenterOn(int x, int y)
    {
        if (!_hasWorld)
            return;

        var (px, py) = _world.PixelOf(x, y);
        var (vx, vy) = _viewport.ToView(px + (_world.MapWidth / 2), py + (_world.MapHeight / 2));

        _viewport.Pan((_viewport.Width / 2) - vx, (_viewport.Height / 2) - vy);
        Settle();
    }

    /// <summary>Gets where the map at coordinates lies in the view.</summary>
    /// <param name="x">The map's horizontal coordinate.</param>
    /// <param name="y">The map's vertical coordinate.</param>
    /// <returns>Its rectangle.</returns>
    public Rect RectOf(int x, int y)
    {
        var (px, py) = _world.PixelOf(x, y);
        var (vx, vy) = _viewport.ToView(px, py);

        return new Rect(vx, vy, _world.MapWidth * _viewport.Scale, _world.MapHeight * _viewport.Scale);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(_sea, new Rect(Bounds.Size));

        if (_source is null || !_hasWorld || _viewport.Width < 1)
            return;

        DrawImage(context);

        var mapWidth = _world.MapWidth * _viewport.Scale;
        var mapHeight = _world.MapHeight * _viewport.Scale;

        DrawGrid(context, mapWidth);
        DrawSharedMarks(context, mapWidth);

        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.Medium);

        WorldMapRouteLayer.Draw(context, Scene, mapWidth, mapHeight, RectOf, _hit, typeface);

        if (_hit.Kind is WorldMapHitKind.Square or WorldMapHitKind.Shared)
            context.DrawRectangle(null, Tool is WorldMapTool.Trace ? _tracePen : _hoverPen, RectOf(_hit.X, _hit.Y).Deflate(1));
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
            UseSource(Source);
        else if (change.Property == WorldProperty)
            UseWorld(refit: true);
        else if (change.Property == ToolProperty)
            Cursor = null;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);

        _viewport.Resize(arranged.Width, arranged.Height);

        if (_needsFit && _hasWorld && arranged.Width > 1 && arranged.Height > 1)
        {
            _needsFit = false;
            FitScene();
        }

        return arranged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _store?.Dispose();
        _store = null;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_source is not null && _store is null)
            _store = MakeStore(_source);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        var at = point.Position;

        Focus();

        if (point.Properties.IsMiddleButtonPressed || (point.Properties.IsLeftButtonPressed && !_hasWorld))
        {
            StartDrag(e, Drag.Pan, at);

            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
            return;

        var hit = HitAt(at);

        SetHit(hit);

        switch (hit.Kind)
        {
            case WorldMapHitKind.Station:
                StationSelected?.Invoke(hit.Station);
                _dragStation = hit.Station;
                StartDrag(e, Drag.Move, at);

                break;
            case WorldMapHitKind.PathPoint when Tool is WorldMapTool.Select:
                PathPointPinned?.Invoke(hit.Leg, hit.X, hit.Y);

                break;
            case WorldMapHitKind.Shared:
                SharedPressed?.Invoke(hit.X, hit.Y, at);

                break;
            case WorldMapHitKind.Square or WorldMapHitKind.PathPoint when Tool is WorldMapTool.Trace:
                _lastSquare = (hit.X, hit.Y);
                SquareTraced?.Invoke(hit.X, hit.Y, false);
                StartDrag(e, Drag.Paint, at);

                break;
            default:
                StartDrag(e, Drag.Pan, at);

                break;
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var at = e.GetPosition(this);

        switch (_drag)
        {
            case Drag.Pan:
                _viewport.Pan(at.X - _dragFrom.X, at.Y - _dragFrom.Y);
                _dragFrom = at;
                KeepInView();
                Changed();

                return;
            case Drag.Paint:
                SetHit(HitAt(at));

                if (_hit.Kind is WorldMapHitKind.Square or WorldMapHitKind.PathPoint && (_hit.X, _hit.Y) != _lastSquare)
                {
                    _lastSquare = (_hit.X, _hit.Y);
                    SquareTraced?.Invoke(_hit.X, _hit.Y, true);
                }

                return;
            case Drag.Move:
                var square = SquareAt(at);

                if (square is { } over && _dragStation >= 0 && (over.X, over.Y) != _lastSquare && _dragStation < Scene.Stations.Count)
                {
                    var station = Scene.Stations[_dragStation];

                    if ((station.X, station.Y) != (over.X, over.Y))
                    {
                        _lastSquare = (over.X, over.Y);
                        StationMoved?.Invoke(_dragStation, over.X, over.Y);
                    }
                }

                SetHit(HitAt(at));

                return;
        }

        SetHit(HitAt(at));
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_drag is Drag.Move)
            StationMoveEnded?.Invoke();

        _drag = Drag.None;
        _dragStation = -1;
        e.Pointer.Capture(null);
        Cursor = null;
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_drag is Drag.None)
            SetHit(WorldMapHit.None);
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (!_hasWorld)
            return;

        ZoomTowards(e.GetPosition(this), _targetScale * Math.Pow(WheelStep, e.Delta.Y));
        e.Handled = true;
    }

    private static long CostOf(Bitmap tile)
    {
        return 4L * tile.PixelSize.Width * tile.PixelSize.Height;
    }

    private static void Release(Bitmap tile)
    {
        // Later than the frame that may still draw it.
        Dispatcher.UIThread.Post(tile.Dispose, DispatcherPriority.Background);
    }

    private static Bitmap? Decode(WorldMapSource source, WorldMapTileKey key)
    {
        using var stream = new MemoryStream(source.ReadTile(key.World, key.ZoomPercent, key.Index), writable: false);

        return new Bitmap(stream);
    }


    private static double Distance(WorldMapLevel level, int index, double x, double y)
    {
        var (px, py, width, height) = WorldMapTiling.Place(level, index);
        var dx = px + (width / 2) - x;
        var dy = py + (height / 2) - y;

        return (dx * dx) + (dy * dy);
    }

    private TileStore<Bitmap> MakeStore(WorldMapSource source)
    {
        var store = new TileStore<Bitmap>(key => Decode(source, key), CostOf, Release, TileBudget, Workers);

        store.Loaded += QueueRepaint;

        return store;
    }

    private void QueueRepaint()
    {
        if (Interlocked.Exchange(ref _repaintQueued, 1) == 0)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    Volatile.Write(ref _repaintQueued, 0);
                    InvalidateVisual();
                },
                DispatcherPriority.Render);
        }
    }

    private void UseSource(WorldMapSource? source)
    {
        _store?.Dispose();
        _store = null;
        _source = source;

        if (source is not null && TopLevel.GetTopLevel(this) is not null)
            _store = MakeStore(source);

        UseWorld(refit: true);
    }

    private void UseWorld(bool refit)
    {
        _hasWorld = _source is not null && _source.Catalog.TryGet(World, out _world);
        _levels = _hasWorld ? [.. _source!.Catalog.LevelsOf(_world).ToArray()] : [];
        _shared = _hasWorld ? [.. _source!.Index.SquaresOf(World).ToArray().Where(square => square.IsShared)] : [];
        _fallback = default;
        _drawn = default;
        _hit = WorldMapHit.None;
        _needsFit = refit;

        if (refit && _hasWorld && _viewport.Width > 1 && _viewport.Height > 1)
        {
            _needsFit = false;
            FitScene();
        }

        InvalidateVisual();
    }

    private void DrawImage(DrawingContext context)
    {
        var catalog = _source!.Catalog;
        var coarsest = _levels[^1];
        var desired = catalog.LevelFor(_world, _viewport.Scale);

        if (_drawn.ZoomPercent != 0 && _drawn.ZoomPercent != desired.ZoomPercent)
            _fallback = _drawn;

        _drawn = desired;

        var (left, top, right, bottom) = _viewport.VisibleImage();
        var ahead = Prefetch / _viewport.Scale;
        var centerX = (left + right) / 2;
        var centerY = (top + bottom) / 2;
        var wanted = WorldMapTiling.TilesIn(desired, left - ahead, top - ahead, right + ahead, bottom + ahead)
            .OrderBy(index => Distance(desired, index, centerX, centerY))
            .Select(index => new WorldMapTileKey(World, desired.ZoomPercent, index))
            .ToList();
        var pinned = Enumerable.Range(1, coarsest.TileCount).Select(index => new WorldMapTileKey(World, coarsest.ZoomPercent, index)).ToList();

        _store?.Want(pinned, wanted);

        // Coarse under fine: whatever is not loaded yet shows the tile of a coarser zoom, blurred, rather than the sea.
        foreach (var level in new[] { coarsest, _fallback, desired }.Where(level => level.ZoomPercent != 0).DistinctBy(level => level.ZoomPercent).OrderBy(level => level.ZoomPercent))
            DrawLevel(context, level, left, top, right, bottom);
    }

    private void DrawLevel(DrawingContext context, WorldMapLevel level, double left, double top, double right, double bottom)
    {
        if (_store is null)
            return;

        foreach (var index in WorldMapTiling.TilesIn(level, left, top, right, bottom))
        {
            if (!_store.TryGet(new WorldMapTileKey(World, level.ZoomPercent, index), out var tile))
                continue;

            var (x, y, width, height) = WorldMapTiling.Place(level, index);
            var (viewLeft, viewTop) = _viewport.ToView(x, y);

            // Whole pixels, so that two tiles never leave a seam between them.
            var from = new Point(Math.Floor(viewLeft), Math.Floor(viewTop));
            var to = new Point(Math.Ceiling(viewLeft + (width * _viewport.Scale)), Math.Ceiling(viewTop + (height * _viewport.Scale)));

            context.DrawImage(tile, new Rect(0, 0, tile.PixelSize.Width, tile.PixelSize.Height), new Rect(from, to));
        }
    }

    private void DrawGrid(DrawingContext context, double mapWidth)
    {
        if (!ShowGrid || mapWidth < GridFrom)
            return;

        var (left, top, right, bottom) = _viewport.VisibleImage();
        var firstX = (int)Math.Floor((left - _world.OriginX) / _world.MapWidth);
        var lastX = (int)Math.Floor((right - _world.OriginX) / _world.MapWidth);
        var firstY = (int)Math.Floor((top - _world.OriginY) / _world.MapHeight);
        var lastY = (int)Math.Floor((bottom - _world.OriginY) / _world.MapHeight);

        for (var y = firstY; y <= lastY; y++)
        {
            for (var x = firstX; x <= lastX; x++)
            {
                if (_source!.Index.MapsAt(World, x, y).Length > 0)
                    context.DrawRectangle(null, _gridPen, RectOf(x, y).Deflate(0.5));
            }
        }
    }

    private void DrawSharedMarks(DrawingContext context, double mapWidth)
    {
        if (!ShowShared || mapWidth < GridFrom)
            return;

        var (left, top, right, bottom) = _viewport.VisibleImage();
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.SemiBold);

        foreach (var square in _shared)
        {
            var (px, py) = _world.PixelOf(square.X, square.Y);

            if (px > right || py > bottom || px + _world.MapWidth < left || py + _world.MapHeight < top)
                continue;

            var (center, radius) = SharedMark(square.X, square.Y, mapWidth);
            var count = new FormattedText(square.Count.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, radius * 1.1, _amber);

            context.DrawEllipse(_badge, null, center, radius, radius);
            context.DrawText(count, new Point(center.X - (count.Width / 2), center.Y - (count.Height / 2)));
        }
    }

    private (Point Center, double Radius) SharedMark(int x, int y, double mapWidth)
    {
        var rect = RectOf(x, y);
        var k = mapWidth / 64;
        var radius = Math.Max(6, (8 * k) + 2.5);

        return (new Point(rect.Right - 3 - radius, rect.Top + 3 + radius), radius);
    }

    private (int X, int Y)? SquareAt(Point at)
    {
        if (!_hasWorld)
            return null;

        var (ix, iy) = _viewport.ToImage(at.X, at.Y);
        var x = (int)Math.Floor((ix - _world.OriginX) / _world.MapWidth);
        var y = (int)Math.Floor((iy - _world.OriginY) / _world.MapHeight);

        return _source!.Index.MapsAt(World, x, y).Length > 0 ? (x, y) : null;
    }

    private WorldMapHit HitAt(Point at)
    {
        if (!_hasWorld)
            return WorldMapHit.None;

        var scene = Scene;
        var mapWidth = _world.MapWidth * _viewport.Scale;
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.Medium);

        for (var i = scene.Stations.Count - 1; i >= 0; i--)
        {
            var station = scene.Stations[i];

            if (WorldMapRouteLayer.StationBounds(station, RectOf(station.X, station.Y).Center, mapWidth, typeface).Contains(at))
                return new WorldMapHit(WorldMapHitKind.Station, station.X, station.Y, i, -1);
        }

        var reach = Math.Max(8, Math.Min(mapWidth, _world.MapHeight * _viewport.Scale) / 3);

        for (var leg = 0; leg < scene.Legs.Count; leg++)
        {
            var path = scene.Legs[leg];

            if (path.Kind is not WorldMapLegKind.Path)
                continue;

            foreach (var square in path.Squares.Skip(1).Take(path.Squares.Count - 2))
            {
                var center = RectOf(square.X, square.Y).Center;

                if (Math.Abs(center.X - at.X) <= reach && Math.Abs(center.Y - at.Y) <= reach && Math.Sqrt(((center.X - at.X) * (center.X - at.X)) + ((center.Y - at.Y) * (center.Y - at.Y))) <= reach)
                    return new WorldMapHit(WorldMapHitKind.PathPoint, square.X, square.Y, -1, leg);
            }
        }

        if (SquareAt(at) is not { } over)
            return WorldMapHit.None;

        if (ShowShared && mapWidth >= GridFrom && _source!.Index.MapsAt(World, over.X, over.Y).Length > 1)
        {
            var (center, radius) = SharedMark(over.X, over.Y, mapWidth);

            if (Math.Sqrt(((center.X - at.X) * (center.X - at.X)) + ((center.Y - at.Y) * (center.Y - at.Y))) <= radius + 2)
                return new WorldMapHit(WorldMapHitKind.Shared, over.X, over.Y, -1, -1);
        }

        return new WorldMapHit(WorldMapHitKind.Square, over.X, over.Y, -1, -1);
    }

    private void SetHit(WorldMapHit hit)
    {
        if (hit == _hit)
            return;

        _hit = hit;
        Cursor = hit.Kind switch
        {
            WorldMapHitKind.Station or WorldMapHitKind.Shared => new Cursor(StandardCursorType.Hand),
            WorldMapHitKind.PathPoint when Tool is WorldMapTool.Select => new Cursor(StandardCursorType.Hand),
            WorldMapHitKind.Square or WorldMapHitKind.PathPoint when Tool is WorldMapTool.Trace => new Cursor(StandardCursorType.Cross),
            _ => null,
        };
        InvalidateVisual();
        HitChanged?.Invoke();
    }

    private void StartDrag(PointerPressedEventArgs e, Drag drag, Point at)
    {
        _drag = drag;
        _dragFrom = at;
        e.Pointer.Capture(this);

        if (drag is Drag.Pan)
            Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void ZoomTowards(Point anchor, double scale)
    {
        _targetScale = Math.Clamp(scale, WorldMapViewport.MinScale, WorldMapViewport.MaxScale);
        _anchor = anchor;

        if (_easing)
            return;

        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            _viewport.ZoomAt(anchor.X, anchor.Y, _targetScale);
            Changed();

            return;
        }

        _easing = true;
        _lastFrame = TimeSpan.Zero;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        var elapsed = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : Math.Min((now - _lastFrame).TotalSeconds, 0.05);
        var ratio = _targetScale / _viewport.Scale;
        var done = Math.Abs(Math.Log(ratio)) < 0.002;
        var next = done ? _targetScale : _viewport.Scale * Math.Pow(ratio, 1 - Math.Exp(-elapsed * Ease));

        _lastFrame = now;
        _viewport.ZoomAt(_anchor.X, _anchor.Y, next);
        KeepInView();
        Changed();

        if (done)
        {
            _easing = false;

            return;
        }

        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void Settle()
    {
        _targetScale = _viewport.Scale;
        KeepInView();
        Changed();
    }

    private void KeepInView()
    {
        if (_hasWorld)
            _viewport.Keep(_world.Width, _world.Height, KeptOfImage);
    }

    private void Changed()
    {
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    private enum Drag
    {
        None,
        Pan,
        Paint,
        Move,
    }
}
