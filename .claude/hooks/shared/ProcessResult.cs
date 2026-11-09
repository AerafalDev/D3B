namespace D3B.Hooks;

/// <summary>How a command the hooks ran ended, and what it wrote on its two outputs.</summary>
internal sealed class ProcessResult
{
    public ProcessResult(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }

    public int ExitCode { get; }

    public string Output { get; }
}
