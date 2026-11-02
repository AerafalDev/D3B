using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace D3B.Scripts.Icons;

/// <summary>An IntelliJ icon read from its SVG file: the notice it carries and its shapes on a grid of 16.</summary>
/// <remarks>
/// The tool reads what the IntelliJ Platform's icons are drawn with, measured on the files it takes: paths, rectangles,
/// circles and ellipses, in groups, filled and stroked with plain colours, some through a transform, a clip that is the
/// whole grid. Anything else, a mask, a gradient, a clip of part of the icon, fails rather than draws the icon wrong.
/// </remarks>
internal sealed partial class SvgIcon
{
    private const double Grid = 16;

    private static readonly XNamespace _svg = "http://www.w3.org/2000/svg";

    /// <summary>Gets the notice the file carries, as its first comment says it.</summary>
    public string Notice { get; }

    /// <summary>Gets the icon's shapes, in the order they are drawn.</summary>
    public IReadOnlyList<IconShape> Shapes { get; }

    private SvgIcon(string notice, IReadOnlyList<IconShape> shapes)
    {
        Notice = notice;
        Shapes = shapes;
    }

    /// <summary>Reads an icon, refusing it unless its file says it is under the Apache 2.0 license.</summary>
    /// <param name="name">The icon's name, such as <c>general/search</c>, which errors give.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="InvalidDataException">The file carries no such notice, or draws with what the tool does not read.</exception>
    public static SvgIcon Read(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var document = XDocument.Parse(text, LoadOptions.None);
        var comment = document.Nodes().OfType<XComment>().FirstOrDefault()?.Value.Trim();

        if (comment is null || !Apache().IsMatch(comment))
            throw new InvalidDataException($"{name}: its file does not say it is under the Apache 2.0 license, so it is not taken.");

        var root = document.Root ?? throw new InvalidDataException($"{name}: the file has no svg element.");

        if (Number(root, "width") != Grid || Number(root, "height") != Grid || root.Attribute("viewBox")?.Value != "0 0 16 16")
            throw new InvalidDataException($"{name}: the icon is not drawn on a grid of 16.");

        var shapes = new List<IconShape>();
        var clips = root.Descendants(_svg + "clipPath").ToDictionary(clip => (string)clip.Attribute("id")!, clip => clip);

        Walk(name, root, SvgStyle.Default.Of(root), null, clips, shapes);

        return shapes.Count > 0 ? new SvgIcon(comment, shapes) : throw new InvalidDataException($"{name}: the icon draws nothing.");
    }

    [GeneratedRegex(@"^Copyright 2000-20\d\d JetBrains s\.r\.o\. and contributors\. Use of this source code is governed by the Apache 2\.0 license\.$")]
    private static partial Regex Apache();

    private static void Walk(string name, XElement parent, SvgStyle style, string? transform, Dictionary<string, XElement> clips, List<IconShape> shapes)
    {
        foreach (var element in parent.Elements())
        {
            var local = element.Name.LocalName;

            if (local is "defs" or "title" or "desc")
                continue;

            CheckClip(name, element, clips);

            var own = style.Of(element);
            var matrix = Combine(name, transform, element.Attribute("transform")?.Value);

            switch (local)
            {
                case "g":
                    Walk(name, element, own, matrix, clips, shapes);
                    break;
                case "path":
                    Add(element, own, matrix, SvgPath.FromData((string)element.Attribute("d")!, own.FillRule == "evenodd"), shapes);
                    break;
                case "rect":
                    Add(element, own, matrix, SvgPath.FromRectangle(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height"), Number(element, "rx"), Number(element, "ry")), shapes);
                    break;
                case "circle":
                    Add(element, own, matrix, SvgPath.FromEllipse(Number(element, "cx"), Number(element, "cy"), Number(element, "r"), Number(element, "r")), shapes);
                    break;
                case "ellipse":
                    Add(element, own, matrix, SvgPath.FromEllipse(Number(element, "cx"), Number(element, "cy"), Number(element, "rx"), Number(element, "ry")), shapes);
                    break;
                default:
                    throw new InvalidDataException($"{name}: the element {local} is not one the tool reads.");
            }
        }
    }

    private static void Add(XElement element, SvgStyle style, string? matrix, string data, List<IconShape> shapes)
    {
        var fill = style.FillOf(element);
        var stroke = style.StrokeOf(element);

        if (fill is null && stroke is null)
            return;

        var cap = style.LineCap switch
        {
            "round" => "Round",
            "square" => "Square",
            _ => "Flat",
        };
        var join = style.LineJoin switch
        {
            "round" => "Round",
            "bevel" => "Bevel",
            _ => "Miter",
        };

        shapes.Add(new IconShape(data, matrix, fill, stroke, style.StrokeWidth, cap, join));
    }

    private static void CheckClip(string name, XElement element, Dictionary<string, XElement> clips)
    {
        if (element.Attribute("mask") is not null || element.Attribute("filter") is not null)
            throw new InvalidDataException($"{name}: a mask or a filter is not one the tool reads.");

        if (element.Attribute("clip-path")?.Value is not { } clip)
            return;

        // IntelliJ's exports clip most icons to their whole grid, which clips nothing.
        var id = clip.Replace("url(#", "", StringComparison.Ordinal).TrimEnd(')');
        var shapes = clips.TryGetValue(id, out var path) ? path.Elements().ToList() : [];

        if (shapes is not [{ Name.LocalName: "rect" } rect] || Number(rect, "x") != 0 || Number(rect, "y") != 0 || Number(rect, "width") < Grid || Number(rect, "height") < Grid || rect.Attribute("transform") is not null)
            throw new InvalidDataException($"{name}: its clip {id} is not the whole grid.");
    }

    private static string? Combine(string name, string? outer, string? inner)
    {
        if (inner is null)
            return outer;

        var matrix = Matrix(name, inner);

        if (outer is null)
            return Write(matrix);

        var o = outer.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();

        // The inner transform applies first, then the outer: a point p is drawn at outer(inner(p)).
        return Write([
            matrix[0] * o[0] + matrix[1] * o[2],
            matrix[0] * o[1] + matrix[1] * o[3],
            matrix[2] * o[0] + matrix[3] * o[2],
            matrix[2] * o[1] + matrix[3] * o[3],
            matrix[4] * o[0] + matrix[5] * o[2] + o[4],
            matrix[4] * o[1] + matrix[5] * o[3] + o[5]]);
    }

    private static double[] Matrix(string name, string transform)
    {
        var match = TransformPattern().Match(transform.Trim());

        if (!match.Success)
            throw new InvalidDataException($"{name}: the transform {transform} is not one the tool reads.");

        var values = match.Groups["values"].Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();

        switch (match.Groups["kind"].Value, values.Length)
        {
            case ("matrix", 6):
                return values;
            case ("translate", 1 or 2):
                return [1, 0, 0, 1, values[0], values.Length > 1 ? values[1] : 0];
            case ("rotate", 1 or 3):
                var radians = values[0] * Math.PI / 180;
                var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
                var (cx, cy) = values.Length == 3 ? (values[1], values[2]) : (0, 0);

                // Rotating about (cx, cy): moved there, rotated, moved back.
                return [cos, sin, -sin, cos, cx - cos * cx + sin * cy, cy - sin * cx - cos * cy];
            default:
                throw new InvalidDataException($"{name}: the transform {transform} is not one the tool reads.");
        }
    }

    [GeneratedRegex(@"^(?<kind>matrix|translate|rotate)\((?<values>[^)]*)\)$")]
    private static partial Regex TransformPattern();

    private static string Write(double[] matrix)
    {
        return string.Join(',', matrix.Select(value => Math.Round(value, 6).ToString("R", CultureInfo.InvariantCulture)));
    }

    private static double Number(XElement element, string attribute)
    {
        return element.Attribute(attribute)?.Value is { } text ? double.Parse(text.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture) : 0;
    }
}
