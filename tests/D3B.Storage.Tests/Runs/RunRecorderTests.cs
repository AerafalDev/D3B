using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Protocol.Game;
using D3B.Storage.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Runs;

public sealed class RunRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static JobExperiencesEvent Lumberjack(int level, long experience)
    {
        return new JobExperiencesEvent { Jobs = { new JobExperience { JobId = 2, Level = level, Experience = experience, LevelFloor = 0, NextLevelFloor = 10_000 } } };
    }

    [Fact]
    public async Task ARunIsKeptAsItGoesAndOnceOverWithWhatItBroughtAndTheExperienceGained()
    {
        var path = _directory.Combine("d3b.db");
        var cancellation = TestContext.Current.CancellationToken;

        await using (var storage = await new TestStorage(path).StartedAsync())
        {
            var (connection, bot) = await storage.Play.EnterAsync(300);
            var recorder = storage.Services.GetRequiredService<RunRecorder>();
            var book = storage.Services.GetRequiredService<RunBook>();
            var activity = new CountingActivity();

            connection.Server(Lumberjack(10, 1_000));
            await bot.ProcessedAsync();

            var run = bot.StartActivity(activity, "Ortie");

            await recorder.PollAsync(cancellation);

            var begun = Assert.Single(book.Runs);

            Assert.Equal(("Ortie", "CountingActivity", null, 0), (begun.Title, begun.Activity, begun.Outcome, begun.Items.Count));

            // Banked: written at once, with the experience gained since the run began.
            activity.Figures = new ActivityStatistics
            {
                Items = ImmutableDictionary<int, long>.Empty.Add(303, 12),
                Banks = 1,
                Fights = ImmutableDictionary<FightKind, int>.Empty.Add(FightKind.Protector, 1),
            };
            connection.Server(Lumberjack(11, 1_800));
            await bot.ProcessedAsync();
            await recorder.PollAsync(cancellation);

            var banked = Assert.Single(book.Runs);

            Assert.Equal(12, banked.Items[303]);
            Assert.Equal((1, 1, 0), (banked.Banks, banked.Protectors, banked.Aggressions));
            Assert.Equal([new RunJob(2, 10, 11, 800)], banked.Jobs);

            run.Stop();
            await run.Completion;
            await recorder.PollAsync(cancellation);

            var over = Assert.Single(book.Runs);

            Assert.Equal(ActivityOutcome.Stopped, over.Outcome);
            Assert.NotNull(over.EndedAt);
        }

        // Read back from the database at the next run's start.
        await using var next = await new TestStorage(path).StartedAsync();
        var kept = Assert.Single(next.Services.GetRequiredService<RunBook>().Runs);

        Assert.Equal((ActivityOutcome.Stopped, 12L, 800L), (kept.Outcome, kept.Items[303], kept.Jobs.Single().Experience));
    }

    [Fact]
    public async Task ARunKeepsTheFightsItsCharacterWonAndLostItsDeathsAndTheExperienceAndKamasTheyGave()
    {
        var path = _directory.Combine("d3b.db");
        var cancellation = TestContext.Current.CancellationToken;

        await using (var storage = await new TestStorage(path).StartedAsync())
        {
            var (connection, bot) = await storage.Play.EnterAsync(300);
            var recorder = storage.Services.GetRequiredService<RunRecorder>();
            var run = bot.StartActivity(new CountingActivity(), "Bouftous");

            await recorder.PollAsync(cancellation);

            connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
            connection.Server(new FightEndEvent { Results = { Result(FightOutcome.Won, 1_250, 40) } });
            connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
            connection.Server(new FightEndEvent { Results = { Result(FightOutcome.Lost, 0, 0) } });
            await bot.ProcessedAsync();

            run.Stop();
            await run.Completion;
            await recorder.PollAsync(cancellation);
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var kept = Assert.Single(next.Services.GetRequiredService<RunBook>().Runs);

        Assert.Equal((1, 1, 0, 1_250L, 40L), (kept.FightsWon, kept.FightsLost, kept.Deaths, kept.Experience, kept.Kamas));
    }

    private static FightResult Result(FightOutcome outcome, long experience, long kamas)
    {
        return new FightResult
        {
            Outcome = outcome,
            Fighter = new FightResultFighter
            {
                FighterId = TestPlay.CharacterId,
                Character = new FightResultCharacter { Data = { new FightResultData { Experience = new FightExperience { Gained = experience } } } },
            },
            Loot = new FightLoot { Kamas = kamas },
        };
    }

    private sealed class CountingActivity : BotActivity
    {
        private ActivityStatistics? _figures;

        public override ActivityStatistics? Statistics => Volatile.Read(ref _figures);

        public ActivityStatistics? Figures
        {
            get => Volatile.Read(ref _figures);
            set => Volatile.Write(ref _figures, value);
        }

        public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return true;
        }
    }
}
