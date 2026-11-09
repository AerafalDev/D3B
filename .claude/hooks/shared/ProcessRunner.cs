using System.Diagnostics;

namespace D3B.Hooks;

/// <summary>Runs the commands the hooks need, <c>git</c> and <c>dotnet</c>, and waits for them.</summary>
internal static class ProcessRunner
{
    public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} did not start.");
        process.StandardInput.Close();
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output + error.GetAwaiter().GetResult());
    }
}
