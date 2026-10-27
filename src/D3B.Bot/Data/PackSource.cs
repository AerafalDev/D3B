using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Releases;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Data;

/// <summary>The pack of the installed client's build: kept on this machine, else downloaded from its release (D24).</summary>
/// <remarks>
/// <para>
/// A pack extracted on this machine is found first, so a host works offline. Otherwise <see cref="EnsureAsync"/>
/// downloads the release's, checks it against its SHA-256 and that it was extracted from this build, then moves it into
/// place. The pack of another build is never used: the data, and from step 3 the protocol mapping, must match the client.
/// </para>
/// <para>
/// Every failure is a <see cref="DataUnavailableException"/> whose message says which build lacks its pack and how to
/// extract it.
/// </para>
/// </remarks>
public sealed class PackSource
{
    private readonly DataSettings _settings;
    private readonly ReleaseDownloader _downloader;
    private readonly ReleaseToken _token;
    private readonly ILogger _log;
    private readonly Lazy<ClientBuild> _build;

    /// <summary>Gets the build of the installed client.</summary>
    /// <exception cref="DataUnavailableException">No client is installed where the configuration says.</exception>
    public ClientBuild Build => _build.Value;

    /// <summary>Gets where the pack of the installed build is kept.</summary>
    /// <exception cref="DataUnavailableException">No client is installed where the configuration says.</exception>
    public string PackPath => PackLocations.PathOf(_settings.PacksDirectory, Build);

    internal PackSource(DataSettings settings, ReleaseDownloader downloader, ReleaseToken token, ILogger<PackSource> log)
    {
        _settings = settings;
        _downloader = downloader;
        _token = token;
        _log = log;

        // Publication only: a failed read is not remembered, so that a client installed meanwhile is found.
        _build = new Lazy<ClientBuild>(ReadBuild, LazyThreadSafetyMode.PublicationOnly);
    }

    private static void Check(string path, ClientBuild build)
    {
        using var pack = GamePack.Open(path);

        if (pack.Info.ClientVersion != build.Version)
            throw new PackFormatException($"the release's {PackLocations.FileName} was extracted from client {pack.Info.ClientVersion}.");
    }

    /// <summary>Makes sure the pack of the installed build is on this machine, downloading it when it is not.</summary>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>Whether this call downloaded the pack, rather than finding it in place.</returns>
    /// <exception cref="DataUnavailableException">No client is installed, or its build's pack is neither here nor downloadable.</exception>
    public async Task<bool> EnsureAsync(CancellationToken cancellationToken)
    {
        var build = Build;
        var path = PackPath;

        if (File.Exists(path))
            return false;

        var tag = ClientRelease.TagOf(build);
        var token = await _token.GetAsync(cancellationToken);

        DataLog.Downloading(_log, build.Version, _settings.PacksDirectory, PackLocations.FileName, tag, _downloader.Repository, token is null ? "without a token" : "with a token");

        try
        {
            var hash = await _downloader.DownloadAsync(tag, PackLocations.FileName, path, token, file => Check(file, build), cancellationToken);

            // Null when a pack landed meanwhile, such as one the extraction tool wrote: Open checks it like any other.
            if (hash is null)
                return false;

            DataLog.Downloaded(_log, path, hash);

            return true;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or UnauthorizedAccessException)
        {
            // Without a token the repository looks empty while it is private; say where one comes from.
            var hint = token is null ? $" A private repository needs a token: {DataSettings.TokenKey}, or the GitHub CLI's once logged in with gh auth login." : string.Empty;

            throw new DataUnavailableException($"No pack for client {build.Version}: none at {path}, and the download from release {tag} failed: {exception.Message}{hint} {ExtractHint(false)}", exception);
        }
    }

    /// <summary>Opens the pack of the installed build.</summary>
    /// <returns>The open pack, which the caller disposes.</returns>
    /// <exception cref="DataUnavailableException">No client is installed, or its build's pack is missing, unreadable or of another build.</exception>
    public GamePack Open()
    {
        var build = Build;
        var path = PackPath;

        if (!File.Exists(path))
            throw new DataUnavailableException($"No pack for client {build.Version}: none at {path}. {ExtractHint(false)}");

        GamePack pack;

        try
        {
            pack = GamePack.Open(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DataUnavailableException($"The pack of client {build.Version} cannot be read: {exception.Message} {ExtractHint(true)}", exception);
        }

        if (pack.Info.ClientVersion != build.Version)
        {
            var other = pack.Info.ClientVersion;

            pack.Dispose();

            throw new DataUnavailableException($"The pack in {path} was extracted from client {other}, not from {build.Version}, the one installed. {ExtractHint(true)}");
        }

        return pack;
    }

    private ClientBuild ReadBuild()
    {
        try
        {
            return ClientBuild.Read(_settings.ClientDirectory);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new DataUnavailableException($"No client build in {_settings.ClientDirectory}: {exception.Message} {DataSettings.ClientDirectoryKey} names the client's installation directory.", exception);
        }
    }

    private string ExtractHint(bool force)
    {
        var options = new List<string>();

        if (!string.Equals(_settings.ClientDirectory, DataSettings.DefaultClientDirectory, StringComparison.OrdinalIgnoreCase))
            options.Add($"--client \"{_settings.ClientDirectory}\"");

        if (!string.Equals(_settings.PacksDirectory, PackLocations.DefaultRoot, StringComparison.OrdinalIgnoreCase))
            options.Add($"--output \"{_settings.PacksDirectory}\"");

        if (force)
            options.Add("--force");

        var arguments = options.Count > 0 ? " -- " + string.Join(' ', options) : string.Empty;

        return $"Extract it from the installed client with: dotnet run --file scripts/extract.cs{arguments}";
    }
}
