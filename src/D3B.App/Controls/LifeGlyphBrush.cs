using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace D3B.App.Controls;

/// <summary>The life's glyph's colour: a muted red, a brighter one under half (D323).</summary>
internal sealed class LifeGlyphBrush : IValueConverter
{
    private static readonly IBrush _usual = new ImmutableSolidColorBrush(Color.Parse("#C9665E"));
    private static readonly IBrush _low = new ImmutableSolidColorBrush(Color.Parse("#EF6B5F"));

    /// <summary>Gets the converter.</summary>
    public static LifeGlyphBrush Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _low : _usual;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
