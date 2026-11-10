using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using D3B.Bot.Reports;
using D3B.Network.Captures;

namespace D3B.Scripts.Report;

/// <summary>The command that unpacks and describes a bug report, which anyone could have posted: bounded, checked, never run.</summary>
internal static class ReportCommand
{
    public static RootCommand Create()
    {
        var file = new Argument<FileInfo>("report")
        {
            Description = "The .d3breport file.",
        };
        var output = new Option<DirectoryInfo?>("--out")
        {
            Description = "Where to unpack the report. Defaults to artifacts/reports/<code> in the repository, which Git ignores.",
        };
        var replay = new Option<bool>("--replay")
        {
            Description = "Also runs scripts/fights.cs on the report's capture, against the pack of the client build the report names.",
        };
        var command = new RootCommand("Unpacks a bug report, checks it against its manifest and describes it, the player's message first.")
        {
            file,
            output,
            replay,
        };

        command.SetAction(result => Run(result.GetValue(file)!, result.GetValue(output)?.FullName, result.GetValue(replay)));

        return command;
    }

    private static int Run(FileInfo file, string? output, bool replay)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"{file.FullName} does not exist.");

            return 2;
        }

        ReportBundle bundle;

        try
        {
            using var stream = file.OpenRead();

            bundle = ReportReader.Read(stream);
        }
        catch (ReportFormatException exception)
        {
            Console.Error.WriteLine($"{file.Name} is not a report to trust: {Printable(exception.Message)}");

            return 1;
        }

        var manifest = bundle.Manifest;
        var folder = output ?? Path.Combine(RepositoryRoot(), "artifacts", "reports", manifest.Code);

        // The message first: it is what the player wrote, and what the rest is read against. It is text, never rendered or run.
        if (bundle.Message is { } message)
        {
            Console.WriteLine("Message of the player:");
            Console.WriteLine(Indented(Printable(message.ReplaceLineEndings("\n"))));
        }
        else
        {
            Console.WriteLine("The player wrote no message.");
        }

        Console.WriteLine();
        Console.WriteLine($"Report {manifest.Code}, made {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}: {manifest.Area}");
        Console.WriteLine($"  app {Printable(manifest.App)}, client {Printable(manifest.Client)}, pack {Printable(manifest.Pack ?? "unknown")}, mapping {Printable(manifest.Mapping ?? "unknown")}");

        if (manifest.Character is { } character)
            Console.WriteLine($"  character {character.Id}, breed {character.Breed?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, level {character.Level?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");

        if (manifest.CaptureStartedAt is { } started)
            Console.WriteLine($"  capture started {started:yyyy-MM-dd HH:mm:ss zzz}: the journal's times join the capture's from there");

        foreach (var reported in manifest.Files)
            Console.WriteLine($"  {Printable(reported.Name),-28} {reported.Size,12:N0} bytes  sha256 {reported.Sha256[..12]}");

        bundle.WriteTo(folder);
        Console.WriteLine();
        Console.WriteLine($"Unpacked in {folder}");

        if (bundle.Files.TryGetValue(ReportFiles.Capture, out var capture))
            DescribeCapture(capture);

        var path = Path.Combine(folder, ReportFiles.Capture);

        if (!replay || !bundle.Files.ContainsKey(ReportFiles.Capture))
        {
            Console.WriteLine();
            Console.WriteLine($"To replay its fights: dotnet run --file scripts/fights.cs -- \"{path}\" --client-version {Printable(manifest.Client)}");

            return 0;
        }

        return Replay(path, manifest.Client);
    }

    private static void DescribeCapture(byte[] capture)
    {
        Console.WriteLine();

        try
        {
            using var reader = new CaptureReader(new MemoryStream(capture));
            var records = reader.ReadAll().ToList();
            var last = records.Count > 0 ? records[^1].Offset : TimeSpan.Zero;

            Console.WriteLine($"Capture of client {Printable(reader.Header.ClientVersion)}, started {reader.Header.StartedAt:yyyy-MM-dd HH:mm:ss zzz}: {records.Count:N0} records over {last:hh\\:mm\\:ss}, {records.Sum(record => (long)record.Data.Length):N0} bytes{(reader.IsTruncated ? ", cut short" : string.Empty)}");
        }
        catch (CaptureFormatException exception)
        {
            Console.WriteLine($"The capture does not read: {Printable(exception.Message)}");
        }
    }

    private static int Replay(string capture, string client)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = RepositoryRoot(), UseShellExecute = false };

        foreach (var argument in new[] { "run", "--file", Path.Combine("scripts", "fights.cs"), "--", capture, "--client-version", client })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;

        process.WaitForExit();

        return process.ExitCode;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "D3B.slnx")))
                return directory.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    // Text from a stranger: control and formatting characters (a terminal escape, a right-to-left override) are shown as a question mark.
    private static string Printable(string text)
    {
        return new string([.. text.Select(character => character is '\n' or '\t' || (!char.IsControl(character) && char.GetUnicodeCategory(character) is not UnicodeCategory.Format) ? character : '?')]);
    }

    private static string Indented(string text)
    {
        return string.Join('\n', text.ReplaceLineEndings("\n").Split('\n').Select(line => "  | " + line));
    }
}
