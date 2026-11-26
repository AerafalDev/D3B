using System.Threading.Channels;
using D3B.Bot.Harvesting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Harvests;

/// <summary>Keeps what each map holds, as the host's bots see it, so that the next run's tours know it from their start (D584).</summary>
/// <remarks>
/// As the graphics' recorder does (D138): read back before the host starts, a map whose elements changed queued on the bot's
/// thread and written by the recorder's own loop with what the host knows of it then. Whether each element offered its skill is
/// not kept: it changes at every harvest, and a run that starts hours later cannot trust it; a map taken back is gone through
/// as one whose elements all offer theirs. A write that fails is logged, and the bots go on knowing the map until the host stops.
/// </remarks>
internal sealed class SightingRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly HarvestSightings _sightings;
    private readonly ILogger<SightingRecorder> _log;
    private readonly Channel<(long MapId, TaskCompletionSource? Written)> _queue = Channel.CreateUnbounded<(long, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    public SightingRecorder(IDbContextFactory<StorageContext> contexts, HarvestSightings sightings, ILogger<SightingRecorder> log)
    {
        _contexts = contexts;
        _sightings = sightings;
        _log = log;
        _sightings.Learned += OnLearned;
    }

    /// <summary>Takes back what the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many maps.</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var sightings = await context.Sightings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var sighting in sightings)
                _sightings.Remember(sighting.MapId, [.. sighting.Elements.Select(element => new SightedElement(element.ElementId, element.SkillId, element.ItemId, IsEnabled: true))], sighting.SeenAt);

            return sightings.Count;
        }
    }

    /// <summary>Waits until every map learned so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite((0, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _sightings.Learned -= OnLearned;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new HashSet<long>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else
                    batch.Add(item.MapId);
            }

            try
            {
                if (batch.Count > 0)
                    await WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.SightingsFailed(_log, exception);
            }

            batch.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private void OnLearned(long mapId)
    {
        _queue.Writer.TryWrite((mapId, null));
    }

    private async Task WriteAsync(HashSet<long> batch, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var known = await context.Sightings.Where(sighting => batch.Contains(sighting.MapId)).ToDictionaryAsync(sighting => sighting.MapId, cancellationToken).ConfigureAwait(false);

            foreach (var mapId in batch)
            {
                if (!_sightings.TryGet(mapId, out var sighting))
                    continue;

                if (!known.TryGetValue(mapId, out var learned))
                    context.Sightings.Add(learned = new LearnedSighting { MapId = mapId });

                learned.SeenAt = sighting.At;
                learned.Elements = sighting.Elements.IsDefault ? [] : [.. sighting.Elements.Select(element => new KeptElement(element.ElementId, element.SkillId, element.ItemId)).OrderBy(element => element.ElementId).ThenBy(element => element.SkillId)];
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
