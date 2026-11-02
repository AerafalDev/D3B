using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Rendering;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>An icon: an IntelliJ drawing on a grid of 16 units in its own colours, or a line geometry on a grid of 24 in the colour of the text around it.</summary>
/// <remarks>
/// <para>
/// The drawings are the IntelliJ Platform's, in <c>Themes/IntelliJIcons.axaml</c>, which <c>scripts/icons.cs</c> writes
/// (D305, D331): they keep their colours, as IntelliJ draws them, and a tint repaints their neutral strokes and fills, such
/// as the white of an icon on the accent.
/// </para>
/// <para>
/// A geometry is a module page's own icon, lines in SVG's path syntax (<c>PageDefinition.Icon</c>); Lucide's, which the app
/// drew until 7c4, are gone (D355). A button's icon takes the button's text colour.
/// </para>
/// </remarks>
internal sealed class Icon : Control, ICustomHitTest
{
    /// <summary>The geometry's grid: an icon of any size draws it scaled from 24 units.</summary>
    private const double Grid = 24;

    /// <summary>The drawings' grid, IntelliJ's.</summary>
    private const double DrawingGrid = 16;

    /// <summary>Defines the <see cref="Data"/> property.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty = AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    /// <summary>Defines the <see cref="Drawing"/> property.</summary>
    public static readonly StyledProperty<DrawingGroup?> DrawingProperty = AvaloniaProperty.Register<Icon, DrawingGroup?>(nameof(Drawing));

    /// <summary>Defines the <see cref="Tint"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TintProperty = AvaloniaProperty.Register<Icon, IBrush?>(nameof(Tint));

    /// <summary>Defines the <see cref="Foreground"/> property, inherited as text's is.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    /// <summary>Defines the <see cref="StrokeThickness"/> property.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 2);

    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<Tone> ToneProperty = AvaloniaProperty.Register<Icon, Tone>(nameof(Tone));

    /// <summary>Gets or sets the icon's lines, on a grid of 24 units.</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Gets or sets the IntelliJ drawing the icon shows, on a grid of 16 units; it wins over <see cref="Data"/>.</summary>
    public DrawingGroup? Drawing
    {
        get => GetValue(DrawingProperty);
        set => SetValue(DrawingProperty, value);
    }

    /// <summary>Gets or sets the colour the drawing's neutral strokes and fills take, or <see langword="null"/> for IntelliJ's own.</summary>
    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Gets or sets the colour of the icon's lines.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the thickness of the icon's lines, in units of its grid.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Gets or sets the tone the icon's lines take, as <c>Themes/Styles.axaml</c> colours each; neutral keeps the text's colour.</summary>
    public Tone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, DrawingProperty, TintProperty, ForegroundProperty, StrokeThicknessProperty);
        WidthProperty.OverrideDefaultValue<Icon>(16);
        HeightProperty.OverrideDefaultValue<Icon>(16);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == IsEffectivelyEnabledProperty)
            InvalidateVisual();
    }

    /// <inheritdoc/>
    public bool HitTest(Point point)
    {
        // An icon may carry a tooltip, such as a setting's help: its whole square answers the pointer, not its lines alone.
        return new Rect(Bounds.Size).Contains(point);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Drawing is { } drawing)
        {
            RenderDrawing(context, drawing);

            return;
        }

        if (Data is not { } data || Foreground is not { } foreground)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / Grid;
        var pen = new Pen(foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, pen, data);
    }

    private static bool IsNeutral(IBrush? brush)
    {
        // IntelliJ's dark icons draw their lines in these greys, the ones its tinted icons repaint.
        return brush is ISolidColorBrush { Color: var color } && (color == Color.FromRgb(0xCE, 0xD0, 0xD6) || color == Color.FromRgb(0xB4, 0xB8, 0xBF));
    }

    private static IBrush? Grey(IBrush? brush)
    {
        // A disabled control's icon loses its colour, as IntelliJ draws it: a green play turns grey.
        if (brush is not ISolidColorBrush { Color: var color })
            return brush;

        var luma = (byte)Math.Round(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);

        return new SolidColorBrush(Color.FromArgb(color.A, luma, luma, luma));
    }

    private void RenderDrawing(DrawingContext context, DrawingGroup drawing)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / DrawingGrid;
        var tint = Tint;
        var grey = !IsEffectivelyEnabled;

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (var child in drawing.Children)
            {
                if (child is not GeometryDrawing { Geometry: { } geometry } shape)
                    continue;

                var fill = tint is not null && IsNeutral(shape.Brush) ? tint : shape.Brush;
                var pen = shape.Pen;

                if (pen is { } outline && ((tint is not null && IsNeutral(outline.Brush)) || grey))
                    pen = new Pen(tint is not null && IsNeutral(outline.Brush) ? tint : Grey(outline.Brush), outline.Thickness, lineCap: outline.LineCap, lineJoin: outline.LineJoin);

                if (grey)
                    fill = Grey(fill);

                context.DrawGeometry(fill, pen, geometry);
            }
        }
    }
}
