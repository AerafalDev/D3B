using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>A thin gauge: a share filled on its track, and a threshold's mark when one is given, as the cards' life and pods and the rail's rules draw them (D323, D325).</summary>
internal sealed class ShareBar : Control
{
    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ShareBar, double>(nameof(Value));

    /// <summary>Defines the <see cref="Fill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ShareBar, IBrush?>(nameof(Fill));

    /// <summary>Defines the <see cref="Track"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<ShareBar, IBrush?>(nameof(Track));

    /// <summary>Defines the <see cref="Mark"/> property.</summary>
    public static readonly StyledProperty<double?> MarkProperty = AvaloniaProperty.Register<ShareBar, double?>(nameof(Mark));

    /// <summary>Defines the <see cref="MarkBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty = AvaloniaProperty.Register<ShareBar, IBrush?>(nameof(MarkBrush));

    /// <summary>Gets or sets the share filled, from 0 to 100.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets what fills the share.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Gets or sets the track under it.</summary>
    public IBrush? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    /// <summary>Gets or sets the threshold marked across the gauge, from 0 to 100, if any.</summary>
    public double? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <summary>Gets or sets the threshold's mark's colour.</summary>
    public IBrush? MarkBrush
    {
        get => GetValue(MarkBrushProperty);
        set => SetValue(MarkBrushProperty, value);
    }

    static ShareBar()
    {
        AffectsRender<ShareBar>(ValueProperty, FillProperty, TrackProperty, MarkProperty, MarkBrushProperty);
        HeightProperty.OverrideDefaultValue<ShareBar>(5);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var area = new Rect(Bounds.Size);
        var radius = area.Height / 2;

        if (Track is { } track)
            context.DrawRectangle(track, null, new RoundedRect(area, radius));

        var filled = area.Width * Math.Clamp(Value, 0, 100) / 100;

        if (Fill is { } fill && filled > 0)
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, 0, Math.Max(filled, area.Height), area.Height), radius));

        // The mark stands out of the track, as the mockup's: a threshold reads at a glance against the value.
        if (Mark is { } mark && MarkBrush is { } brush)
        {
            var x = area.Width * Math.Clamp(mark, 0, 100) / 100;

            context.DrawRectangle(brush, null, new RoundedRect(new Rect(x - 1, -3, 2, area.Height + 6), 1));
        }
    }
}
