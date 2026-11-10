using Avalonia.Headless;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels.Reports;

public sealed class ReportCharacterChoicesTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task ACharacterOnlineIsOfferedWithItsLevelSaidOnceAndTheLookOfItsOutlinerRow()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (choice, bot) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ReportCharacterChoicesTests).Assembly).Dispatch(
            () =>
            {
                Localizer.Instance.Use(Localizer.English);

                var main = new MainViewModel(runner, Localizer.Instance);

                main.Refresh();

                return (ReportSourcesFactory.Choices(main.Characters, Localizer.Instance).Single(), main.Characters.Online.Single());
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("Level 200, online", choice.Detail);
        Assert.Equal(bot.Initials, choice.Initials);
        Assert.Same(bot.PortraitColor, choice.PortraitColor);
        Assert.Same(bot.Head, choice.Head);
        Assert.False(choice.IsDimmed);
    }
}
