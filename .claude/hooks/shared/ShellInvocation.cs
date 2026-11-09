namespace D3B.Hooks;

/// <summary>A <c>git</c> or <c>gh</c> call read from one segment of a shell command, git's global options skipped.</summary>
internal sealed class ShellInvocation
{
    public ShellInvocation(string tool, string verb, IReadOnlyList<string> arguments)
    {
        Tool = tool;
        Verb = verb;
        Arguments = arguments;
    }

    public string Tool { get; }

    public string Verb { get; }

    public IReadOnlyList<string> Arguments { get; }
}
