using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace D3B.App.Controls;

/// <summary>A column of the inspector's rows, from the labels' share: the labels' own given no parameter, the values' given <c>Value</c> (D324).</summary>
internal sealed class ShareColumn : IValueConverter
{
    /// <summary>Gets the converter.</summary>
    public static ShareColumn Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var share = value is double shown ? shown : 0.36;

        return new GridLength(parameter is "Value" ? 1 - share : share, GridUnitType.Star);
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
