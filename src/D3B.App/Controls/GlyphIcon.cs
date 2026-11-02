using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>One of the client's glyphs, white on transparent in the pack, painted in the colour of the text around it (D336).</summary>
/// <remarks>
/// The glyph is a mask: its alpha cuts the colour, as IntelliJ's tinted icons are repainted, so that one glyph serves every
/// state, green, amber or dim. Without the pack, or a pack without glyphs, it draws a neutral dot, never a broken picture.
/// </remarks>
internal sealed class GlyphIcon : Control
{
    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<GlyphIcon, IImage?>(nameof(Source));

    /// <summary>Defines the <see cref="Foreground"/> property, inherited as text's is.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<GlyphIcon>();

    /// <summary>Gets or sets the glyph, from the pack.</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Gets or sets the colour the glyph is painted in.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static GlyphIcon()
    {
        AffectsRender<GlyphIcon>(SourceProperty, ForegroundProperty);
        WidthProperty.OverrideDefaultValue<GlyphIcon>(14);
        HeightProperty.OverrideDefaultValue<GlyphIcon>(14);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Foreground is not { } foreground)
            return;

        var area = new Rect(Bounds.Size);

        if (Source is not IImageBrushSource source)
        {
            var radius = Math.Min(area.Width, area.Height) * 0.18;

            context.DrawEllipse(foreground, null, area.Center, radius, radius);

            return;
        }

        var mask = new ImageBrush(source) { Stretch = Stretch.Uniform };

        // Drawn at 11 to 18 pixels from 64: smoothed, or the glyph's edges stair.
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality }))
        using (context.PushOpacityMask(mask, area))
            context.FillRectangle(foreground, area);
    }
}
