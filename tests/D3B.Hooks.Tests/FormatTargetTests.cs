namespace D3B.Hooks.Tests;

public sealed class FormatTargetTests
{
    [Theory]
    [InlineData("src/D3B.Core/Modules/BotBuilder.cs")]
    [InlineData("tests/D3B.Core.Tests/TestBots.cs")]
    [InlineData("scripts/fights/CastBench.cs")]
    public void TheCSharpOfTheProjectsIsFormatted(string path)
    {
        Assert.True(FormatTarget.IsFormatted(path));
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("src/D3B.App/Views/Main.axaml")]
    [InlineData("src/D3B.Protocol/Game/fights.proto")]
    [InlineData("artifacts/obj/A.cs")]
    [InlineData("src/D3B.Core/obj/Debug/A.cs")]
    [InlineData("src/D3B.Core/bin/A.cs")]
    [InlineData("vendor/BillBot/A.cs")]
    [InlineData("src/D3B.Protocol/Game/Fight.g.cs")]
    [InlineData("src/D3B.App/Views/Main.Designer.cs")]
    public void OtherFilesAndGeneratedCodeAreLeftAlone(string path)
    {
        Assert.False(FormatTarget.IsFormatted(path));
    }
}
