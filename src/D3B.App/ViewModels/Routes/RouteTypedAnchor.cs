using System.Globalization;
using System.Text.RegularExpressions;

namespace D3B.App.ViewModels.Routes;

/// <summary>Reads what is typed to add an anchor: coordinates, or a map's id (D299).</summary>
internal static partial class RouteTypedAnchor
{
    private static readonly Regex _coordinates = CoordinatesPattern();
    private static readonly Regex _mapId = MapIdPattern();

    /// <summary>Reads coordinates such as "-12,5", "[3 4]" or "7;-8".</summary>
    /// <param name="text">What was typed, trimmed.</param>
    /// <param name="x">The first coordinate.</param>
    /// <param name="y">The second coordinate.</param>
    /// <returns><see langword="true"/> when the text is a pair of coordinates.</returns>
    public static bool TryCoordinates(string text, out int x, out int y)
    {
        ArgumentNullException.ThrowIfNull(text);

        var pair = _coordinates.Match(text);

        x = 0;
        y = 0;

        if (!pair.Success)
            return false;

        x = int.Parse(pair.Groups[1].Value, CultureInfo.InvariantCulture);
        y = int.Parse(pair.Groups[2].Value, CultureInfo.InvariantCulture);

        return true;
    }

    /// <summary>Reads a map's id: five digits or more.</summary>
    /// <param name="text">What was typed, trimmed.</param>
    /// <param name="map">The map's id.</param>
    /// <returns><see langword="true"/> when the text is a map's id.</returns>
    public static bool TryMapId(string text, out long map)
    {
        ArgumentNullException.ThrowIfNull(text);

        map = 0;

        return _mapId.IsMatch(text) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out map);
    }

    [GeneratedRegex(@"^\[?\s*(-?\d{1,4})\s*[,;\s]\s*(-?\d{1,4})\s*\]?$", RegexOptions.CultureInvariant)]
    private static partial Regex CoordinatesPattern();

    [GeneratedRegex(@"^\d{5,}$", RegexOptions.CultureInvariant)]
    private static partial Regex MapIdPattern();
}
