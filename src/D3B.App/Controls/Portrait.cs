using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace D3B.App.Controls;

/// <summary>A character's portrait: its class's head on its breed's colour, or its initials when the pack has no head of it (D323).</summary>
/// <remarks>
/// Drawn rather than templated: the studio shows dozens, in the cards, the inspector, the run block, the tabs and the search,
/// at 16 to 44 pixels. A light falls from the top left, as on the mockup's faces; a portrait dimmed is one offline.
/// </remarks>
internal sealed class Portrait : Control
{
    /// <summary>Defines the <see cref="Head"/> property.</summary>
    public static readonly StyledProperty<IImage?> HeadProperty = AvaloniaProperty.Register<Portrait, IImage?>(nameof(Head));

    /// <summary>Defines the <see cref="Initials"/> property.</summary>
    public static readonly StyledProperty<string?> InitialsProperty = AvaloniaProperty.Register<Portrait, string?>(nameof(Initials));

    /// <summary>Defines the <see cref="Color"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<Portrait, IBrush?>(nameof(Color));

    /// <summary>Defines the <see cref="Radius"/> property.</summary>
    public static readonly StyledProperty<double> RadiusProperty = AvaloniaProperty.Register<Portrait, double>(nameof(Radius), 7);

    /// <summary>Defines the <see cref="IsDimmed"/> property.</summary>
    public static readonly StyledProperty<bool> IsDimmedProperty = AvaloniaProperty.Register<Portrait, bool>(nameof(IsDimmed));

    private static readonly IBrush _light = new RadialGradientBrush
    {
        Center = new RelativePoint(0.25, 0.15, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.25, 0.15, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(1.2, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(1.2, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Avalonia.Media.Color.FromArgb(0x47, 0xFF, 0xFF, 0xFF), 0), new GradientStop(Avalonia.Media.Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0.55)],
    }.ToImmutable();

    private static readonly IBrush _shade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0.6, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(Avalonia.Media.Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Avalonia.Media.Color.FromArgb(0x40, 0, 0, 0), 1)],
    }.ToImmutable();

    // Immutable: the portraits share them, drawn on whichever thread renders first.
    private static readonly IPen _edge = new ImmutablePen(new ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)), 1);
    private static readonly IBrush _dim = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb(0x8C, 0x0E, 0x0E, 0x10));

    /// <summary>Gets or sets the class's head, from the pack.</summary>
    public IImage? Head
    {
        get => GetValue(HeadProperty);
        set => SetValue(HeadProperty, value);
    }

    /// <summary>Gets or sets the initials drawn without a head.</summary>
    public string? Initials
    {
        get => GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    /// <summary>Gets or sets the breed's colour, under the head.</summary>
    public IBrush? Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Gets or sets the corners' radius.</summary>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Gets or sets whether the portrait is dimmed, as an offline character's.</summary>
    public bool IsDimmed
    {
        get => GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }

    static Portrait()
    {
        AffectsRender<Portrait>(HeadProperty, InitialsProperty, ColorProperty, RadiusProperty, IsDimmedProperty);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var area = new Rect(Bounds.Size);
        var shape = new RoundedRect(area, Radius);

        using (context.PushClip(shape))
        {
            context.FillRectangle(Color ?? Brushes.DimGray, area);
            context.FillRectangle(_light, area);

            if (Head is { } head)
            {
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                    context.DrawImage(head, new Rect(head.Size), area);
            }
            else if (Initials is { Length: > 0 } initials)
            {
                var text = new FormattedText(initials, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(GetValue(TextElement.FontFamilyProperty), weight: FontWeight.SemiBold), Math.Round(area.Height * 0.39), Brushes.White);

                context.DrawText(text, new Point((area.Width - text.Width) / 2, (area.Height - text.Height) / 2));
            }

            context.FillRectangle(_shade, area);

            if (IsDimmed)
                context.FillRectangle(_dim, area);
        }

        context.DrawRectangle(null, _edge, shape.Deflate(0.5, 0.5));
    }
}
