using D3B.App.Localization;
using D3B.App.ViewModels.Bottom;
using D3B.Bot.Characters;
using D3B.Bot.Hosting;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.App.Tests.ViewModels;

public sealed class ProblemsViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _french = new(Localizer.French);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task TheProjectTheCharactersAndTheHostSayWhatIsWrongEachWithItsFix()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.OpenedAsync();
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Character", 200), (TestPlay.CharacterId + 1, "Mule", 12));
        await _store.Accounts.AssignAsync(new CharacterKey(TestPlay.AccountId, TestPlay.CharacterId + 1), "gone", ct);
        Directory.CreateDirectory(Path.Combine(_store.Assets.Folder, "Chasse"));
        await File.WriteAllTextAsync(Path.Combine(_store.Assets.Folder, "Chasse", "Frigost.profile.json"), "{", ct);
        await _store.Assets.LoadAsync([.. _store.Assets.Assets.Where(asset => asset.IsGiven)], ct);

        string? opened = null;
        CharacterKey? chosen = null;
        var problems = new ProblemsViewModel(_french) { OpenFolder = folder => opened = folder, ChooseCharacter = key => chosen = key };

        problems.Refresh(_store.Host.Services, null);

        Assert.Equal(["Projet", "Personnages"], problems.Groups.Select(group => group.Title));
        Assert.Equal(
            [
                "Frigost.profile.json ne se lit pas",
                "Le profil de Mule a disparu",
                "Character n'a pas de profil",
            ],
            problems.Groups.SelectMany(group => group.Problems).Select(problem => problem.Title));
        Assert.Equal((2, 1), (problems.Errors, problems.Warnings));

        problems.Groups[0].Problems[0].FixCommand.Execute(null);
        problems.Groups[1].Problems[0].FixCommand.Execute(null);

        Assert.Equal(Path.Combine(_store.Assets.Folder, "Chasse"), opened);
        Assert.Equal(new CharacterKey(TestPlay.AccountId, TestPlay.CharacterId + 1), chosen);

        problems.ShowHostCommand.Execute(null);

        Assert.True(problems.IsEmpty);
        Assert.Equal((2, 1), (problems.Errors, problems.Warnings));

        problems.Refresh(_store.Host.Services, "The pack did not open.");

        Assert.Equal("L'hôte n'a pas démarré", problems.Groups[0].Problems[0].Title);
        Assert.False(problems.Groups[0].Problems[0].CanFix);
        Assert.Equal(3, problems.Errors);
    }

    [Fact]
    public void TheHostsWarningsAreToldOnceEachTheJournalShowingThem()
    {
        var host = new HostProblems(TimeProvider.System);
        using var services = new ServiceCollection().AddSingleton(host).BuildServiceProvider();
        var shown = 0;
        var problems = new ProblemsViewModel(_french) { ShowHostJournal = () => shown++ };

        host.Problems.Add(new HostProblem(DateTimeOffset.UnixEpoch, LogLevel.Warning, "D3B.Bot.Runtime.Bots", "Game connection 7: its account is not known", null));
        host.Problems.Add(new HostProblem(DateTimeOffset.UnixEpoch.AddSeconds(1), LogLevel.Warning, "D3B.Bot.Runtime.Bots", "Game connection 7: its account is not known", null));
        host.Problems.Add(new HostProblem(DateTimeOffset.UnixEpoch.AddSeconds(2), LogLevel.Error, "D3B.Network.Relay", "The relay stopped\nat …", null));
        problems.Refresh(services, null);

        Assert.Equal(["The relay stopped", "Game connection 7: its account is not known"], Assert.Single(problems.Groups).Problems.Select(problem => problem.Title));
        Assert.Equal((1, 1), (problems.Errors, problems.Warnings));
        Assert.StartsWith("Relay · ", problems.Groups[0].Problems[0].Why, StringComparison.Ordinal);

        problems.Groups[0].Problems[1].FixCommand.Execute(null);

        Assert.Equal(1, shown);
    }

    [Fact]
    public void WithoutTheHostOnlyItsFailureIsSaid()
    {
        var problems = new ProblemsViewModel(_french);

        problems.Refresh(null, null);

        Assert.True(problems.IsEmpty);

        problems.Refresh(null, "No client is installed.\nInstall it.");

        var failure = Assert.Single(Assert.Single(problems.Groups).Problems);

        Assert.Equal(("L'hôte n'a pas démarré", "No client is installed."), (failure.Title, failure.Why));
        Assert.True(problems.HasErrors);
    }
}
