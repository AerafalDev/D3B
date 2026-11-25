using System.Globalization;
using System.Xml.Linq;

namespace D3B.Scripts.Icons;

/// <summary>A Lucide icon read from its SVG file: its outlines on a grid of 24, all of them stroked, none filled.</summary>
/// <remarks>
/// Lucide draws every icon the same way, measured on the files the tool takes: paths, circles, ellipses, rectangles, lines,
/// polylines and polygons, stroked with the colour around them, round caps and joins, nothing filled, no transform. Anything
/// else fails rather than draws the icon wrong.
/// </remarks>
internal sealed class LucideIcon
{
    private const string ViewBox = "0 0 24 24";

    private static readonly XNamespace _svg = "http://www.w3.org/2000/svg";

    /// <summary>Gets the icon's outlines, in Avalonia's path syntax, in the order they are drawn.</summary>
    public IReadOnlyList<string> Outlines { get; }

    private LucideIcon(IReadOnlyList<string> outlines)
    {
        Outlines = outlines;
    }

    /// <summary>Reads an icon.</summary>
    /// <param name="name">The icon's name, such as <c>search</c>, which errors give.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="InvalidDataException">The file draws with what the tool does not read.</exception>
    public static LucideIcon Read(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var root = XDocument.Parse(text, LoadOptions.None).Root ?? throw new InvalidDataException($"{name}: the file has no svg element.");

        if (root.Attribute("viewBox")?.Value != ViewBox || root.Attribute("fill")?.Value != "none" || root.Attribute("stroke")?.Value != "currentColor")
            throw new InvalidDataException($"{name}: the icon is not drawn as Lucide's are, stroked on a grid of 24.");

        var outlines = new List<string>();

        Walk(name, root, outlines);

        return outlines.Count > 0 ? new LucideIcon(outlines) : throw new InvalidDataException($"{name}: the icon draws nothing.");
    }

    private static void Walk(string name, XElement parent, List<string> outlines)
    {
        foreach (var element in parent.Elements())
        {
            if (element.Attribute("transform") is not null || element.Attribute("fill") is { Value: not "none" })
                throw new InvalidDataException($"{name}: a transform or a fill is not one the tool reads.");

            switch (element.Name.LocalName)
            {
                case "g":
                    Walk(name, element, outlines);
                    break;
                case "path":
                    outlines.Add(SvgPath.FromData((string)element.Attribute("d")!, evenOdd: false));
                    break;
                case "circle":
                    outlines.Add(SvgPath.FromEllipse(Number(element, "cx"), Number(element, "cy"), Number(element, "r"), Number(element, "r")));
                    break;
                case "ellipse":
                    outlines.Add(SvgPath.FromEllipse(Number(element, "cx"), Number(element, "cy"), Number(element, "rx"), Number(element, "ry")));
                    break;
                case "rect":
                    outlines.Add(SvgPath.FromRectangle(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"), Number(element, "rx"), Number(element, "ry")));
                    break;
                case "line":
                    outlines.Add(Polyline([Number(element, "x1"), Number(element, "y1"), Number(element, "x2"), Number(element, "y2")], closed: false));
                    break;
                case "polyline":
                    outlines.Add(Polyline(Points(element), closed: false));
                    break;
                case "polygon":
                    outlines.Add(Polyline(Points(element), closed: true));
                    break;
                default:
                    throw new InvalidDataException($"{name}: the element {element.Name.LocalName} is not one the tool reads.");
            }
        }
    }

    private static double[] Points(XElement element)
    {
        return [.. ((string)element.Attribute("points")!).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture))];
    }

    private static string Polyline(double[] points, bool closed)
    {
        var parts = new List<string>();

        for (var i = 0; i + 1 < points.Length; i += 2)
            parts.Add($"{(i == 0 ? "M" : "L")}{Format(points[i])},{Format(points[i + 1])}");

        return "F1 " + string.Join(' ', parts) + (closed ? " Z" : "");
    }

    private static string Format(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static double Number(XElement element, string attribute)
    {
        return element.Attribute(attribute)?.Value is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : 0;
    }
}
