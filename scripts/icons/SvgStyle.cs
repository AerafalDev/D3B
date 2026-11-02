using System.Globalization;
using System.Xml.Linq;

namespace D3B.Scripts.Icons;

/// <summary>The presentation an SVG element inherits from its parents: fill, stroke and their opacity.</summary>
internal sealed record SvgStyle(string Fill, string Stroke, double StrokeWidth, string LineCap, string LineJoin, string FillRule, double Opacity)
{
    /// <summary>Gets SVG's own defaults: filled black, no stroke.</summary>
    public static SvgStyle Default { get; } = new("black", "none", 1, "butt", "miter", "nonzero", 1);

    /// <summary>Gives the style of an element: its parent's, with what its own attributes say.</summary>
    public SvgStyle Of(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var opacity = Opacity * Number(element, "opacity", 1);

        return new SvgStyle(
            Attribute(element, "fill") ?? Fill,
            Attribute(element, "stroke") ?? Stroke,
            Number(element, "stroke-width", StrokeWidth),
            Attribute(element, "stroke-linecap") ?? LineCap,
            Attribute(element, "stroke-linejoin") ?? LineJoin,
            Attribute(element, "fill-rule") ?? FillRule,
            opacity);
    }

    /// <summary>Gives the fill of an element, its own opacity applied.</summary>
    public string? FillOf(XElement element)
    {
        return Brush(Fill, Opacity * Number(element, "fill-opacity", 1));
    }

    /// <summary>Gives the stroke of an element, its own opacity applied.</summary>
    public string? StrokeOf(XElement element)
    {
        return Brush(Stroke, Opacity * Number(element, "stroke-opacity", 1));
    }

    private static string? Attribute(XElement element, string name)
    {
        return element.Attribute(name)?.Value.Trim();
    }

    private static double Number(XElement element, string name, double fallback)
    {
        return Attribute(element, name) is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;
    }

    private static string? Brush(string paint, double opacity)
    {
        var rgb = paint.ToLowerInvariant() switch
        {
            "none" or "transparent" => null,
            "white" => "FFFFFF",
            "black" => "000000",
            ['#', _, _, _, _, _, _] => paint[1..].ToUpperInvariant(),
            ['#', var r, var g, var b] => string.Concat(r, r, g, g, b, b).ToUpperInvariant(),
            _ => throw new InvalidDataException($"The paint {paint} is not one the tool reads: a colour #RRGGBB, #RGB, white, black or none."),
        };

        if (rgb is null)
            return null;

        var alpha = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

        return $"#{alpha:X2}{rgb}";
    }
}
