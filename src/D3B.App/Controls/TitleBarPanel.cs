using Avalonia;
using Avalonia.Controls;

namespace D3B.App.Controls;

/// <summary>Lays out the window's one title row: its first child on the left, its last on the right, the middle one centred on the window as long as it does not touch them (D322).</summary>
/// <remarks>
/// The search sits in the middle of the window, not of the room left between the menus and the run block, which differ in
/// width: centred on the window while it fits, it narrows to its least, then slides away from whichever side would touch
/// it, as the mockup's row does.
/// </remarks>
internal sealed class TitleBarPanel : Panel
{
    /// <summary>Defines the <see cref="Gap"/> property.</summary>
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<TitleBarPanel, double>(nameof(Gap), 12);

    /// <summary>Gets or sets the least room between the middle child and either side.</summary>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    static TitleBarPanel()
    {
        AffectsMeasure<TitleBarPanel>(GapProperty);
    }

    /// <summary>Places the middle child: centred on the row, narrowed then slid aside so that it never covers a side.</summary>
    /// <param name="width">The row's width.</param>
    /// <param name="left">The left child's width.</param>
    /// <param name="right">The right child's width.</param>
    /// <param name="middle">The middle child's widest.</param>
    /// <param name="least">The middle child's narrowest, below which it is not shown.</param>
    /// <param name="gap">The least room on each of its sides.</param>
    /// <returns>Where the middle child starts and how wide it is.</returns>
    internal static (double X, double Width) Middle(double width, double left, double right, double middle, double least, double gap)
    {
        var room = Math.Max(0, width - left - right - 2 * gap);
        var shown = Math.Min(middle, room);

        // Narrower than its least, it would only be cut: it gives its room up, Ctrl K still opening the search.
        if (shown < least)
            return (left + gap, 0);

        var x = (width - shown) / 2;

        x = Math.Max(x, left + gap);
        x = Math.Min(x, width - right - gap - shown);

        return (Math.Max(left + gap, x), shown);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? Children.Sum(child => child.DesiredSize.Width) : availableSize.Width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
            return finalSize;

        var left = Children[0];
        var right = Children.Count > 1 ? Children[^1] : null;
        var middle = Children.Count > 2 ? Children[1] : null;
        var leftWidth = Math.Min(left.DesiredSize.Width, finalSize.Width);
        var rightWidth = right is null ? 0 : Math.Min(right.DesiredSize.Width, finalSize.Width - leftWidth);

        left.Arrange(new Rect(0, 0, leftWidth, finalSize.Height));
        right?.Arrange(new Rect(finalSize.Width - rightWidth, 0, rightWidth, finalSize.Height));

        if (middle is not null)
        {
            var widest = double.IsNaN(middle.Width) ? middle.MaxWidth : middle.Width;
            var (x, width) = Middle(finalSize.Width, leftWidth, rightWidth, double.IsInfinity(widest) ? middle.DesiredSize.Width : widest, middle.MinWidth, Gap);

            middle.Arrange(new Rect(x, 0, width, finalSize.Height));
        }

        return finalSize;
    }
}
