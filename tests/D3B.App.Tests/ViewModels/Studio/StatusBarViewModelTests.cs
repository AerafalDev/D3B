using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels.Studio;

namespace D3B.App.Tests.ViewModels.Studio;

public sealed class StatusBarViewModelTests
{
    private static StatusBarViewModel Bar(StatusFacts facts, string language = "fr")
    {
        var bar = new StatusBarViewModel(new Localizer(language == "fr" ? Localizer.French : Localizer.English), "0.9.10");

        bar.Refresh(facts);

        return bar;
    }

    [Fact]
    public void TheLeftNamesTheAppsVersionThenTheRelayAndHowManyCharactersItRelays()
    {
        var bar = Bar(new StatusFacts { Host = HostStatus.Running, InGame = 2, ClientBuild = "3.7.14.18" });

        Assert.Equal(("D3B 0.9.10", "Relais prêt · 2 en jeu", Tone.Success, false), (bar.VersionText, bar.RelayText, bar.RelayTone, bar.IsRelayFailed));

        // What the bar does not say: start Dofus after D3B, and that quitting ends the game connections it relays.
        Assert.Equal(
            ["Quitter D3B coupe les connexions de jeu qu'il relaie.", "Lancez Dofus après D3B : un client lancé avant joue sans le relais."],
            bar.RelayTip.Split('\n'));
    }

    [Theory]
    [InlineData("Starting", "Relais en démarrage…", "Info", false)]
    [InlineData("Failed", "Relais arrêté", "Danger", true)]
    [InlineData("Stopping", "Relais fermé", "Neutral", false)]
    public void TheRelaySaysWhereTheHostStandsRedOnceItFailed(string hostState, string text, string toneName, bool failed)
    {
        // By their names: the app's types are internal, a public test's parameters cannot be.
        var host = Enum.Parse<HostStatus>(hostState);
        var tone = Enum.Parse<Tone>(toneName);
        var bar = Bar(new StatusFacts { Host = host, Failure = host is HostStatus.Failed ? "No pack for client 3.7.14.18" : null });

        Assert.Equal((text, tone, failed), (bar.RelayText, bar.RelayTone, bar.IsRelayFailed));

        if (failed)
            Assert.StartsWith("No pack for client 3.7.14.18", bar.RelayTip, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRightNamesTheClientBuildAndTheDataPacksVersionTheBotsPlayingAndTheProblems()
    {
        var bar = Bar(new StatusFacts { Host = HostStatus.Running, ClientBuild = "3.7.14.18", PackFormat = 13, PackExtractor = "0.9.10", Playing = 1, Errors = 2, Warnings = 3 });

        Assert.Equal("Client 3.7.14.18 · données v13", bar.ClientText);
        Assert.Equal("Données extraites par D3B.Data 0.9.10", bar.ClientTip);
        Assert.Equal(("1 bot actif", true), (bar.PlayingText, bar.ShowsPlaying));
        Assert.Equal(("2", "3", true), (bar.ErrorsText, bar.WarningsText, bar.HasProblems));
    }

    [Fact]
    public void BeforeTheHostRunsTheClientIsTheOneTheAppShipsAndNoBotOrProblemIsCounted()
    {
        var bar = Bar(new StatusFacts { Host = HostStatus.Starting, ClientBuild = "3.7.14.18" }, "en");

        Assert.Equal(("Client 3.7.14.18", false), (bar.ClientText, bar.ShowsPlaying));
        Assert.Equal(("0", "0", false), (bar.ErrorsText, bar.WarningsText, bar.HasProblems));
    }

    [Fact]
    public void AnUpdateDownloadedOffersToRestartIntoItsVersion()
    {
        var none = Bar(new StatusFacts { Host = HostStatus.Running });
        var ready = Bar(new StatusFacts { Host = HostStatus.Running, UpdateVersion = "0.9.11" });

        Assert.False(none.IsUpdateReady);
        Assert.Equal((true, "Redémarrer pour passer à 0.9.11"), (ready.IsUpdateReady, ready.UpdateText));
    }

    [Fact]
    public void EachItemOpensWhatItNames()
    {
        var opened = new List<string>();
        var bar = new StatusBarViewModel(new Localizer(Localizer.English), "0.9.10")
        {
            ShowAbout = () => opened.Add("about"),
            ShowRelay = () => opened.Add("journal"),
            ShowData = () => opened.Add("files"),
            ShowCharacters = () => opened.Add("characters"),
            ShowProblems = () => opened.Add("problems"),
            SaveAll = () => opened.Add("save"),
            Restart = () => opened.Add("restart"),
        };

        bar.ShowAboutPageCommand.Execute(null);
        bar.ShowRelayJournalCommand.Execute(null);
        bar.ShowDataFilesCommand.Execute(null);
        bar.ShowCharactersViewCommand.Execute(null);
        bar.ShowProblemsTabCommand.Execute(null);
        bar.SaveEverythingCommand.Execute(null);
        bar.RestartIntoUpdateCommand.Execute(null);

        Assert.Equal(["about", "journal", "files", "characters", "problems", "save", "restart"], opened);
    }
}
