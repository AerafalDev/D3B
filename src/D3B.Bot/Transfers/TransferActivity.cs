using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Data;

namespace D3B.Bot.Transfers;

/// <summary>Moves objects between the character's bank and its guild's chest, either way, in as many rounds as its pods need, then says what it moved (D550).</summary>
/// <remarks>
/// <para>
/// A round goes to the source, the nearest banker or guild's chest, opens it, takes out what the settings choose and the pods hold,
/// a stack at a time, and leaves; then goes to the destination, puts in what it took, and leaves (D551). The chest stands in banks'
/// insides (D153), so a round's journeys are short, often none. Only what the transfer took moves: what the character carried before
/// stays in its bags. The guild's chest holds no kamas and opens on one tab, the one the transfer works on.
/// </para>
/// <para>
/// Each round weighs the takes with the pack's weights, so that the character never carries more than its pods; a take the server
/// still refuses for the pods ends that opening's takes. An item the source or the destination refuses is given up for the run, the
/// destination's refusal answered as the settings say. A rank without the right to take out or put in, or a source or destination out
/// of reach once the recovery ladder is climbed (D518), ends the transfer, saying why. It is done once the source holds nothing more
/// it may move, or, for a dry run, once it said what it would move.
/// </para>
/// </remarks>
public sealed class TransferActivity : BotActivity
{
    // As the zaaps' discovery (D167): a zaap costs kamas and saves little on the short ways between banks.
    private const int ZaapSaving = 5;

    private readonly TransferSettings _settings;
    private readonly TransferFilter _filter;
    private readonly IReadOnlySet<long> _chests;
    private readonly BankingPreferences _banking;
    private readonly RecoveryLadder _openings = new();
    private readonly BusyWait _busy = new();
    private readonly Dictionary<int, long> _taken = [];
    private readonly Dictionary<int, int> _carried = [];
    private readonly Dictionary<int, int> _givingBack = [];
    private readonly SortedDictionary<int, long> _moved = [];
    private readonly SortedDictionary<int, StorageMoveOutcome> _givenUp = [];
    private readonly string _source;
    private readonly string _destination;
    private int _rounds;
    private volatile ActivityReport? _report;

    /// <inheritdoc/>
    public override ActivityReport? Report => _report;

    /// <inheritdoc/>
    public override int TeleportChanges => ZaapSaving;

    /// <inheritdoc/>
    public override bool UsesTeleports => _settings.UseZaaps;

    /// <inheritdoc/>
    public override bool BanksByTeleport => _banking.UseZaapsOf(false, _settings.UseZaaps);

    private bool FromBank => _settings.Direction is TransferDirection.BankToGuildChest;

    /// <summary>Initializes a new instance of the <see cref="TransferActivity"/> class.</summary>
    /// <param name="settings">The transfer's settings.</param>
    /// <param name="tables">The installed build's tables: the items, their types and weights.</param>
    /// <param name="places">Where each graphic is drawn, which says where the guild's chests stand.</param>
    /// <param name="banking">The general banking settings, whose zaap rule the journeys follow (D405); the defaults when none is given.</param>
    public TransferActivity(TransferSettings settings, GameTables tables, GraphicPlaces places, BankingPreferences? banking = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(places);

        _settings = settings;
        _filter = new TransferFilter(settings, tables.Items);
        _chests = places.MapsDrawing(GuildChest.GfxId);
        _banking = banking ?? new BankingPreferences();
        _source = PlaceOf(FromBank);
        _destination = PlaceOf(!FromBank);
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(TransferTexts.Text(key))) { IsFinal = true };
    }

    private static string PlaceOf(bool bank)
    {
        return bank ? "bank" : "guild's chest";
    }

    private static ImmutableArray<ActivityCount> Counts(SortedDictionary<int, long> counts)
    {
        return [.. counts.Select(entry => new ActivityCount(GameText.Of(TransferTexts.Text("CountItem"), GameTextValue.Item(entry.Key), GameTextValue.Number(entry.Value)), entry.Key))];
    }

    private static async Task LeaveAsync(BotContext context, CancellationToken cancellationToken)
    {
        // The client shows the storage's last change before the leaving closes it (D132).
        await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
        await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        // The recovery after an interruption puts the character back in order: the bounded wait counts anew (D484).
        _busy.Reset();

        return base.OnInterrupted(reason);
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _busy.WaitAsync(context, Failure, cancellationToken).ConfigureAwait(false))
            return true;

        return _carried.Count > 0
            ? await PutAsync(context, cancellationToken).ConfigureAwait(false)
            : await TakeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TakeAsync(BotContext context, CancellationToken cancellationToken)
    {
        Publish(context.State, FromBank ? "DoingTakeBank" : "DoingTakeChest");
        await OpenAsync(context, FromBank, cancellationToken).ConfigureAwait(false);
        await GiveBackAsync(context, cancellationToken).ConfigureAwait(false);

        if (_settings.DryRun)
            return await SayWhatWouldMoveAsync(context, cancellationToken).ConfigureAwait(false);

        var took = false;
        var tooHeavy = false;
        var moves = 0;
        var character = context.State.Key.CharacterId;

        foreach (var stack in Stored(context.State))
        {
            var state = context.State;

            if (state.Storage is null)
                break;

            var quantity = Math.Min(Allowance(state, stack.ItemId), Fitting(state, stack.ItemId));

            if (quantity <= 0)
            {
                tooHeavy |= Allowance(state, stack.ItemId) > 0;
                continue;
            }

            // Measured: moved 28 ms apart, the stacks went faster than the client showed them (D153).
            if (moves++ > 0)
                await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            var quantityTaken = Math.Min(quantity, stack.Quantity);

            switch (await context.TakeFromStorageAsync(stack.Uid, quantityTaken, cancellationToken).ConfigureAwait(false))
            {
                case StorageMoveOutcome.Moved:
                    took = true;
                    _taken[stack.ItemId] = _taken.GetValueOrDefault(stack.ItemId) + quantityTaken;
                    _carried[stack.ItemId] = _carried.GetValueOrDefault(stack.ItemId) + quantityTaken;
                    TransferLog.Took(context.Log, character, stack.ItemId, quantityTaken, _source);
                    break;
                case StorageMoveOutcome.TooHeavy:
                    tooHeavy = true;
                    break;
                case StorageMoveOutcome.NoRight:
                    await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
                    throw Failure("The guild's rank has no right to take objects out of the chest.", "FailNoTakeRight");
                case StorageMoveOutcome.Busy:
                    break;
                case var outcome:
                    GiveUp(context, stack.ItemId, outcome);
                    break;
            }
        }

        await LeaveAsync(context, cancellationToken).ConfigureAwait(false);

        if (took)
            return true;

        // Nothing fit though something was left to move: the character's own load leaves no room, which no round would change.
        if (tooHeavy)
            throw Failure("The character carries too much to take anything out.", "FailNoPods");

        End(context);

        return false;
    }

    private async Task<bool> PutAsync(BotContext context, CancellationToken cancellationToken)
    {
        Publish(context.State, FromBank ? "DoingPutChest" : "DoingPutBank");
        await OpenAsync(context, !FromBank, cancellationToken).ConfigureAwait(false);

        var character = context.State.Key.CharacterId;
        var moves = 0;

        foreach (var itemId in _carried.Keys.Order().ToList())
        {
            var left = _carried[itemId];
            var refusal = StorageMoveOutcome.Moved;

            foreach (var stack in context.State.Inventory.Values.Where(stack => stack.ItemId == itemId && !stack.IsWorn).ToList())
            {
                if (left <= 0 || refusal is not StorageMoveOutcome.Moved)
                    break;

                if (moves++ > 0)
                    await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

                var quantity = Math.Min(left, stack.Quantity);

                refusal = await context.PutInStorageAsync(stack.Uid, quantity, cancellationToken).ConfigureAwait(false);

                if (refusal is StorageMoveOutcome.Moved)
                {
                    left -= quantity;
                    _moved[itemId] = _moved.GetValueOrDefault(itemId) + quantity;
                    TransferLog.Put(context.Log, character, itemId, quantity, _destination);
                }
            }

            _carried.Remove(itemId);

            // A stack the character no longer carries, such as one the player moved, is not looked for.
            if (left <= 0 || refusal is StorageMoveOutcome.Moved or StorageMoveOutcome.Busy)
                continue;

            if (refusal is StorageMoveOutcome.NoRight)
            {
                await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
                throw Failure("The guild's rank has no right to put objects in the chest.", "FailNoPutRight");
            }

            GiveUp(context, itemId, refusal);

            switch (_settings.WhenRefused)
            {
                case TransferRefusal.GiveBack:
                    _givingBack[itemId] = _givingBack.GetValueOrDefault(itemId) + left;
                    break;
                case TransferRefusal.Stop:
                    await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
                    End(context);
                    throw new ActivityException(
                        $"The destination refused item {itemId}.",
                        GameText.Of(TransferTexts.Text("FailRefused"), GameTextValue.Item(itemId)))
                    { IsFinal = true };
            }
        }

        await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
        _rounds++;

        return true;
    }

    private async Task GiveBackAsync(BotContext context, CancellationToken cancellationToken)
    {
        foreach (var (itemId, quantity) in _givingBack.OrderBy(entry => entry.Key).ToList())
        {
            var left = quantity;

            foreach (var stack in context.State.Inventory.Values.Where(stack => stack.ItemId == itemId && !stack.IsWorn).ToList())
            {
                if (left <= 0)
                    break;

                var put = Math.Min(left, stack.Quantity);

                // What the source refuses back stays in the bags: it took it out a moment ago, so this is not expected.
                if (await context.PutInStorageAsync(stack.Uid, put, cancellationToken).ConfigureAwait(false) is not StorageMoveOutcome.Moved)
                    break;

                left -= put;
                await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
            }

            _givingBack.Remove(itemId);
        }
    }

    private async Task<bool> SayWhatWouldMoveAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;
        var would = new SortedDictionary<int, long>();

        foreach (var stack in Stored(state))
        {
            var quantity = Math.Min(Allowance(state, stack.ItemId) - would.GetValueOrDefault(stack.ItemId), stack.Quantity);

            if (quantity > 0)
                would[stack.ItemId] = would.GetValueOrDefault(stack.ItemId) + quantity;
        }

        foreach (var (itemId, quantity) in would)
            TransferLog.Would(context.Log, state.Key.CharacterId, itemId, quantity, _source);

        await LeaveAsync(context, cancellationToken).ConfigureAwait(false);

        _report = new ActivityReport
        {
            Doing = GameText.Of(TransferTexts.Text("DoingDryRun"), GameTextValue.Number(would.Values.Sum()), GameTextValue.Number(would.Count)),
            Counts = Counts(would),
        };

        return false;
    }

    private async Task OpenAsync(BotContext context, bool bank, CancellationToken cancellationToken)
    {
        var useZaaps = BanksByTeleport;

        // Measured on the owner's miner (D517, D518): any opening that fails is tried again up the ladder, but a place the world
        // lacks, or a chest the guild's rights keep closed, which no other try would change.
        var outcome = await _openings.TryAsync(
            context,
            $"the {PlaceOf(bank)}",
            async token =>
            {
                var reached = bank
                    ? await context.GoToBankerAsync(useZaaps, token).ConfigureAwait(false)
                    : await context.GoToGuildChestAsync(_chests, useZaaps, token).ConfigureAwait(false);

                if (reached is not BankOutcome.Done)
                    return reached;

                return bank ? await context.OpenBankAsync(token).ConfigureAwait(false) : await context.OpenGuildChestAsync(token).ConfigureAwait(false);
            },
            opened => opened is BankOutcome.Done or BankOutcome.NoBank || opened is BankOutcome.Refused && !bank,
            cancellationToken).ConfigureAwait(false);

        switch (outcome)
        {
            case BankOutcome.Done:
                return;
            case BankOutcome.NoBank:
                throw Failure($"No {PlaceOf(bank)} can be reached.", bank ? "FailNoBank" : "FailNoChest");
            case BankOutcome.Refused when !bank:
                throw Failure("The server keeps the guild's chest closed to the character.", "FailChestClosed");
            default:
                throw Failure($"The {PlaceOf(bank)} could not be opened, even after the recovery ladder: {outcome}.", bank ? "FailBankNotOpened" : "FailChestNotOpened");
        }
    }

    private List<StorageItem> Stored(CharacterState state)
    {
        return [.. (state.Storage?.Stacks.Values ?? []).Where(stack => !_givenUp.ContainsKey(stack.ItemId) && _filter.Moves(stack.ItemId)).OrderBy(stack => stack.ItemId).ThenBy(stack => stack.Uid)];
    }

    private int Allowance(CharacterState state, int itemId)
    {
        var allowance = _settings.Quantity switch
        {
            TransferQuantity.AtMost => _settings.Amount - _taken.GetValueOrDefault(itemId),
            TransferQuantity.KeepInSource => (state.Storage?.Stacks.Values.Where(stack => stack.ItemId == itemId).Sum(stack => (long)stack.Quantity) ?? 0) - _settings.Amount,
            _ => int.MaxValue,
        };

        return (int)Math.Clamp(allowance, 0, int.MaxValue);
    }

    private int Fitting(CharacterState state, int itemId)
    {
        var weight = _filter.WeightOf(itemId);

        if (weight <= 0)
            return int.MaxValue;

        return state.MaxWeight > 0 ? Math.Max(0, state.MaxWeight - state.Weight) / weight : 0;
    }

    private void GiveUp(BotContext context, int itemId, StorageMoveOutcome outcome)
    {
        _givenUp[itemId] = outcome;
        TransferLog.GivenUp(context.Log, context.State.Key.CharacterId, itemId, outcome.ToString());
    }

    private void End(BotContext context)
    {
        var moved = _moved.Values.Sum();

        TransferLog.Over(context.Log, context.State.Key.CharacterId, moved, _moved.Count, _rounds, _givenUp.Count);

        _report = new ActivityReport
        {
            Doing = GameText.Of(TransferTexts.Text("DoingOver"), GameTextValue.Number(moved), GameTextValue.Number(_moved.Count), GameTextValue.Number(_rounds)),
            Counts = Counts(_moved),
            Warnings = Warnings(),
        };
    }

    private void Publish(CharacterState state, string doing)
    {
        var moved = _moved.Values.Sum();
        var total = moved + _carried.Values.Sum() + Stored(state).Sum(stack => (long)Math.Min(stack.Quantity, Allowance(state, stack.ItemId)));

        _report = new ActivityReport
        {
            Doing = GameText.Of(TransferTexts.Text(doing)),
            Progress = state.Storage is null && _rounds is 0 && moved is 0 ? null : new ActivityProgress(GameText.Of(TransferTexts.Text("Moved")), (int)Math.Min(moved, int.MaxValue), (int)Math.Min(Math.Max(total, moved), int.MaxValue)),
            Counts = Counts(_moved),
            Warnings = Warnings(),
        };
    }

    private ImmutableArray<GameText> Warnings()
    {
        return [.. _givenUp.Select(entry => GameText.Of(TransferTexts.Text($"Warning{entry.Value}"), GameTextValue.Item(entry.Key)))];
    }
}
