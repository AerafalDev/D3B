using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;

namespace D3B.Scripts.Icons;

/// <summary>The command that takes the IntelliJ Platform's icons the app draws and writes them for Avalonia (D331).</summary>
internal static class IconsCommand
{
    private const int Done = 0;
    private const int Refused = 1;
    private const int Unreachable = 2;

    // The revision of JetBrains/intellij-community the icons come from: pinned, so that writing them again changes nothing
    // unless the revision does.
    private const string Revision = "74546168a76169cc3b4ddd81e4c5d63d0e06e624";

    private const string Folder = "platform/icons/src/expui";

    // Those the app draws, by their folder and name in expui: their dark variant, <name>_dark.svg, is taken. The mockup's
    // debugger/freeze and toolwindows/dataflow carry no notice, and are not here (D331).
    private static readonly string[] _names =
    [
        "debugger/threadRunning", "debugger/watch",
        "general/add", "general/chevronDown", "general/chevronRight", "general/close", "general/collapseAll", "general/copy",
        "general/editorOnly", "general/filter", "general/groups", "general/hide", "general/history", "general/ideUpdate",
        "general/layout", "general/locked", "general/menu", "general/moreHorizontal", "general/pin", "general/reset", "general/save",
        "general/search", "general/settings", "general/user",
        "ide/externalLink",
        "nodes/dataTables", "nodes/desktop", "nodes/folder", "nodes/function", "nodes/tag", "nodes/template", "nodes/variable",
        "run/pause", "run/resume", "run/run", "run/stepOver", "run/stop",
        "status/error", "status/info", "status/success", "status/warning",
        "toolwindows/debug", "toolwindows/messages", "toolwindows/notifications", "toolwindows/problems", "toolwindows/profiler",
        "toolwindows/project", "toolwindows/run", "toolwindows/services", "toolwindows/structure", "toolwindows/web",
    ];

    public static RootCommand Create()
    {
        var revision = new Option<string>("--revision")
        {
            Description = "The revision of JetBrains/intellij-community to take the icons from.",
            DefaultValueFactory = _ => Revision,
        };
        var output = new Option<FileInfo?>("--output")
        {
            Description = @"The file to write. Defaults to src\D3B.App\Themes\IntelliJIcons.axaml in the repository.",
        };
        var check = new Option<bool>("--check")
        {
            Description = "Writes nothing: says whether the file holds what the tool would write.",
        };
        var command = new RootCommand("Takes the IntelliJ Platform's icons the app draws, checked file by file, and writes them for Avalonia (D331).")
        {
            revision,
            output,
            check,
        };

        command.SetAction((result, cancellationToken) => RunAsync(result.GetValue(revision)!, result.GetValue(output), result.GetValue(check), cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(string revision, FileInfo? output, bool check, CancellationToken cancellationToken)
    {
        var path = output?.FullName ?? Path.Combine(RepositoryRoot(), "src", "D3B.App", "Themes", "IntelliJIcons.axaml");
        var icons = new List<(string Name, SvgIcon Icon)>();
        var refused = new List<string>();

        using var http = new HttpClient();

        foreach (var name in _names)
        {
            var url = $"https://raw.githubusercontent.com/JetBrains/intellij-community/{revision}/{Folder}/{name}_dark.svg";
            string text;

            try
            {
                text = await http.GetStringAsync(new Uri(url), cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                await Console.Error.WriteLineAsync($"{name}: {url} could not be fetched{(exception.StatusCode is HttpStatusCode code ? $", {(int)code}" : "")}: {exception.Message}");

                return Unreachable;
            }

            try
            {
                icons.Add((name, SvgIcon.Read(name, text)));
            }
            catch (InvalidDataException exception)
            {
                refused.Add(exception.Message);
            }
        }

        if (refused.Count > 0)
        {
            foreach (var reason in refused)
                await Console.Error.WriteLineAsync(reason);

            await Console.Error.WriteLineAsync($"{refused.Count} icon(s) refused: nothing written.");

            return Refused;
        }

        var written = Write(revision, icons);

        if (check)
        {
            var same = File.Exists(path) && (await File.ReadAllTextAsync(path, cancellationToken)).ReplaceLineEndings() == written.ReplaceLineEndings();

            Console.WriteLine(same ? $"{path} holds the {icons.Count} icons as the tool writes them." : $"{path} differs from what the tool writes: run it without --check.");

            return same ? Done : Refused;
        }

        await File.WriteAllTextAsync(path, written, new UTF8Encoding(false), cancellationToken);
        Console.WriteLine($"{icons.Count} icons, each under the Apache 2.0 license as its file says, written to {path}.");

        return Done;
    }

    private static string Write(string revision, List<(string Name, SvgIcon Icon)> icons)
    {
        var text = new StringBuilder();

        text.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        text.AppendLine("    <!--  Written by scripts/icons.cs: run it again rather than edit this file (D331). The IntelliJ Platform's icons,");
        text.AppendLine(CultureInfo.InvariantCulture, $"         the dark variants of {Folder} in JetBrains/intellij-community at {revision},");
        text.AppendLine("         each under the Apache 2.0 license as its notice below says, which THIRD-PARTY-NOTICES.md holds whole.  -->");

        foreach (var (name, icon) in icons)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"    <!--  {name}: {icon.Notice}  -->");
            text.AppendLine(CultureInfo.InvariantCulture, $"    <DrawingGroup x:Key=\"{Key(name)}\">");

            foreach (var shape in icon.Shapes)
                WriteShape(text, shape);

            text.AppendLine("    </DrawingGroup>");
        }

        text.AppendLine("</ResourceDictionary>");

        return text.ToString();
    }

    private static void WriteShape(StringBuilder text, IconShape shape)
    {
        var fill = shape.Fill is { } brush ? $" Brush=\"{brush}\"" : "";

        text.AppendLine(CultureInfo.InvariantCulture, $"        <GeometryDrawing{fill}>");
        text.AppendLine("            <GeometryDrawing.Geometry>");

        if (shape.Transform is { } transform)
        {
            // A path geometry, unlike a stream geometry written as text, takes a transform; its rule is its own attribute.
            var rule = shape.Data.StartsWith("F0", StringComparison.Ordinal) ? "EvenOdd" : "NonZero";

            text.AppendLine(CultureInfo.InvariantCulture, $"                <PathGeometry FillRule=\"{rule}\" Figures=\"{shape.Data[2..].Trim()}\">");
            text.AppendLine("                    <PathGeometry.Transform>");
            text.AppendLine(CultureInfo.InvariantCulture, $"                        <MatrixTransform Matrix=\"{transform}\" />");
            text.AppendLine("                    </PathGeometry.Transform>");
            text.AppendLine("                </PathGeometry>");
        }
        else
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"                <StreamGeometry>{shape.Data}</StreamGeometry>");
        }

        text.AppendLine("            </GeometryDrawing.Geometry>");

        if (shape.Stroke is { } stroke)
        {
            text.AppendLine("            <GeometryDrawing.Pen>");
            text.AppendLine(CultureInfo.InvariantCulture, $"                <Pen Brush=\"{stroke}\" LineCap=\"{shape.LineCap}\" LineJoin=\"{shape.LineJoin}\" Thickness=\"{shape.StrokeWidth.ToString("R", CultureInfo.InvariantCulture)}\" />");
            text.AppendLine("            </GeometryDrawing.Pen>");
        }

        text.AppendLine("        </GeometryDrawing>");
    }

    private static string Key(string name)
    {
        // general/chevronDown is drawn as {StaticResource General.ChevronDown}.
        var (folder, icon) = (name[..name.IndexOf('/', StringComparison.Ordinal)], name[(name.IndexOf('/', StringComparison.Ordinal) + 1)..]);

        return $"{char.ToUpperInvariant(folder[0])}{folder[1..]}.{char.ToUpperInvariant(icon[0])}{icon[1..]}";
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "D3B.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Run the tool from the repository, which holds D3B.slnx, or name the file with --output.");
    }
}
