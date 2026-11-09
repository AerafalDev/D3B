using System.Text.Json.Nodes;
using System.Threading.Channels;
using D3B.Bot.Characters;
using D3B.Studio.Behaviors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Variables;

/// <summary>Keeps the variables each character keeps, so that the next run reads them from its start (D298).</summary>
/// <remarks>
/// Taken back before the host starts (<see cref="StoragePreparation"/>). A variable is set from a behavior's step or the
/// bot's loop, which must not wait: it is queued, and written by the recorder's own loop with what the host holds of it
/// then. A write that fails is logged, and the bots go on reading it until the host stops.
/// </remarks>
internal sealed class VariableRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly CharacterVariables _variables;
    private readonly ILogger<VariableRecorder> _log;
    private readonly Channel<((CharacterKey Character, string Name)? Variable, TaskCompletionSource? Written)> _queue =
        Channel.CreateUnbounded<((CharacterKey, string)?, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    public VariableRecorder(IDbContextFactory<StorageContext> contexts, CharacterVariables variables, ILogger<VariableRecorder> log)
    {
        _contexts = contexts;
        _variables = variables;
        _log = log;
        _variables.Changed += OnChanged;
    }

    /// <summary>Takes back the variables the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many.</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var kept = await context.Variables.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var variable in kept)
            {
                if (JsonNode.Parse(variable.Value) is { } value)
                    _variables.Remember(new CharacterKey(variable.AccountId, variable.CharacterId), variable.Name, value);
            }

            return kept.Count;
        }
    }

    /// <summary>Waits until every variable set so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite((null, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _variables.Changed -= OnChanged;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new HashSet<(CharacterKey Character, string Name)>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else if (item.Variable is { } variable)
                    batch.Add(variable);
            }

            try
            {
                if (batch.Count > 0)
                    await WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.VariablesFailed(_log, exception);
            }

            batch.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private void OnChanged(object? sender, CharacterVariableChange change)
    {
        _queue.Writer.TryWrite(((change.Character, change.Name), null));
    }

    private async Task WriteAsync(HashSet<(CharacterKey Character, string Name)> batch, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            foreach (var (character, name) in batch)
            {
                if (_variables.Get(character, name) is not { } value)
                    continue;

                var known = await context.Variables.FindAsync([character.AccountId, character.CharacterId, name], cancellationToken).ConfigureAwait(false);

                if (known is null)
                    context.Variables.Add(new KeptVariable { AccountId = character.AccountId, CharacterId = character.CharacterId, Name = name, Value = value.ToJsonString() });
                else
                    known.Value = value.ToJsonString();
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
