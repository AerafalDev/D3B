namespace D3B.Bot.Reports;

/// <summary>A report that is not one, or that goes past the bounds a reader keeps: it is untrusted input (D410).</summary>
public sealed class ReportFormatException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ReportFormatException"/> class.</summary>
    public ReportFormatException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the report.</param>
    public ReportFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReportFormatException"/> class.</summary>
    /// <param name="message">What is wrong with the report.</param>
    /// <param name="innerException">What failed.</param>
    public ReportFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
