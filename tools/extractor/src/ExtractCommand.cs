using System.CommandLine;
using System.Reflection;

namespace D3B.Extractor;

/// <summary>The command that extracts the installed client's game data into the data folder of its build.</summary>
internal static class ExtractCommand
{
    /// <summary>The exit code when the extraction succeeded.</summary>
    public const int Done = 0;

    /// <summary>The exit code when the command line was not understood.</summary>
    public const int Misused = 3;

    /// <summary>Gets the extractor's version and commit, which every manifest it writes records.</summary>
    public static string Version => typeof(ExtractCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Runs the command line <paramref name="args"/>, writing to <paramref name="output"/> and <paramref name="error"/>.</summary>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var parsed = Create(output).Parse(args);
        var code = parsed.Invoke(new InvocationConfiguration { Output = output, Error = error });

        // System.CommandLine exits with 1 for a command line it does not understand, which a script would take for a failed extraction.
        return parsed.Errors.Count > 0 ? Misused : code;
    }

    private static RootCommand Create(TextWriter output)
    {
        var command = new RootCommand("Extracts the game data of the installed Dofus 3 client into a JSON data folder.");

        command.SetAction(_ =>
        {
            output.WriteLine($"D3B extractor {Version}");

            return Done;
        });

        return command;
    }
}
