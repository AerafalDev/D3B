using D3B.Bot.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Hosting;

public sealed partial class HostProblemsTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 4, 17, 11, TimeSpan.Zero));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Script {Script} does not read: {Error}")]
    private static partial void ScriptBroken(ILogger logger, string script, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: activity {Activity} failed")]
    private static partial void ActivityFailed(ILogger logger, long character, string activity, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {Count} scripts")]
    private static partial void Listed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "The relay failed")]
    private static partial void RelayFailed(ILogger logger, Exception exception);

    [Fact]
    public void TheHostsWarningsAndErrorsAreKeptAsTheLogWroteThem()
    {
        using var problems = new HostProblems(_time);
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(problems).SetMinimumLevel(LogLevel.Debug));
        var log = factory.CreateLogger("D3B.Scripting.Library.ScriptLibrary");

        ScriptBroken(log, "mineur", "mineur.lua:3: unexpected symbol");
        RelayFailed(log, new IOException("The pipe broke."));

        var kept = problems.Problems.All();

        Assert.Equal(2, kept.Count);
        Assert.Equal(new HostProblem(_time.GetUtcNow(), LogLevel.Warning, "D3B.Scripting.Library.ScriptLibrary", "Script mineur does not read: mineur.lua:3: unexpected symbol", null), kept[0]);
        Assert.Equal(LogLevel.Error, kept[1].Level);
        Assert.Contains("The pipe broke.", kept[1].Exception, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatNamesABotOrInformsIsNotKept()
    {
        using var problems = new HostProblems(_time);
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(problems).SetMinimumLevel(LogLevel.Debug));
        var log = factory.CreateLogger("D3B.Bot.Runtime.BotActor");

        // The bot's journal says it already, in the app's words.
        ActivityFailed(log, 70_001, "promenade", new InvalidOperationException("The step failed."));
        Listed(log, 2);

        Assert.Empty(problems.Problems.All());
    }
}
