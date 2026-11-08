namespace D3B.Studio.Assets;

/// <summary>A file of the project that does not read as an asset, and why.</summary>
public sealed class AssetFormatException : Exception
{
    /// <summary>Gets why, as the app says it.</summary>
    public AssetProblemKind Kind { get; init; } = AssetProblemKind.Content;

    /// <summary>Initializes a new instance of the <see cref="AssetFormatException"/> class.</summary>
    public AssetFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AssetFormatException"/> class.</summary>
    /// <param name="message">Why the file does not read.</param>
    public AssetFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AssetFormatException"/> class.</summary>
    /// <param name="message">Why the file does not read.</param>
    /// <param name="innerException">What failed as it was read.</param>
    public AssetFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
