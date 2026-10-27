using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace D3B.Data.Releases;

/// <summary>Downloads the assets of a GitHub repository's releases, each checked against the SHA-256 published beside it.</summary>
/// <remarks>
/// <para>
/// Everything goes through the GitHub API, which serves private repositories too: the release is found by its tag, then
/// each asset is asked for by its identifier, and GitHub redirects to where the bytes are. A token, when given, is sent
/// to the API only: <see cref="HttpClient"/> clears the <c>Authorization</c> header when it follows a redirect.
/// </para>
/// <para>
/// An asset is written beside its destination and moved there only once whole and checked (D24), so a download cut
/// short, a damaged one or one the caller rejects never lands under the name a host looks for. A lock beside the
/// destination keeps two downloads of it from running at once, and a read that waits too long fails the download
/// rather than hanging it.
/// </para>
/// </remarks>
public sealed class ReleaseDownloader : IDisposable
{
    private static readonly Uri _api = new("https://api.github.com/");

    private readonly HttpClient _http;
    private readonly TimeSpan _readTimeout;

    /// <summary>Gets the repository whose releases are downloaded, as <c>owner/name</c>.</summary>
    public string Repository { get; }

    /// <summary>Initializes a new instance of the <see cref="ReleaseDownloader"/> class.</summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="handler">What sends the requests; by default, the network.</param>
    /// <exception cref="ArgumentException"><paramref name="repository"/> does not name a repository.</exception>
    public ReleaseDownloader(string repository, HttpMessageHandler? handler = null)
        : this(repository, handler, TimeSpan.FromSeconds(30))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReleaseDownloader"/> class.</summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="handler">What sends the requests; by default, the network.</param>
    /// <param name="readTimeout">How long a read of an answer may wait for its bytes.</param>
    internal ReleaseDownloader(string repository, HttpMessageHandler? handler, TimeSpan readTimeout)
    {
        if (!ClientRelease.IsRepository(repository))
            throw new ArgumentException($"'{repository}' does not name a GitHub repository as owner/name.", nameof(repository));

        Repository = repository;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _readTimeout = readTimeout;

        // GitHub's API rejects requests without a user agent.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("D3B");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private static long Find(Dictionary<string, long> assets, string tag, string name)
    {
        return assets.TryGetValue(name, out var id) ? id : throw new ReleaseDownloadException($"Release {tag} has no asset {name}.");
    }

    private static HttpRequestMessage Request(Uri uri, string accept, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static FileStream Lock(string path)
    {
        try
        {
            // Deleted when closed, even by a process that ends: a lock left by a killed one opens again.
            return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception)
        {
            throw new ReleaseDownloadException($"Another download of {Path.GetFileName(path)} is running: start again once it is done.", exception);
        }
    }

    /// <summary>Downloads an asset of a release into a file, once it matches its SHA-256 and the caller accepts it.</summary>
    /// <param name="tag">The release's tag.</param>
    /// <param name="assetName">The asset's name. The release holds its SHA-256 in <see cref="ClientRelease.ChecksumNameOf"/>.</param>
    /// <param name="path">Where the asset goes. A file already there, or appearing there meanwhile, is kept.</param>
    /// <param name="token">A GitHub token, needed while the repository is private; <see langword="null"/> for none.</param>
    /// <param name="check">Checks the downloaded file, by its path, before it is moved into place; throws to reject it, and closes it.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>The asset's SHA-256, in lowercase hexadecimal; <see langword="null"/> when a file at <paramref name="path"/> was kept instead.</returns>
    /// <exception cref="ReleaseDownloadException">
    /// The release or the asset is missing, GitHub could not be reached or stopped sending, the bytes do not match the
    /// SHA-256, or another download of the file is running.
    /// </exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    public async Task<string?> DownloadAsync(string tag, string assetName, string path, string? token, Action<string>? check, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        ArgumentException.ThrowIfNullOrEmpty(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        // Held until the asset is in place: of two hosts starting at once, the second stops here.
        using var held = Lock(path);

        if (File.Exists(path))
            return null;

        var assets = await AssetsAsync(tag, token, cancellationToken);
        var expected = await ChecksumAsync(tag, assets, assetName, token, cancellationToken);
        var assetId = Find(assets, tag, assetName);
        var temporary = path + ".download";
        var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81_920, FileOptions.Asynchronous);

        try
        {
            string actual;

            await using (file)
                actual = await CopyAsync(assetId, assetName, token, file, cancellationToken);

            if (actual != expected)
                throw new ReleaseDownloadException($"{assetName} of release {tag} does not match its SHA-256: {actual} instead of {expected}.");

            check?.Invoke(temporary);

            try
            {
                File.Move(temporary, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Written meanwhile by something that takes no lock, such as the extraction tool, and maybe open already.
                return null;
            }

            return actual;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
    }

    private async Task<Dictionary<string, long>> AssetsAsync(string tag, string? token, CancellationToken cancellationToken)
    {
        using var request = Request(new Uri(_api, $"repos/{Repository}/releases/tags/{Uri.EscapeDataString(tag)}"), "application/vnd.github+json", token);
        using var response = await SendAsync(request, $"release {tag}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            var hint = token is null ? "or it is private and needs a GitHub token" : "or the token cannot read it";

            throw new ReleaseDownloadException($"{Repository} has no release {tag}, {hint}.");
        }

        EnsureSuccess(response, $"release {tag}");

        try
        {
            return await ReadAsync($"release {tag}", async timeout =>
            {
                await using var stream = await response.Content.ReadAsStreamAsync(timeout);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout);

                var assets = new Dictionary<string, long>(StringComparer.Ordinal);

                foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
                    assets[asset.GetProperty("name").GetString()!] = asset.GetProperty("id").GetInt64();

                return assets;
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ReleaseDownloadException($"GitHub's description of release {tag} is not one this version of D3B reads.", exception);
        }
    }

    private async Task<string> ChecksumAsync(string tag, Dictionary<string, long> assets, string assetName, string? token, CancellationToken cancellationToken)
    {
        var name = ClientRelease.ChecksumNameOf(assetName);

        using var request = Request(AssetUri(Find(assets, tag, name)), "application/octet-stream", token);
        using var response = await SendAsync(request, name, cancellationToken);

        EnsureSuccess(response, name);

        var text = await ReadAsync(name, response.Content.ReadAsStringAsync, cancellationToken);

        try
        {
            return ClientRelease.ParseChecksum(text, assetName);
        }
        catch (InvalidDataException exception)
        {
            throw new ReleaseDownloadException($"Release {tag}: {exception.Message}", exception);
        }
    }

    private async Task<string> CopyAsync(long assetId, string assetName, string? token, FileStream file, CancellationToken cancellationToken)
    {
        using var request = Request(AssetUri(assetId), "application/octet-stream", token);
        using var response = await SendAsync(request, assetName, cancellationToken);

        EnsureSuccess(response, assetName);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81_920);

        try
        {
            await using var content = await ReadAsync(assetName, response.Content.ReadAsStreamAsync, cancellationToken);

            int read;

            // Each read has its own time limit: a download that stalls fails, however long the whole one takes.
            while ((read = await ReadAsync(assetName, timeout => content.ReadAsync(buffer, timeout).AsTask(), cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private async Task<T> ReadAsync<T>(string what, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        // HttpClient's own timeout ends with the headers: the body is read under this one.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(_readTimeout);

        try
        {
            return await read(timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReleaseDownloadException($"GitHub stopped sending {what} for {_readTimeout.TotalSeconds:F0} s.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException and not ReleaseDownloadException)
        {
            throw new ReleaseDownloadException($"The download of {what} was cut short: {exception.Message}", exception);
        }
    }

    private Uri AssetUri(long assetId)
    {
        // Built from the identifier rather than taken from GitHub's answer, so that the token only ever goes to the API.
        return new Uri(_api, $"repos/{Repository}/releases/assets/{assetId}");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string what, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ReleaseDownloadException($"GitHub could not be reached for {what}: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReleaseDownloadException($"GitHub did not answer in time for {what}.", exception);
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode)
            return;

        throw response.StatusCode is HttpStatusCode.Unauthorized
            ? new ReleaseDownloadException($"GitHub refused the token for {what} of {Repository}: 401 Unauthorized.")
            : new ReleaseDownloadException($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase} for {what} of {Repository}.");
    }
}
