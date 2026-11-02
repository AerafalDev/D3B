using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>An edge between two docks, dragged to resize one of them, which a line in the accent marks under the pointer (D320, D321).</summary>
/// <remarks>
/// It only says how far it was dragged since the press: the window gives the size to the studio's layout, which keeps it
/// within its bounds, so that a drag past a bound resumes where the pointer comes back. The line shows after a short
/// while, as the mockup's, so that the pointer crossing an edge does not flash it.
/// </remarks>
internal sealed class DockSplitter : Control
{
    /// <summary>Defines the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<DockSplitter, Orientation>(nameof(Orientation));

    /// <summary>Defines the <see cref="LineBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<DockSplitter, IBrush?>(nameof(LineBrush));

    private static readonly StyledProperty<double> _lineOpacityProperty = AvaloniaProperty.Register<DockSplitter, double>("LineOpacity");

    private Point? _pressed;

    /// <summary>Raised as a drag starts.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised as the pointer moves while dragged, with how far it went since the press.</summary>
    public event EventHandler<VectorEventArgs>? DragDelta;

    /// <summary>Raised as a drag ends.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>Gets or sets the edge's direction: <see cref="Orientation.Vertical"/> between two columns, <see cref="Orientation.Horizontal"/> between two rows.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets the colour of the line that marks the edge.</summary>
    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    static DockSplitter()
    {
        AffectsRender<DockSplitter>(OrientationProperty, LineBrushProperty, _lineOpacityProperty);
    }

    /// <summary>Initializes a new instance of the <see cref="DockSplitter"/> class.</summary>
    public DockSplitter()
    {
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        Transitions = [new DoubleTransition { Property = _lineOpacityProperty, Duration = TimeSpan.FromMilliseconds(120), Delay = TimeSpan.FromMilliseconds(120) }];
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The whole edge answers the pointer, though only its line is drawn.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var opacity = GetValue(_lineOpacityProperty);

        if (opacity <= 0 || LineBrush is not { } brush)
            return;

        var line = Orientation is Orientation.Vertical
            ? new Rect((Bounds.Width - 2) / 2, 10, 2, Math.Max(0, Bounds.Height - 20))
            : new Rect(10, (Bounds.Height - 2) / 2, Math.Max(0, Bounds.Width - 20), 2);

        using (context.PushOpacity(opacity))
            context.DrawRectangle(brush, null, line, 1, 1);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrientationProperty)
            Cursor = new Cursor(Orientation is Orientation.Vertical ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth);
    }

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        SetValue(_lineOpacityProperty, 1d);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_pressed is null)
            SetValue(_lineOpacityProperty, 0d);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _pressed = e.GetPosition(Parent as Visual ?? this);
        e.Pointer.Capture(this);
        e.Handled = true;
        DragStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);

        if (_pressed is not { } pressed)
            return;

        // Measured against the parent, which does not move as the edge does.
        var now = e.GetPosition(Parent as Visual ?? this);

        DragDelta?.Invoke(this, new VectorEventArgs { Vector = now - pressed });
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);
        End(e.Pointer);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        End(null);
    }

    private void End(IPointer? pointer)
    {
        if (_pressed is null)
            return;

        _pressed = null;
        pointer?.Capture(null);

        if (!IsPointerOver)
            SetValue(_lineOpacityProperty, 0d);

        DragCompleted?.Invoke(this, EventArgs.Empty);
    }
}
