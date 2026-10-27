using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D3B.Data.Releases;

namespace D3B.Data.Tests.Releases;

/// <summary>Answers as GitHub's API does for the releases published to it, and records what it was asked.</summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    private const string Tags = "/releases/tags/";
    private const string Assets = "/releases/assets/";

    private readonly Dictionary<string, List<Asset>> _releases = new(StringComparer.Ordinal);
    private readonly List<Asset> _assets = [];

    /// <summary>Gets or sets the token a request must carry, as for a private repository, which GitHub then hides.</summary>
    public string? RequiredToken { get; set; }

    /// <summary>Gets or sets an asset sent only in part, after which the connection stalls.</summary>
    public string? StallingAsset { get; set; }

    /// <summary>Gets each request, with the token it carried.</summary>
    public List<(Uri Uri, string? Token)> Requests { get; } = [];

    public static byte[] Checksum(byte[] content, string name)
    {
        return Encoding.UTF8.GetBytes(ClientRelease.FormatChecksum(SHA256.HashData(content), name));
    }

    private static HttpResponseMessage NotFound()
    {
        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"message":"Not Found"}""") };
    }

    private static HttpResponseMessage Release(string tag, List<Asset> assets)
    {
        // The links point elsewhere on purpose: a download must never follow them with its token.
        var json = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            assets = assets.Select(asset => new
            {
                id = asset.Id,
                name = asset.Name,
                size = asset.Content.Length,
                url = $"https://example.com/assets/{asset.Id}",
                browser_download_url = $"https://example.com/download/{asset.Name}",
            }),
        });

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    /// <summary>Publishes a release whose files each come with their SHA-256 beside them, as the release tool does.</summary>
    public void Publish(string repository, string tag, params (string Name, byte[] Content)[] files)
    {
        PublishAssets(repository, tag, [.. files.SelectMany(file => new[] { file, (ClientRelease.ChecksumNameOf(file.Name), Checksum(file.Content, file.Name)) })]);
    }

    /// <summary>Publishes a release holding exactly these assets.</summary>
    public void PublishAssets(string repository, string tag, params (string Name, byte[] Content)[] files)
    {
        var assets = new List<Asset>();

        foreach (var file in files)
        {
            var asset = new Asset(100 + _assets.Count, repository, file.Name, file.Content);

            _assets.Add(asset);
            assets.Add(asset);
        }

        _releases[$"{repository}/{tag}"] = assets;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var token = request.Headers.Authorization?.Parameter;

        Requests.Add((uri, token));

        return Task.FromResult(RequiredToken is not null && token != RequiredToken ? NotFound() : Answer(uri, request));
    }

    private HttpResponseMessage Answer(Uri uri, HttpRequestMessage request)
    {
        var path = uri.AbsolutePath;

        if (uri.Host != "api.github.com" || !path.StartsWith("/repos/", StringComparison.Ordinal))
            return NotFound();

        var tags = path.IndexOf(Tags, StringComparison.Ordinal);

        if (tags >= 0)
        {
            var repository = path["/repos/".Length..tags];
            var tag = Uri.UnescapeDataString(path[(tags + Tags.Length)..]);

            return _releases.TryGetValue($"{repository}/{tag}", out var assets) ? Release(tag, assets) : NotFound();
        }

        var assetsAt = path.IndexOf(Assets, StringComparison.Ordinal);

        if (assetsAt < 0 || !request.Headers.Accept.Any(accept => accept.MediaType == "application/octet-stream"))
            return NotFound();

        var id = long.Parse(path[(assetsAt + Assets.Length)..], CultureInfo.InvariantCulture);
        var found = _assets.Find(asset => asset.Id == id && path == $"/repos/{asset.Repository}{Assets}{id}");

        if (found is null)
            return NotFound();

        HttpContent content = found.Name == StallingAsset ? new StreamContent(new StallingStream(found.Content[..(found.Content.Length / 2)])) : new ByteArrayContent(found.Content);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed record Asset(long Id, string Repository, string Name, byte[] Content);
}
