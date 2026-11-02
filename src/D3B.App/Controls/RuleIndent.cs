using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace D3B.App.Controls;

/// <summary>The margin of a rail's dot: a rule's, smaller, kept on the rail's line and its text set in under its layer (D325).</summary>
internal sealed class RuleIndent : IValueConverter
{
    private static readonly Thickness _rule = new(2, 0, 14, 0);

    /// <summary>Gets the converter.</summary>
    public static RuleIndent Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _rule : default(Thickness);
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
