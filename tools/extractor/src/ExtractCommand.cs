using System.CommandLine;
using System.Reflection;

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
        var command = new RootCommand("Extracts the game data of the installed Dofus 3 client into a JSON data folder.")
        {
            client,
        };

        command.SetAction(result =>
        {
            return Execute(result.GetValue(client)?.FullName, output, error);
        });

        return command;
    }

    private static int Execute(string? clientFolder, TextWriter output, TextWriter error)
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
            return Extract(client, output);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error.WriteLine($"The extraction failed: {exception.Message}");

            return Failed;
        }
    }

    private static int Extract(ClientInstall client, TextWriter output)
    {
        var build = ClientBuild.Read(client);

        output.WriteLine($"Client: {client.Root}, version {build}, extractor {Version}");

        return Done;
    }
}
