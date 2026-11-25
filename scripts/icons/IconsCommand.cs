using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;

namespace D3B.Scripts.Icons;

/// <summary>The command that takes the Lucide icons the app draws, as the next line's, and writes them for Avalonia (D331, D532).</summary>
internal static class IconsCommand
{
    private const int Done = 0;
    private const int Refused = 1;
    private const int Unreachable = 2;

    // The version of lucide-icons/lucide the icons come from, the next line's own (lucide-react 1.72.0): pinned, so that
    // writing them again changes nothing unless the version does.
    private const string Version = "1.72.0";

    // next draws them in the grey of the text around it and with a stroke of 1.5 on Lucide's grid of 24; colour only for a
    // problem or an offer (docs/shell.md on the next line). The grey is the one Icon repaints with a tint.
    private const string Neutral = "#FFCED0D6";
    private const double Stroke = 1.5;

    // The app's keys, kept from its IntelliJ icons so that no view changes, each drawn now by a Lucide icon, with its colour
    // when it says a state.
    private static readonly (string Key, string Lucide, string? Tone)[] _icons =
    [
        ("Actions.NewFolder", "folder-plus", null),
        ("Actions.SelectAll", "square-dashed", null),
        ("Actions.StopWatch", "timer", null),
        ("Debugger.ThreadRunning", "activity", null),
        ("Debugger.Watch", "eye", null),
        ("General.Add", "plus", null),
        ("General.ChevronDown", "chevron-down", null),
        ("General.ChevronRight", "chevron-right", null),
        ("General.ChevronUp", "chevron-up", null),
        ("General.Close", "x", null),
        ("General.CollapseAll", "chevrons-down-up", null),
        ("General.Copy", "copy", null),
        ("General.Delete", "trash", null),
        ("General.Edit", "pencil", null),
        ("General.EditorOnly", "app-window", null),
        ("General.ExpandAll", "chevrons-up-down", null),
        ("General.Export", "file-output", null),
        ("General.Filter", "funnel", null),
        ("General.Groups", "users", null),
        ("General.Hide", "eye-off", null),
        ("General.History", "clock-arrow-left", null),
        ("General.IdeUpdate", "circle-arrow-down", "#FFC77D55"),
        ("General.Import", "file-input", null),
        ("General.Language", "languages", null),
        ("General.Layout", "layout-dashboard", null),
        ("General.ListFiles", "list", null),
        ("General.Locate", "locate", null),
        ("General.Locked", "lock", null),
        ("General.Menu", "menu", null),
        ("General.MoreHorizontal", "ellipsis", null),
        ("General.MoveDown", "arrow-down", null),
        ("General.MoveUp", "arrow-up", null),
        ("General.Open", "external-link", null),
        ("General.Pin", "pin", null),
        ("General.QuestionMark", "circle-question-mark", null),
        ("General.Refresh", "refresh-cw", null),
        ("General.Remove", "minus", null),
        ("General.Reset", "rotate-ccw", null),
        ("General.Save", "save", null),
        ("General.ScrollDown", "arrow-down-to-line", null),
        ("General.Search", "search", null),
        ("General.Settings", "settings", null),
        ("General.User", "user", null),
        ("General.Vcs", "route", null),
        ("Ide.ExternalLink", "external-link", null),
        ("Layout.PanelBottom", "panel-bottom", null),
        ("Layout.PanelLeft", "panel-left", null),
        ("Layout.PanelRight", "panel-right", null),
        ("Nodes.DataTables", "table", null),
        ("Nodes.Desktop", "monitor", null),
        ("Nodes.Folder", "folder", null),
        ("Nodes.Function", "square-function", null),
        ("Nodes.SecurityRole", "shield", null),
        ("Nodes.Tag", "hash", null),
        ("Nodes.Template", "workflow", null),
        ("Nodes.Variable", "variable", null),
        ("Run.Pause", "pause", null),
        ("Run.Resume", "play", "#FF3FB950"),
        ("Run.Run", "play", "#FF3FB950"),
        ("Run.StepOver", "step-forward", null),
        ("Run.Stop", "square", "#FFF85149"),
        ("Status.Error", "circle-exclamation-point", "#FFDB5C5C"),
        ("Status.Info", "info", null),
        ("Status.InfoOutline", "info", null),
        ("Status.Success", "circle-check", "#FF3FB950"),
        ("Status.Warning", "triangle-exclamation-point", "#FFF2C55C"),
        ("Toolwindows.Debug", "bug", null),
        ("Toolwindows.Hierarchy", "list-tree", null),
        ("Toolwindows.Messages", "scroll-text", null),
        ("Toolwindows.Notifications", "bell", null),
        ("Toolwindows.Palette", "paintbrush", null),
        ("Toolwindows.Problems", "octagon-exclamation-point", null),
        ("Toolwindows.Profiler", "chart-column", null),
        ("Toolwindows.Project", "library-big", null),
        ("Toolwindows.Run", "bot", null),
        ("Toolwindows.Services", "blocks", null),
        ("Toolwindows.Structure", "list", null),
        ("Toolwindows.Web", "map", null),
    ];

    public static RootCommand Create()
    {
        var version = new Option<string>("--version")
        {
            Description = "The version of lucide-icons/lucide to take the icons from.",
            DefaultValueFactory = _ => Version,
        };
        var output = new Option<FileInfo?>("--output")
        {
            Description = @"The file to write. Defaults to src\D3B.App\Themes\Icons.axaml in the repository.",
        };
        var check = new Option<bool>("--check")
        {
            Description = "Writes nothing: says whether the file holds what the tool would write.",
        };
        var command = new RootCommand("Takes the Lucide icons the app draws, as the next line's, under Lucide's ISC license, and writes them for Avalonia (D532).")
        {
            version,
            output,
            check,
        };

        command.SetAction((result, cancellationToken) => RunAsync(result.GetValue(version)!, result.GetValue(output), result.GetValue(check), cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(string version, FileInfo? output, bool check, CancellationToken cancellationToken)
    {
        var path = output?.FullName ?? Path.Combine(RepositoryRoot(), "src", "D3B.App", "Themes", "Icons.axaml");
        var icons = new Dictionary<string, LucideIcon>(StringComparer.Ordinal);
        var refused = new List<string>();

        using var http = new HttpClient();

        // The license is the repository's, one for every icon: the tool takes nothing unless it is Lucide's ISC license.
        var license = await FetchAsync(http, $"https://raw.githubusercontent.com/lucide-icons/lucide/{version}/LICENSE", cancellationToken);

        if (license is null)
            return Unreachable;

        if (!license.StartsWith("ISC License", StringComparison.Ordinal) || !license.Contains("Lucide Icons and Contributors", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync($"lucide-icons/lucide {version}: its license is not the ISC license of Lucide's contributors; nothing written.");

            return Refused;
        }

        foreach (var name in _icons.Select(icon => icon.Lucide).Distinct(StringComparer.Ordinal))
        {
            var text = await FetchAsync(http, $"https://raw.githubusercontent.com/lucide-icons/lucide/{version}/icons/{name}.svg", cancellationToken);

            if (text is null)
                return Unreachable;

            try
            {
                icons[name] = LucideIcon.Read(name, text);
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

        var written = Write(version, icons);

        if (check)
        {
            var same = File.Exists(path) && (await File.ReadAllTextAsync(path, cancellationToken)).ReplaceLineEndings() == written.ReplaceLineEndings();

            Console.WriteLine(same ? $"{path} holds the {_icons.Length} icons as the tool writes them." : $"{path} differs from what the tool writes: run it without --check.");

            return same ? Done : Refused;
        }

        await File.WriteAllTextAsync(path, written, new UTF8Encoding(false), cancellationToken);
        Console.WriteLine($"{_icons.Length} icons from {icons.Count} of Lucide's, under its ISC license, written to {path}.");

        return Done;
    }

    private static async Task<string?> FetchAsync(HttpClient http, string url, CancellationToken cancellationToken)
    {
        try
        {
            return await http.GetStringAsync(new Uri(url), cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            await Console.Error.WriteLineAsync($"{url} could not be fetched{(exception.StatusCode is HttpStatusCode code ? $", {(int)code}" : "")}: {exception.Message}");

            return null;
        }
    }

    private static string Write(string version, Dictionary<string, LucideIcon> icons)
    {
        var text = new StringBuilder();

        text.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        text.AppendLine("    <!--  Written by scripts/icons.cs: run it again rather than edit this file (D331, D532). Lucide's icons, as the next");
        text.AppendLine(CultureInfo.InvariantCulture, $"         line draws them, from lucide-icons/lucide {version}, under Lucide's ISC license, which THIRD-PARTY-NOTICES.md");
        text.AppendLine("         holds whole; each key is the app's own, drawn on a grid of 16, Lucide's 24 scaled.  -->");

        foreach (var (key, lucide, tone) in _icons)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"    <!--  {lucide}  -->");
            text.AppendLine(CultureInfo.InvariantCulture, $"    <DrawingGroup x:Key=\"{key}\">");

            foreach (var outline in icons[lucide].Outlines)
                WriteShape(text, outline, tone ?? Neutral);

            text.AppendLine("    </DrawingGroup>");
        }

        text.AppendLine("</ResourceDictionary>");

        return text.ToString();
    }

    private static void WriteShape(StringBuilder text, string outline, string stroke)
    {
        // Lucide's grid of 24 drawn on the app's of 16: the outline scaled by two thirds, the pen, which is not, given in the
        // grid of 16 already.
        var rule = outline.StartsWith("F0", StringComparison.Ordinal) ? "EvenOdd" : "NonZero";

        text.AppendLine("        <GeometryDrawing>");
        text.AppendLine("            <GeometryDrawing.Geometry>");
        text.AppendLine(CultureInfo.InvariantCulture, $"                <PathGeometry FillRule=\"{rule}\" Figures=\"{outline[2..].Trim()}\">");
        text.AppendLine("                    <PathGeometry.Transform>");
        text.AppendLine("                        <MatrixTransform Matrix=\"0.666667,0,0,0.666667,0,0\" />");
        text.AppendLine("                    </PathGeometry.Transform>");
        text.AppendLine("                </PathGeometry>");
        text.AppendLine("            </GeometryDrawing.Geometry>");
        text.AppendLine("            <GeometryDrawing.Pen>");
        text.AppendLine(CultureInfo.InvariantCulture, $"                <Pen Brush=\"{stroke}\" LineCap=\"Round\" LineJoin=\"Round\" Thickness=\"{(Stroke * 16 / 24).ToString("R", CultureInfo.InvariantCulture)}\" />");
        text.AppendLine("            </GeometryDrawing.Pen>");
        text.AppendLine("        </GeometryDrawing>");
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
