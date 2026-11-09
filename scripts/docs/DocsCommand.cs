using System.CommandLine;

namespace D3B.Scripts.Docs;

/// <summary>The commands that look after the repository's Markdown files.</summary>
internal static class DocsCommand
{
    private const int Done = 0;
    private const int Broken = 1;
    private const int Unreadable = 2;

    public static RootCommand Create()
    {
        var root = new Option<DirectoryInfo>("--root")
        {
            Description = "The repository's root. Defaults to the current directory.",
            DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory()),
            Recursive = true,
        };
        var check = new Command("check", "Finds the links of the Markdown files, tracked or not ignored, whose file or heading does not exist. The default.");
        var command = new RootCommand("Looks after the repository's Markdown files: their links and headings.")
        {
            root,
            check,
        };

        command.SetAction(result => Check(result.GetValue(root)!.FullName));
        check.SetAction(result => Check(result.GetValue(root)!.FullName));

        return command;
    }

    private static int Check(string root)
    {
        try
        {
            var files = LinkChecker.MarkdownFiles(root);
            var broken = LinkChecker.Check(root, files);

            foreach (var link in broken)
                Console.WriteLine($"{link.File}:{link.Line}: {link.Target}: {link.Reason}");

            Console.WriteLine(broken.Count == 0 ? $"{files.Count} Markdown files, every link leads somewhere." : $"{broken.Count} broken links in {files.Count} Markdown files.");

            return broken.Count == 0 ? Done : Broken;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);

            return Unreadable;
        }
    }
}
