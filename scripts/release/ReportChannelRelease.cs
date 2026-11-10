using D3B.Bot.Reports;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Release;

/// <summary>Publishes the URL of the private Discord webhook that bug reports go to, as the one asset of the release <c>report-channel</c> (D410).</summary>
/// <remarks>
/// The URL is a secret, so it is never in the repository: it is read from a file the owner keeps elsewhere, checked to be a Discord webhook's, and never
/// printed. Unlike a client build's files, which are never replaced (D33), this asset is replaced on purpose: the webhook is revoked and a new one published,
/// so that reports go to the new channel without a new app. Whoever downloads the app can read the asset: D410 says so.
/// </remarks>
internal static class ReportChannelRelease
{
    public static int Run(FileInfo file, string repository, bool publish)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"{file.FullName} does not exist.");

            return 2;
        }

        if (!ReportWebhook.TryParse(File.ReadAllText(file.FullName), out var webhook))
        {
            Console.Error.WriteLine($"{file.Name} does not hold the URL of a Discord webhook and nothing else.");

            return 1;
        }

        // The asset's name is fixed: the app reads it by name, whatever the file the owner keeps it in is called.
        var directory = Path.Combine(Path.GetTempPath(), "d3b-report-channel-" + Guid.NewGuid().ToString("N"));
        var asset = Path.Combine(directory, ReportChannel.AssetName);

        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(asset, webhook!.Url.ToString());

            var published = GitHubCli.Assets(repository, ReportChannel.ReleaseTag);
            var arguments = published is null
                ? new List<string> { "release", "create", ReportChannel.ReleaseTag, asset, "--repo", repository, "--title", "Report channel", "--notes", "Where the app sends the bug reports a player chooses to send (D410): the URL of a private webhook, replaced when it is revoked. Not a release of the app.", "--latest=false" }
                : ["release", "upload", ReportChannel.ReleaseTag, asset, "--repo", repository, "--clobber"];

            Console.WriteLine($"Webhook {webhook}: {(published is null ? "release report-channel does not exist, it is to be created" : "release report-channel exists, its webhook.txt is to be replaced")} on {repository}.");

            if (!publish)
            {
                Console.WriteLine("Dry run, nothing changed on GitHub. --publish runs it.");

                return 0;
            }

            var (exitCode, output, error) = GitHubCli.Run(arguments);

            Console.Write(ReportWebhook.Hide(output));

            if (exitCode is not 0)
            {
                Console.Error.WriteLine($"gh {arguments[0]} {arguments[1]} failed: {ReportWebhook.Hide(error).Trim()}");

                return 1;
            }

            Console.WriteLine($"Published to release {ReportChannel.ReleaseTag} of {repository}: the app reads it at the next report.");

            return 0;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
