using System.Globalization;
using Avalonia.Data.Converters;

namespace D3B.App.Controls;

/// <summary>Whether a tone is one, for a class that colours what has it, such as a decision's dot (D325).</summary>
internal sealed class ToneIs : IValueConverter
{
    private readonly Tone _tone;

    private ToneIs(Tone tone)
    {
        _tone = tone;
    }

    /// <summary>Gets the converter that tells a success.</summary>
    public static ToneIs Success { get; } = new(Tone.Success);

    /// <summary>Gets the converter that tells a warning.</summary>
    public static ToneIs Warning { get; } = new(Tone.Warning);

    /// <summary>Gets the converter that tells an information.</summary>
    public static ToneIs Info { get; } = new(Tone.Info);

    /// <summary>Gets the converter that tells a danger.</summary>
    public static ToneIs Danger { get; } = new(Tone.Danger);

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Tone tone && tone == _tone;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
