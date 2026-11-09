namespace D3B.Hooks.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void ACommandsOutputAndExitCodeAreReturned()
    {
        var result = ProcessRunner.Run("dotnet", ["--version"], Directory.GetCurrentDirectory());

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.Output.Trim());
    }

    [Fact]
    public void AFailingCommandsExitCodeAndErrorsAreReturned()
    {
        var result = ProcessRunner.Run("dotnet", ["no-such-command-d3b"], Directory.GetCurrentDirectory());

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Output);
    }
}
