namespace D3B.Data.Releases;

/// <summary>The exception thrown when an asset of a release cannot be downloaded, or what was downloaded is not that asset.</summary>
public sealed class ReleaseDownloadException : IOException
{
    /// <summary>Initializes a new instance of the <see cref="ReleaseDownloadException"/> class.</summary>
    public ReleaseDownloadException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReleaseDownloadException"/> class.</summary>
    /// <param name="message">Why the asset could not be had.</param>
    public ReleaseDownloadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReleaseDownloadException"/> class.</summary>
    /// <param name="message">Why the asset could not be had.</param>
    /// <param name="innerException">The error met while downloading it.</param>
    public ReleaseDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
