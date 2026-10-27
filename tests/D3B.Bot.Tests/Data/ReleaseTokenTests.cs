using D3B.Bot.Data;

namespace D3B.Bot.Tests.Data;

public sealed class ReleaseTokenTests
{
    // Stands for a machine without the GitHub CLI, so that no test ever reads the owner's token.
    private const string NoCli = "d3b-no-such-command";

    [Fact]
    public async Task AConfiguredTokenComesFirst()
    {
        Assert.Equal("configured", await new ReleaseToken("configured", NoCli).GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithoutTheGitHubCliThereIsNoToken()
    {
        Assert.Null(await new ReleaseToken(null, NoCli).GetAsync(TestContext.Current.CancellationToken));
    }
}
