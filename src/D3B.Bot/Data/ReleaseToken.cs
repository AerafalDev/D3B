using System.ComponentModel;
using System.Diagnostics;

namespace D3B.Bot.Data;

/// <summary>The GitHub token a release download sends while the repository is private (D4, D32).</summary>
/// <remarks>
/// <c>Releases:Token</c> when the configuration sets it, else the token of the GitHub CLI's logged-in account, which the
/// owner already has: nothing else to configure. It is asked for only when a pack must be downloaded, and never logged.
/// Without either, a download goes without a token, which a public repository accepts.
/// </remarks>
internal sealed class ReleaseToken
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly string? _configured;
    private readonly string _cli;

    public ReleaseToken(string? configured, string cli = "gh")
    {
        _configured = configured;
        _cli = cli;
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        return _configured ?? await AskCliAsync(cancellationToken);
    }

    private async Task<string?> AskCliAsync(CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_cli)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "auth", "token", "--hostname", "github.com" },
        };

        // The CLI would print these instead of its logged-in account's token, which is the one D32 asks for.
        start.Environment.Remove("GH_TOKEN");
        start.Environment.Remove("GITHUB_TOKEN");

        Process? process;

        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            // The CLI is not installed.
            return null;
        }

        if (process is null)
            return null;

        using (process)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(_timeout);

            try
            {
                // Both streams are read, so that the CLI never blocks on a full pipe.
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var error = process.StandardError.ReadToEndAsync(timeout.Token);

                await process.WaitForExitAsync(timeout.Token);
                await error;

                var token = (await output).Trim();

                return process.ExitCode is 0 && token.Length > 0 && !token.Any(char.IsWhiteSpace) ? token : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(true);

                return null;
            }
        }
    }
}
