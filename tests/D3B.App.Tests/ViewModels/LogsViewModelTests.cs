using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Logs;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Protocol.Game;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class LogsViewModelTests : IAsyncDisposable
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 3, 17, 19, TimeSpan.Zero);

    private readonly TestPlay _play = new();
    private readonly HostProblems _problems = new(TimeProvider.System);
    private readonly IHost _host;
    private readonly Localizer _french = new(Localizer.French);
    private readonly TemporaryDirectory _journals = new();
    private readonly JournalRecorder _recorder;

    public LogsViewModelTests()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_problems);
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        _recorder = new JournalRecorder(new JournalFiles(_journals.Path), JournalRetention.Default, _play.Bots, _problems, _play.Time, NullLogger<JournalRecorder>.Instance);
        builder.Services.AddSingleton(_recorder);
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        _recorder.Dispose();
        await _play.DisposeAsync();
        _journals.Dispose();
    }

    private static void Write(BotActor bot)
    {
        bot.Journal.Add(new WalkEntry(_at, 0, TestPlay.MapId, 388, WalkOutcome.Arrived, TimeSpan.FromMilliseconds(900)));
        bot.Journal.Add(new MapChangeEntry(_at, 1, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.NorthMapId, MapChangeKind.Scroll, MapChangeOutcome.Changed, TimeSpan.FromMilliseconds(1_400)));
        bot.Journal.Add(new TravelEntry(_at, 0, TestPlay.MapId, TestPlay.NorthMapId, TestPlay.NorthMapId, 1, TravelOutcome.Arrived, TimeSpan.FromMilliseconds(3_300)));
        bot.Journal.Add(new ActivityOverEntry(_at, "patrouille", 1, ActivityOutcome.Failed, "The step failed.", false));
    }

    [Fact]
    public async Task ABotsJournalIsSaidInTheAppsLanguageEachActionOnALine()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice(), new Toasts());

        Write(bot);
        page.Refresh(_host.Services);
        page.Show(bot.Key);

        Assert.Equal(
            [
                ("Joue par la connexion de jeu", null),
                ("Marche vers la case 388", "arrivé · 900 ms"),
                ("Change de carte : 4,-18 → 4,-19, par le haut", "changé · 1,4 s"),
                ("Voyage vers 4,-19", "arrivé · 1 carte · 3,3 s"),
                ("« patrouille » a échoué : The step failed.", "1 étape"),
            ],
            page.Lines.Select(line => (line.Text, line.Detail)));
        Assert.Equal([Tone.Success, Tone.Success, Tone.Success, Tone.Success, Tone.Danger], page.Lines.Select(line => line.Tone));
        Assert.Equal([0.0, 0, 20, 0, 0], page.Lines.Select(line => line.Indent));
        Assert.All(page.Lines, line => Assert.Null(line.Source));
        Assert.Equal("5 lignes gardées", page.Summary);
    }

    [Fact]
    public async Task WhatComesSinceIsAddedAtEachRefresh()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        page.Refresh(_host.Services);

        Assert.Single(page.Lines);

        bot.Journal.Add(new BotEventEntry(_at, BotEvent.PausedByPlayer));
        page.Refresh(_host.Services);

        Assert.Equal(["Joue par la connexion de jeu", "Le joueur a pris la main : le bot est en pause"], page.Lines.Select(line => line.Text));
        Assert.Equal(Tone.Warning, page.Lines[^1].Tone);
    }

    [Fact]
    public async Task AFightsEndSaysItsOutcomeAndGains()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        bot.Journal.Add(new FightEndEntry(_at, FightOutcome.Won, 77, 7, 1));
        bot.Journal.Add(new FightEndEntry(_at, FightOutcome.Lost, 0, 0, 0));
        page.Refresh(_host.Services);

        Assert.Equal(("Combat gagné", "77 XP · 7 kamas · 1 objet", Tone.Success), (page.Lines[^2].Text, page.Lines[^2].Detail, page.Lines[^2].Tone));
        Assert.Equal(("Combat perdu", "0 XP · 0 kamas · 0 objet", Tone.Danger), (page.Lines[^1].Text, page.Lines[^1].Detail, page.Lines[^1].Tone));
    }

    [Fact]
    public async Task AProfileThatDidNotStartOnConnectionSaysWhy()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        bot.Journal.Add(new ProfileNotStartedEntry(_at, "Promenade", D3B.Studio.Behaviors.ProfileStartFailure.BehaviorMissing, [], null));
        page.Refresh(_host.Services);

        var line = page.Lines[^1];

        Assert.Equal(("Le profil « Promenade » n'a pas démarré à la connexion", "Son comportement a disparu", Tone.Warning), (line.Text, line.Detail, line.Tone));
    }

    [Fact]
    public async Task TheLevelAndTheTextSoughtFilterTheLines()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        Write(bot);
        bot.Journal.Add(new WalkEntry(_at, 0, TestPlay.MapId, 12, WalkOutcome.Refused, TimeSpan.FromMilliseconds(100)));
        page.Refresh(_host.Services);
        page.ShowWarningsCommand.Execute(null);

        Assert.Equal(["Marche vers la case 12"], page.Lines.Select(line => line.Text));

        page.ShowErrorsCommand.Execute(null);

        Assert.Equal(["« patrouille » a échoué : The step failed."], page.Lines.Select(line => line.Text));

        page.ShowInfosCommand.Execute(null);

        Assert.Equal(4, page.Lines.Count);
        Assert.All(page.Lines, line => Assert.False(line.IsProblem));

        page.ShowAllLevelsCommand.Execute(null);
        page.Search = "CASE";

        Assert.Equal(["Marche vers la case 388", "Marche vers la case 12"], page.Lines.Select(line => line.Text));
        Assert.Equal("2 lignes affichées sur 6", page.Summary);
    }

    [Fact]
    public async Task EverySourceNamesEachLinesAndTheHostShowsItsProblems()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice(), new Toasts());

        _problems.Problems.Add(new HostProblem(_at, LogLevel.Warning, "D3B.Bot.Runtime.Bots", "Game connection 7: its account is not known, so no bot plays its character", null));
        page.Refresh(_host.Services);

        Assert.True(page.ShowsSources);
        Assert.Equal(["Toutes les sources", "Hôte", "Character"], page.Sources.Select(source => source.Name));
        Assert.Equal(["Character", "Hôte"], page.Lines.Select(line => line.Source).Order(StringComparer.Ordinal));
        Assert.Equal([("CH", "Character"), ("H", "Hôte")], page.Lines.Select(line => (line.Who!.Initials, line.Who.Name)).Order());

        page.Sources[1].SelectCommand.Execute(null);

        var problem = Assert.Single(page.Lines);

        Assert.Equal(("Game connection 7: its account is not known, so no bot plays its character", "Bots", Tone.Warning), (problem.Text, problem.Detail, problem.Tone));
        Assert.False(page.ShowsSources);
        Assert.False(problem.HasWho);
    }

    [Fact]
    public async Task ANewLanguageSaysTheLinesAgain()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        page.Refresh(_host.Services);
        _french.Use(Localizer.English);
        page.Refresh(_host.Services);

        Assert.Equal(["Plays through the game connection"], page.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task TheBotLookedAtIsTheOneTheInspectorLooksAt()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var choice = new BotChoice();
        var page = new LogsViewModel(_french, choice, new Toasts());

        page.Refresh(_host.Services);
        page.Sources[2].SelectCommand.Execute(null);

        Assert.Equal(bot.Key, choice.Key);
    }

    [Fact]
    public async Task SavingABotsJournalWritesEveryDayTheDiskKeepsOfItAsTheMaintainersReadIt()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var toasts = new Toasts();
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, toasts);
        var path = _journals.Combine("saved.txt");

        // A day before the app started again: the journal in memory no longer holds it.
        var before = _play.Time.GetUtcNow().AddDays(-1);

        _recorder.Files.Append(JournalFiles.SourceOf(bot.Key), [new JournalLine(before, $"{JournalText.TimeOf(before)} NoteEntry {{ Text = before the restart }}")]);
        Write(bot);
        page.Refresh(_host.Services);
        await page.SaveAsync(path);

        var lines = File.ReadAllLines(path);

        Assert.Equal($"{JournalText.TimeOf(before)} NoteEntry {{ Text = before the restart }}", lines[0]);
        Assert.Contains(lines, line => line.Contains("WalkEntry", StringComparison.Ordinal) && line.Contains("Cell = 388", StringComparison.Ordinal));
        Assert.Equal("Journal enregistré : saved.txt", toasts.Recent[^1].Text);
        Assert.Equal(Tone.Success, toasts.Recent[^1].Tone);
    }

    [Fact]
    public async Task SavingEverySourceWritesTheHostThenEachBotUnderItsName()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice(), new Toasts());
        var path = _journals.Combine("all.txt");

        _problems.Problems.Add(new HostProblem(_at, LogLevel.Warning, "D3B.Network.Relay", "The relay failed", null));
        page.Refresh(_host.Services);
        page.Sources[0].SelectCommand.Execute(null);
        await page.SaveAsync(path);

        var text = File.ReadAllText(path);

        Assert.StartsWith("# Hôte\n2026-10-29T03:17:19.000Z Warning D3B.Network.Relay: The relay failed\n", text, StringComparison.Ordinal);
        Assert.Contains($"\n# {page.Sources[2].Name}\n", text, StringComparison.Ordinal);
        Assert.Contains("BotEventEntry", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AJournalIsOfferedUnderItsSourceAndItsDay()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());

        page.Refresh(_host.Services);

        Assert.Equal($"journal-{JournalFiles.SourceOf(bot.Key)}-{_play.Time.GetUtcNow():yyyy-MM-dd}.txt", page.SaveName());

        page.ShowHost();

        Assert.Equal($"journal-host-{_play.Time.GetUtcNow():yyyy-MM-dd}.txt", page.SaveName());
    }

    [Fact]
    public async Task WithoutJournalsOnDiskTheLinesShownAreSaved()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        using var host = builder.Build();
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());
        var path = _journals.Combine("shown.txt");

        page.Refresh(host.Services);
        await page.SaveAsync(path);

        Assert.Equal(page.Text(), File.ReadAllText(path));
        Assert.Contains("Joue par la connexion de jeu", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AJournalThatCannotBeSavedSaysWhy()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var toasts = new Toasts();
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, toasts);

        page.Refresh(_host.Services);
        await page.SaveAsync(_journals.Combine("missing", "folder", "x.txt"));

        Assert.Equal(Tone.Danger, toasts.Recent[^1].Tone);
        Assert.StartsWith("Le journal n’a pas pu être enregistré", toasts.Recent[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTheDiskCannotBeWrittenTheLinesShownAreSavedInstead()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var blocked = _journals.Combine("blocked");

        // A file where the folder should be: every write of the recorder fails.
        File.WriteAllText(blocked, "");

        using var recorder = new JournalRecorder(new JournalFiles(blocked), JournalRetention.Default, _play.Bots, _problems, _play.Time, NullLogger<JournalRecorder>.Instance);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(recorder);
        using var host = builder.Build();
        var page = new LogsViewModel(_french, new BotChoice { Key = bot.Key }, new Toasts());
        var path = _journals.Combine("shown.txt");

        page.Refresh(host.Services);
        await page.SaveAsync(path);

        Assert.Equal(page.Text(), File.ReadAllText(path));
        Assert.NotEmpty(page.Text());
    }
}
