using Lua.Platforms;

namespace D3B.Scripting.Sandbox;

/// <summary>The operating system a script sees: no environment variable, no exit.</summary>
/// <remarks>The sandbox removes <c>os.getenv</c> and <c>os.exit</c>; this keeps them harmless should they come back.</remarks>
internal sealed class NoOsEnvironment : ILuaOsEnvironment
{
    public string? GetEnvironmentVariable(string name)
    {
        return null;
    }

    public ValueTask Exit(int exitCode, CancellationToken cancellationToken)
    {
        throw new UnauthorizedAccessException("A script cannot end the process.");
    }

    public double GetTotalProcessorTime()
    {
        return 0;
    }
}
