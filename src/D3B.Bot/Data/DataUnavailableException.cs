namespace D3B.Bot.Data;

/// <summary>The exception thrown when the game data of the installed client cannot be had: no client, or no pack of its build.</summary>
/// <remarks>Its message says what is missing and how to get it; the host shows it and does not start.</remarks>
public sealed class DataUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DataUnavailableException"/> class.</summary>
    public DataUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DataUnavailableException"/> class.</summary>
    /// <param name="message">What is missing, and how to get it.</param>
    public DataUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DataUnavailableException"/> class.</summary>
    /// <param name="message">What is missing, and how to get it.</param>
    /// <param name="innerException">The error met while looking for it.</param>
    public DataUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
