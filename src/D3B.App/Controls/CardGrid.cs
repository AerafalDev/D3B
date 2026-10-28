using Avalonia;
using Avalonia.Controls;

namespace D3B.App.Controls;

/// <summary>Lays its children out in as many equal columns as its width holds, each row as tall as its tallest child.</summary>
/// <remarks>
/// The columns depend on the width alone, not on how many children there are, so that a bot's card keeps its size whether
/// the host plays one character or eight.
/// </remarks>
internal sealed class CardGrid : Panel
{
    /// <summary>Defines the <see cref="MinColumnWidth"/> property.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(MinColumnWidth), 420);

    /// <summary>Defines the <see cref="MaxColumns"/> property.</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty = AvaloniaProperty.Register<CardGrid, int>(nameof(MaxColumns), 3);

    /// <summary>Defines the <see cref="Spacing"/> property.</summary>
    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 16);

    /// <summary>Gets or sets the narrowest a column may be.</summary>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Gets or sets the most columns, however wide the grid.</summary>
    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>Gets or sets the space between two columns and between two rows.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    static CardGrid()
    {
        AffectsMeasure<CardGrid>(MinColumnWidthProperty, MaxColumnsProperty, SpacingProperty);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * MaxColumns + Spacing * (MaxColumns - 1) : availableSize.Width;
        var (columns, columnWidth) = Columns(width);
        var height = 0.0;

        foreach (var row in Rows(columns))
        {
            var tallest = 0.0;

            foreach (var child in row)
            {
                child.Measure(new Size(columnWidth, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }

            height += (height > 0 ? Spacing : 0) + tallest;
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, columnWidth) = Columns(finalSize.Width);
        var top = 0.0;

        foreach (var row in Rows(columns))
        {
            var tallest = row.Max(child => child.DesiredSize.Height);

            for (var column = 0; column < row.Count; column++)
                row[column].Arrange(new Rect(column * (columnWidth + Spacing), top, columnWidth, tallest));

            top += tallest + Spacing;
        }

        return finalSize;
    }

    private (int Columns, double Width) Columns(double width)
    {
        var columns = Math.Clamp((int)((width + Spacing) / (MinColumnWidth + Spacing)), 1, Math.Max(1, MaxColumns));

        return (columns, Math.Max(0, (width - Spacing * (columns - 1)) / columns));
    }

    private IEnumerable<List<Control>> Rows(int columns)
    {
        return Children.Where(child => child.IsVisible).Chunk(columns).Select(row => row.ToList());
    }
}
