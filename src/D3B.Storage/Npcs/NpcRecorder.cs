using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Channels;
using D3B.Bot.Npcs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Npcs;

/// <summary>Keeps the NPCs the host's bots saw on the maps and what they learned of the NPCs' dialogs, so that they outlive the run (D383).</summary>
/// <remarks>
/// As the placements' recorder does (D275): read back before the host starts, what is learned on a relay's read loop queued
/// and written by the recorder's own loop. A write that fails is logged, and the host goes on knowing it until it stops.
/// </remarks>
internal sealed class NpcRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly KnownNpcs _known;
    private readonly ILogger<NpcRecorder> _log;
    private readonly Channel<Item> _queue = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });

    public NpcRecorder(IDbContextFactory<StorageContext> contexts, KnownNpcs known, ILogger<NpcRecorder> log)
    {
        _contexts = contexts;
        _known = known;
        _log = log;
        _known.NpcSeen += OnNpc;
        _known.QuestionSeen += OnQuestion;
        _known.OutcomeSeen += OnOutcome;
    }

    private static ImmutableArray<int> Replies(string replies)
    {
        return [.. replies.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(reply => int.Parse(reply, CultureInfo.InvariantCulture))];
    }

    /// <summary>Takes back what the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many NPCs, questions and outcomes.</returns>
    public async Task<(int Npcs, int Questions, int Outcomes)> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var npcs = await context.MapNpcs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var questions = await context.NpcQuestions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var outcomes = await context.NpcOutcomes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var npc in npcs)
                _known.Remember(new KnownMapNpc(npc.MapId, npc.NpcId, npc.ActorId, npc.Cell, npc.Direction, npc.Seen, npc.FirstSeenAt, npc.LastSeenAt));

            foreach (var question in questions)
                _known.Remember(new KnownNpcQuestion(question.MapId, question.NpcId, question.MessageId, Replies(question.Replies), question.Opened, question.Seen, question.FirstSeenAt, question.LastSeenAt));

            foreach (var outcome in outcomes)
                _known.Remember(new KnownNpcOutcome(outcome.MapId, outcome.NpcId, outcome.MessageId, outcome.ReplyId, outcome.Kind, outcome.Target, outcome.Count, outcome.LastSeenAt));

            return (npcs.Count, questions.Count, outcomes.Count);
        }
    }

    /// <summary>Waits until everything learned so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite(new Item(null, null, null, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _known.NpcSeen -= OnNpc;
        _known.QuestionSeen -= OnQuestion;
        _known.OutcomeSeen -= OnOutcome;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var npcs = new Dictionary<(long, int), KnownMapNpc>();
        var questions = new Dictionary<(long, int, int, string), KnownNpcQuestion>();
        var outcomes = new Dictionary<(long, int, int, int, NpcOutcomeKind, long), KnownNpcOutcome>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else if (item.Npc is { } npc)
                    npcs[(npc.MapId, npc.NpcId)] = npc;
                else if (item.Question is { } question)
                    questions[(question.MapId, question.NpcId, question.MessageId, KnownNpcs.Key(question.Replies))] = question;
                else if (item.Outcome is { } outcome)
                    outcomes[(outcome.MapId, outcome.NpcId, outcome.MessageId, outcome.ReplyId, outcome.Kind, outcome.Target)] = outcome;
            }

            try
            {
                if (npcs.Count > 0 || questions.Count > 0 || outcomes.Count > 0)
                    await WriteAsync(npcs, questions, outcomes, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.NpcsFailed(_log, exception);
            }

            npcs.Clear();
            questions.Clear();
            outcomes.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private async Task WriteAsync(
        Dictionary<(long, int), KnownMapNpc> npcs,
        Dictionary<(long, int, int, string), KnownNpcQuestion> questions,
        Dictionary<(long, int, int, int, NpcOutcomeKind, long), KnownNpcOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var maps = npcs.Keys.Select(key => key.Item1)
                .Concat(questions.Keys.Select(key => key.Item1))
                .Concat(outcomes.Keys.Select(key => key.Item1))
                .Distinct()
                .ToList();

            var knownNpcs = await context.MapNpcs.Where(npc => maps.Contains(npc.MapId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var knownQuestions = await context.NpcQuestions.Where(question => maps.Contains(question.MapId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var knownOutcomes = await context.NpcOutcomes.Where(outcome => maps.Contains(outcome.MapId)).ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var npc in npcs.Values)
            {
                var row = knownNpcs.Find(stored => stored.MapId == npc.MapId && stored.NpcId == npc.NpcId);

                if (row is null)
                {
                    row = new LearnedMapNpc { MapId = npc.MapId, NpcId = npc.NpcId, FirstSeenAt = npc.FirstSeen };
                    context.MapNpcs.Add(row);
                    knownNpcs.Add(row);
                }

                row.ActorId = npc.ActorId;
                row.Cell = npc.Cell;
                row.Direction = npc.Direction;
                row.Seen = npc.Seen;
                row.LastSeenAt = npc.LastSeen;
            }

            foreach (var question in questions.Values)
            {
                var replies = KnownNpcs.Key(question.Replies);
                var row = knownQuestions.Find(stored => stored.MapId == question.MapId && stored.NpcId == question.NpcId && stored.MessageId == question.MessageId && stored.Replies == replies);

                if (row is null)
                {
                    row = new LearnedNpcQuestion { MapId = question.MapId, NpcId = question.NpcId, MessageId = question.MessageId, Replies = replies, FirstSeenAt = question.FirstSeen };
                    context.NpcQuestions.Add(row);
                    knownQuestions.Add(row);
                }

                row.Opened = question.Opened;
                row.Seen = question.Seen;
                row.LastSeenAt = question.LastSeen;
            }

            foreach (var outcome in outcomes.Values)
            {
                var row = knownOutcomes.Find(stored => stored.MapId == outcome.MapId && stored.NpcId == outcome.NpcId && stored.MessageId == outcome.MessageId && stored.ReplyId == outcome.ReplyId && stored.Kind == outcome.Kind && stored.Target == outcome.Target);

                if (row is null)
                {
                    row = new LearnedNpcOutcome { MapId = outcome.MapId, NpcId = outcome.NpcId, MessageId = outcome.MessageId, ReplyId = outcome.ReplyId, Kind = outcome.Kind, Target = outcome.Target };
                    context.NpcOutcomes.Add(row);
                    knownOutcomes.Add(row);
                }

                row.Count = outcome.Count;
                row.LastSeenAt = outcome.LastSeen;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnNpc(KnownMapNpc npc)
    {
        _queue.Writer.TryWrite(new Item(npc, null, null, null));
    }

    private void OnQuestion(KnownNpcQuestion question)
    {
        _queue.Writer.TryWrite(new Item(null, question, null, null));
    }

    private void OnOutcome(KnownNpcOutcome outcome)
    {
        _queue.Writer.TryWrite(new Item(null, null, outcome, null));
    }

    private readonly record struct Item(KnownMapNpc? Npc, KnownNpcQuestion? Question, KnownNpcOutcome? Outcome, TaskCompletionSource? Written);
}
