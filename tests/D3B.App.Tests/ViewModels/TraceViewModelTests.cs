using D3B.App.Localization;
using D3B.App.ViewModels.Bottom;
using D3B.Bot.Activities;
using D3B.Bot.Behaviors;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class TraceViewModelTests : IAsyncDisposable
{
    private static readonly DateTimeOffset _first = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _second = new(2026, 11, 2, 14, 30, 0, TimeSpan.Zero);

    private readonly TestPlay _play = new();
    private readonly IHost _host;
    private readonly Localizer _french = new(Localizer.French);

    public TraceViewModelTests()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        await _play.DisposeAsync();
    }

    private static TraceEntry Line(DateTimeOffset run, int seconds, string? state, TraceKind kind, TraceOutcome outcome)
    {
        return new TraceEntry(run.AddSeconds(seconds), run, run == _first ? "Ortie" : "Mineur Astrub", state, kind, outcome);
    }

    [Fact]
    public async Task TheLatestRunsDecisionsAreSaidNewestFirstEachWithHowItEnded()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var trace = new TraceViewModel(_french);

        bot.Trace.Add(Line(_first, 1, "Récolter", TraceKind.Entered, TraceOutcome.None));
        bot.Trace.Add(Line(_second, 1, "Récolter", TraceKind.Entered, TraceOutcome.None));
        bot.Trace.Add(Line(_second, 2, "Récolter", TraceKind.Weighed, TraceOutcome.DoesNotHold) with { Watch = new ActivityWatch(WatchedGauge.Pods, 90, WatchAction.Bank) });
        bot.Trace.Add(Line(_second, 3, "Récolter", TraceKind.Played, TraceOutcome.Played) with { Node = LocalizedText.Invariant("Récolter le fer") });
        bot.Trace.Add(Line(_second, 4, "Récolter", TraceKind.Urgent, TraceOutcome.None) with { Subject = "Soin" });
        bot.Trace.Add(Line(_second, 4, "Récolter", TraceKind.Played, TraceOutcome.Interrupted) with { Node = LocalizedText.Invariant("Récolter le fer") });
        bot.Trace.Add(Line(_second, 5, null, TraceKind.Waiting, TraceOutcome.None));
        trace.Refresh(_host.Services);

        Assert.Equal("Mineur Astrub", trace.Behavior);
        Assert.Equal([_second, _first], trace.Runs.Select(run => run.Run));
        Assert.Equal(
            [
                ("Toujours", "Aucun état ne tient", "", TraceMark.Always),
                ("Récolter", "Récolter le fer", "Interrompu", TraceMark.Playing),
                ("Récolter", "Coupé pour Soin", "", TraceMark.Cut),
                ("Récolter", "Récolter le fer", "Joué", TraceMark.Playing),
                ("Toujours", "Pods ≥ 90 % → Banque", "Non", TraceMark.Always),
                ("Récolter", "Entre dans Récolter", "", TraceMark.Playing),
            ],
            trace.Lines.Select(line => (line.State, line.Text, line.Outcome, line.Mark)));
        Assert.True(trace.Lines[1].IsBad);
        Assert.True(trace.Lines[3].IsGood);
    }

    [Fact]
    public async Task AnOlderRunChosenStaysChosenAsLinesComeAndTheLatestIsFollowedOtherwise()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var trace = new TraceViewModel(_french);

        bot.Trace.Add(Line(_first, 1, "Ortie", TraceKind.Entered, TraceOutcome.None));
        bot.Trace.Add(Line(_second, 1, "Récolter", TraceKind.Entered, TraceOutcome.None));
        trace.Refresh(_host.Services);
        trace.Run = trace.Runs[1];

        Assert.Equal(["Entre dans Ortie"], trace.Lines.Select(line => line.Text));

        bot.Trace.Add(Line(_second, 2, "Récolter", TraceKind.Played, TraceOutcome.Done) with { Node = LocalizedText.Invariant("Récolter") });
        trace.Refresh(_host.Services);

        Assert.Equal(_first, trace.Run?.Run);
        Assert.Single(trace.Lines);

        trace.Run = trace.Runs[0];

        Assert.Equal(["Récolter", "Entre dans Récolter"], trace.Lines.Select(line => line.Text));
        Assert.Equal("Terminé", trace.Lines[0].Outcome);
    }

    [Fact]
    public async Task WithoutABotOrADecisionTheTraceSaysSo()
    {
        var trace = new TraceViewModel(_french);

        trace.Refresh(_host.Services);

        Assert.True(trace.IsEmpty);
        Assert.Equal("Aucun personnage n'est connecté.", trace.EmptyText);

        var (_, bot) = await _play.EnterAsync(300);

        trace.Follow(bot.Key);
        trace.Refresh(_host.Services);

        Assert.Equal(bot.Key, trace.Bot?.Bot?.Key);
        Assert.True(trace.IsEmpty);
        Assert.StartsWith("Aucune décision", trace.EmptyText, StringComparison.Ordinal);
    }
}
