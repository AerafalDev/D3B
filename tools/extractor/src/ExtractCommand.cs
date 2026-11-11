using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using D3B.Extractor.Code;
using D3B.Extractor.Texts;
using D3B.Extractor.World;

namespace D3B.Extractor;

/// <summary>The command that extracts the installed client's game data into the data folder of its build.</summary>
internal static class ExtractCommand
{
    /// <summary>The exit code when the extraction succeeded.</summary>
    public const int Done = 0;

    /// <summary>The exit code when the client was found but could not be extracted.</summary>
    public const int Failed = 1;

    /// <summary>The exit code when no client was found.</summary>
    public const int NoClient = 2;

    /// <summary>The exit code when the command line was not understood.</summary>
    public const int Misused = 3;

    /// <summary>Gets the extractor's version and commit, which every manifest it writes records.</summary>
    public static string Version => typeof(ExtractCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Runs the command line <paramref name="args"/>, writing to <paramref name="output"/> and <paramref name="error"/>.</summary>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var parsed = Create(output, error).Parse(args);
        var code = parsed.Invoke(new InvocationConfiguration { Output = output, Error = error });

        // System.CommandLine exits with 1 for a command line it does not understand, which a script would take for a failed extraction.
        return parsed.Errors.Count > 0 ? Misused : code;
    }

    private static RootCommand Create(TextWriter output, TextWriter error)
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = $@"The client's folder. Defaults to ${ClientInstall.Variable}, else %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var target = new Option<DirectoryInfo?>("--output")
        {
            Description = "The data folder to write. Defaults to artifacts/data/<build> under the current folder.",
        };
        var command = new RootCommand("Extracts the game data of the installed Dofus 3 client into a JSON data folder.")
        {
            client,
            target,
        };

        command.SetAction(result =>
        {
            return Execute(result.GetValue(client)?.FullName, result.GetValue(target)?.FullName, output, error);
        });

        return command;
    }

    private static int Execute(string? clientFolder, string? target, TextWriter output, TextWriter error)
    {
        ClientInstall client;

        try
        {
            client = ClientInstall.Locate(clientFolder);
        }
        catch (DirectoryNotFoundException exception)
        {
            error.WriteLine(exception.Message);

            return NoClient;
        }

        try
        {
            return Extract(client, target, output);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error.WriteLine($"The extraction failed: {exception.Message}");

            return Failed;
        }
    }

    private static int Extract(ClientInstall client, string? target, TextWriter output)
    {
        var stopwatch = Stopwatch.StartNew();
        var build = ClientBuild.Read(client);
        var path = Path.GetFullPath(target ?? Path.Combine("artifacts", "data", build.Version));

        output.WriteLine($"Client: {client.Root}, version {build}, extractor {Version}");

        // Everything is read before the target is replaced, so that a client the extractor misreads leaves it as it was.
        var folder = DataFolder.Create(path);
        var texts = TextFiles.Open(client);

        using var code = ClientCode.Open(client);

        WorldTables.Extract(client, texts, code, folder, output);
        folder.Complete(build.Version, Version);

        output.WriteLine($"Data: {path}, format {DataFolder.Format}, in {stopwatch.Elapsed.TotalSeconds:F1} s");

        return Done;
    }
}
