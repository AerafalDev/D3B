using D3B.Bot.Data;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.Releases;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Protocol;

/// <summary>The schema and mapping of the installed client's build: kept on this machine, else downloaded from its release (D48).</summary>
/// <remarks>
/// <para>
/// As for the pack, files on this machine come first, so a build mapped here needs no release and works offline (D31).
/// Otherwise <see cref="EnsureAsync"/> downloads each from the release, checked against its SHA-256, and that it is a
/// schema, or a mapping of this build, before it is moved into place. <see cref="Load"/> then makes the build's
/// translation, which audits the mapping (D44) and checks that it keeps the wire's numbers (D47): the host's version gate
/// (D11).
/// </para>
/// <para>
/// Every failure is a <see cref="ProtocolUnavailableException"/> whose message says which build lacks what, and how to
/// map it from its client.
/// </para>
/// </remarks>
public sealed class ProtocolSource
{
    private readonly DataSettings _settings;
    private readonly ClientBuild _build;
    private readonly ReleaseDownloader _downloader;
    private readonly ReleaseToken _token;
    private readonly ILogger _log;

    /// <summary>Gets where the schema of the installed build is kept, beside its pack.</summary>
    public string SchemaPath => SchemaFile.PathOf(_settings.PacksDirectory, _build.Version);

    /// <summary>Gets where the mapping of the installed build is kept, beside its pack.</summary>
    public string MappingPath => MappingFile.PathOf(_settings.PacksDirectory, _build.Version);

    internal ProtocolSource(DataSettings settings, ClientBuild build, ReleaseDownloader downloader, ReleaseToken token, ILogger<ProtocolSource> log)
    {
        _settings = settings;
        _build = build;
        _downloader = downloader;
        _token = token;
        _log = log;
    }

    private static void CheckSchema(string path)
    {
        try
        {
            ProtocolSchema.Load(path);
        }
        catch (Exception exception) when (exception is InvalidProtocolBufferException or DescriptorValidationException or ArgumentException)
        {
            throw new InvalidDataException($"the release's {SchemaFile.FileName} is not a schema: {exception.Message}", exception);
        }
    }

    private static string Arguments(List<string> options)
    {
        return options.Count is 0 ? string.Empty : " -- " + string.Join(' ', options);
    }

    /// <summary>Makes sure the schema and mapping of the installed build are on this machine, downloading those that are not.</summary>
    /// <param name="cancellationToken">Stops the downloads.</param>
    /// <returns>Whether this call downloaded either.</returns>
    /// <exception cref="ProtocolUnavailableException">One is neither here nor downloadable.</exception>
    public async Task<bool> EnsureAsync(CancellationToken cancellationToken)
    {
        var schema = await EnsureFileAsync("schema", SchemaFile.FileName, SchemaPath, CheckSchema, cancellationToken);
        var mapping = await EnsureFileAsync("mapping", MappingFile.FileName, MappingPath, CheckMapping, cancellationToken);

        return schema || mapping;
    }

    /// <summary>Loads the schema and mapping of the installed build, and makes its translation once the mapping holds.</summary>
    /// <returns>The build's translation.</returns>
    /// <exception cref="ProtocolUnavailableException">Either is missing or unreadable, the mapping is another build's, or it does not hold.</exception>
    public ProtocolTranslation Load()
    {
        var schema = Read("schema", SchemaPath, ProtocolSchema.Load);
        var mapping = Read("mapping", MappingPath, MappingFile.Read);

        if (mapping.ClientVersion != _build.Version)
            throw new ProtocolUnavailableException($"The mapping in {MappingPath} maps client {mapping.ClientVersion}, not {_build.Version}, the one installed. {MapHint()}");

        try
        {
            return ProtocolTranslation.Create(schema, mapping);
        }
        catch (ProtocolMismatchException exception)
        {
            throw new ProtocolUnavailableException(
                $"{exception.Message} The mapping is {MappingPath}, the schema {SchemaPath}. The mapping tool lists every failure: "
                + $"pin what the structure leaves open, or fix the readable .proto files, then map the build again. {MapHint()}",
                exception);
        }
    }

    private void CheckMapping(string path)
    {
        ProtocolMapping mapping;

        try
        {
            mapping = MappingFile.Read(path);
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException($"the release's {MappingFile.FileName} is not a mapping: {exception.Message}", exception);
        }

        if (mapping.ClientVersion != _build.Version)
            throw new InvalidDataException($"the release's {MappingFile.FileName} maps client {mapping.ClientVersion}.");
    }

    private async Task<bool> EnsureFileAsync(string what, string asset, string path, Action<string> check, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
            return false;

        var tag = ClientRelease.TagOf(_build);
        var token = await _token.GetAsync(cancellationToken);

        ProtocolLog.Downloading(_log, what, _build.Version, path, asset, tag, _downloader.Repository, token is null ? "without a token" : "with a token");

        try
        {
            var hash = await _downloader.DownloadAsync(tag, asset, path, token, check, cancellationToken);

            // Null when a file landed meanwhile, such as one the mapping tool wrote: Load checks it like any other.
            if (hash is null)
                return false;

            ProtocolLog.Downloaded(_log, path, hash);

            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or HttpRequestException or UnauthorizedAccessException)
        {
            var hint = token is null ? $" A private repository needs a token: {DataSettings.TokenKey}, or the GitHub CLI's once logged in with gh auth login." : string.Empty;

            throw new ProtocolUnavailableException($"No {what} for client {_build.Version}: none at {path}, and the download from release {tag} failed: {exception.Message}{hint} {MapHint()}", exception);
        }
    }

    private T Read<T>(string what, string path, Func<string, T> read)
    {
        if (!File.Exists(path))
            throw new ProtocolUnavailableException($"No {what} for client {_build.Version}: none at {path}. {MapHint()}");

        try
        {
            return read(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidProtocolBufferException or DescriptorValidationException or ArgumentException)
        {
            throw new ProtocolUnavailableException($"The {what} of client {_build.Version} cannot be read from {path}: {exception.Message} {MapHint()}", exception);
        }
    }

    private string MapHint()
    {
        var schema = new List<string>();
        var mapping = new List<string>();

        if (!string.Equals(_settings.ClientDirectory, DataSettings.DefaultClientDirectory, StringComparison.OrdinalIgnoreCase))
        {
            schema.Add($"--client \"{_settings.ClientDirectory}\"");
            mapping.Add($"--client \"{_settings.ClientDirectory}\"");
        }

        if (!string.Equals(_settings.PacksDirectory, PackLocations.DefaultRoot, StringComparison.OrdinalIgnoreCase))
        {
            schema.Add($"--output \"{_settings.PacksDirectory}\"");
            mapping.Add($"--data \"{_settings.PacksDirectory}\"");
        }

        // A new build's connection phase is mapped from its structure alone (D43): nothing has to be captured first.
        return $"Map the installed build with: dotnet run --file scripts/schema.cs{Arguments(schema)}, then dotnet run --file scripts/mapping.cs{Arguments(mapping)}; no capture is needed.";
    }
}
