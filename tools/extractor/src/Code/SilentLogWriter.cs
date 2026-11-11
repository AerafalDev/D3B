using LibCpp2IL.Logging;

namespace D3B.Extractor.Code;

/// <summary>Keeps Cpp2IL's own log out of the extractor's output, which says what was extracted.</summary>
internal sealed class SilentLogWriter : LogWriter
{
    public override void Info(string message)
    {
    }

    public override void Warn(string message)
    {
    }

    public override void Error(string message)
    {
    }

    public override void Verbose(string message)
    {
    }
}
