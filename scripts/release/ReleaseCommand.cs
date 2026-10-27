using System.CommandLine;
using System.Security.Cryptography;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Releases;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Release;

/// <summary>The command that publishes the pack of a client build to the release of that build.</summary>
internal static class ReleaseCommand
{
    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client whose build's pack to publish. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var version = new Option<string?>("--client-version")
        {
            Description = "The client version whose pack to publish, instead of the installed client's.",
        };
        var packs = new Option<DirectoryInfo?>("--packs")
        {
            Description = @"The directory packs are kept in, one directory per client version. Defaults to %LOCALAPPDATA%\D3B\data, where the extraction tool writes.",
        };
        var repository = new Option<string>("--repository")
        {
            Description = "The repository whose releases ship the packs, as owner/name.",
            DefaultValueFactory = _ => ClientRelease.DefaultRepository,
        };
        var publish = new Option<bool>("--publish")
        {
            Description = "Creates the release, or uploads the assets it lacks. Without it, nothing changes on GitHub.",
        };
        var command = new RootCommand("Publishes the pack of a Dofus 3 client build to the GitHub release of that build.")
        {
            client,
            version,
            packs,
            repository,
            publish,
        };

        command.SetAction(result => Run(
            result.GetValue(client),
            result.GetValue(version),
            result.GetValue(packs)?.FullName ?? PackLocations.DefaultRoot,
            result.GetValue(repository)!,
            result.GetValue(publish)));

        return command;
    }

    private static int Run(DirectoryInfo? clientDirectory, string? version, string root, string repository, bool publish)
    {
        if (!ClientRelease.IsRepository(repository))
        {
            Console.Error.WriteLine($"'{repository}' does not name a GitHub repository as owner/name.");

            return 1;
        }

        ClientBuild build;

        try
        {
            build = version is null ? ClientBuild.Read(ClientInstall.Locate(clientDirectory).Root) : new ClientBuild(version, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        var path = PackLocations.PathOf(root, build);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"No pack for client {build.Version} at {path}. Extract it first: dotnet run --file scripts/extract.cs");

            return 2;
        }

        try
        {
            return Release(build, path, repository, publish);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"The release failed: {exception.Message}");

            return 1;
        }
    }

    private static int Release(ClientBuild build, string path, string repository, bool publish)
    {
        PackInfo info;

        // Opening checks the format and that the pack is whole; the version check catches a pack filed under another build.
        using (var pack = GamePack.Open(path))
            info = pack.Info;

        if (info.ClientVersion != build.Version)
            throw new InvalidDataException($"{path} was extracted from client {info.ClientVersion}, not {build.Version}.");

        var asset = PackLocations.FileName;
        var checksumName = ClientRelease.ChecksumNameOf(asset);
        var checksumPath = Path.Combine(Path.GetDirectoryName(path)!, checksumName);
        var tag = ClientRelease.TagOf(build);
        byte[] hash;
        long length;

        using (var stream = File.OpenRead(path))
        {
            hash = SHA256.HashData(stream);
            length = stream.Length;
        }

        var sha256 = Convert.ToHexStringLower(hash);

        // Beside the pack, outside the repository: it is uploaded as the pack's checksum asset, never committed.
        File.WriteAllText(checksumPath, ClientRelease.FormatChecksum(hash, asset));

        Console.WriteLine($"Pack: {path}, {length:N0} bytes, format {GamePack.FormatVersion}, client {info.ClientVersion}, extractor {info.ExtractorVersion}");
        Console.WriteLine($"Fingerprint: {info.Fingerprint}");
        Console.WriteLine($"SHA-256: {sha256}, written to {checksumPath}");

        var assets = GitHubCli.Assets(repository, tag);
        List<string> arguments;

        if (assets is null)
        {
            arguments = ["release", "create", tag, path, checksumPath, "--repo", repository, "--title", $"Client {build.Version}", "--notes", Notes(build, info, sha256), "--latest=false"];
            Console.WriteLine($"Release {tag} of {repository} does not exist: it is to be created with {asset} and {checksumName}.");
        }
        else if (assets.TryGetValue(asset, out var digest))
        {
            if (!assets.ContainsKey(checksumName))
                throw new InvalidOperationException($"Release {tag} holds {asset} without {checksumName}: hosts cannot check it. Fix the release by hand.");

            if (digest == $"sha256:{sha256}")
            {
                Console.WriteLine($"Up to date: release {tag} of {repository} already holds this {asset}.");

                return 0;
            }

            // D24: hosts that downloaded the published pack keep it, so a replacement would leave two packs under one name.
            throw new InvalidOperationException(
                $"Release {tag} already holds another {asset} ({digest ?? "no digest"}). A published pack is never replaced (D24): "
                + "if the client changed without changing its version, a build's identity must first include its fingerprint.");
        }
        else
        {
            if (assets.ContainsKey(checksumName))
                throw new InvalidOperationException($"Release {tag} holds {checksumName} without {asset}. Fix the release by hand.");

            arguments = ["release", "upload", tag, path, checksumPath, "--repo", repository];
            Console.WriteLine($"Release {tag} of {repository} exists without {asset}: {asset} and {checksumName} are to be uploaded to it.");
        }

        if (!publish)
        {
            Console.WriteLine($"Dry run, nothing changed on GitHub. --publish runs: gh {string.Join(' ', arguments.Select(Quote))}");

            return 0;
        }

        var (exitCode, output, error) = GitHubCli.Run(arguments);

        Console.Write(output);

        if (exitCode is not 0)
            throw new InvalidOperationException($"gh {arguments[0]} {arguments[1]} failed: {error.Trim()}");

        var published = GitHubCli.Assets(repository, tag);

        if (published is null || !published.TryGetValue(asset, out var uploaded) || uploaded != $"sha256:{sha256}" || !published.ContainsKey(checksumName))
            throw new InvalidOperationException($"Release {tag} does not hold the {asset} just uploaded, by GitHub's digest: check it by hand.");

        Console.WriteLine($"Published {asset} and {checksumName} to release {tag} of {repository}, SHA-256 {sha256}.");

        return 0;
    }

    private static string Notes(ClientBuild build, PackInfo info, string sha256)
    {
        var built = info.ClientBuildDate is { } date ? $", built on {date:yyyy-MM-dd}" : string.Empty;

        return $"""
            Game data of the Dofus 3 client {build.Version}{built}, for D3B. A host reading pack format N downloads `data-N.pack`; each asset's SHA-256 is in the asset of the same name ending in `.sha256`.

            `{PackLocations.FileName}`: SHA-256 `{sha256}`, extracted by D3B.Data {info.ExtractorVersion} from client files of fingerprint `{info.Fingerprint}`.

            Derived from the official client by `scripts/extract.cs`. D3B is not affiliated with Ankama.
            """;
    }

    private static string Quote(string argument)
    {
        // The notes span several lines: their first stands for them.
        if (argument.Contains('\n', StringComparison.Ordinal))
            return $"\"{argument.Split('\n')[0]}...\"";

        return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
    }
}
