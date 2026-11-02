using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>Gives the IntelliJ drawing of a key, such as <c>Nodes.Folder</c>, among those of <c>Themes/IntelliJIcons.axaml</c> (D331).</summary>
/// <remarks>Lets a view model name an icon by its key, never by a drawing of the UI's framework.</remarks>
internal sealed class DrawingOf : IValueConverter
{
    /// <summary>Gets the converter.</summary>
    public static DrawingOf Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key && Application.Current is { } app && app.TryGetResource(key, null, out var drawing) && drawing is DrawingGroup group
            ? group
            : null;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
