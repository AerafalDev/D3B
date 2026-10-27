using D3B.Data.Packs;
using D3B.Data.Releases;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Data;

/// <summary>Where the data module finds the client, keeps packs and downloads them, as the host's configuration says.</summary>
internal sealed class DataSettings
{
    public const string ClientDirectoryKey = "Client:Directory";
    public const string PacksDirectoryKey = "Data:Directory";
    public const string RepositoryKey = "Releases:Repository";
    public const string TokenKey = "Releases:Token";

    /// <summary>Gets where the Ankama launcher installs the client, as the extraction tool assumes too.</summary>
    public static string DefaultClientDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");

    public required string ClientDirectory { get; init; }

    public required string PacksDirectory { get; init; }

    public required string Repository { get; init; }

    public string? Token { get; init; }

    public static DataSettings Read(IConfiguration configuration)
    {
        var repository = configuration[RepositoryKey] ?? ClientRelease.DefaultRepository;
        var token = configuration[TokenKey];

        if (!ClientRelease.IsRepository(repository))
            throw new InvalidOperationException($"{RepositoryKey} is '{repository}', which does not name a GitHub repository as owner/name.");

        return new DataSettings
        {
            ClientDirectory = PathOr(configuration[ClientDirectoryKey], DefaultClientDirectory),
            PacksDirectory = PathOr(configuration[PacksDirectoryKey], PackLocations.DefaultRoot),
            Repository = repository,
            Token = string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
        };
    }

    private static string PathOr(string? configured, string fallback)
    {
        // As for plugins, a relative path starts from the host, never from the directory it was started in.
        return string.IsNullOrWhiteSpace(configured) ? fallback : Path.GetFullPath(configured, AppContext.BaseDirectory);
    }
}
