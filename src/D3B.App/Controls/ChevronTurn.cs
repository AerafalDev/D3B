using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace D3B.App.Controls;

/// <summary>A chevron's turn: pointing right while its section is folded, down once unfolded.</summary>
internal sealed class ChevronTurn : IValueConverter
{
    // Transform operations, not a RotateTransform: they are what a transition turns smoothly from one to the other.
    private static readonly ITransform _down = TransformOperations.Parse("rotate(90deg)");
    private static readonly ITransform _right = TransformOperations.Parse("rotate(0deg)");

    /// <summary>Gets the converter.</summary>
    public static ChevronTurn Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? _down : _right;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
