using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Data;

namespace D3B.Bot.Transfers;

/// <summary>Moves objects between the character's bank and its guild's chest, either way, in as many rounds as its pods need, then says what it moved (D560).</summary>
/// <remarks>
/// <para>
/// A round goes to the source, the nearest banker or guild's chest, opens it, takes out what the settings choose and the pods hold,
/// a stack at a time, and leaves; then goes to the destination, puts in what it took, and leaves (D551). The chest stands in banks'
/// insides (D153), so a round's journeys are short, often none. Only what the transfer took moves: what the character carried before
/// stays in its bags. Before its takes, a round notes how much of each stack in the bags is the character's own, so that a put never
/// takes from it, whether the inventory joined the stack taken to the character's or gave it a stack of its own, as it does an object
/// with its own effects. Those shares and what is carried outlive the run in the host's <see cref="TransferBags"/>, so that a run
/// stopped between a take and its put leaves the next one on the same connection to put it in first (D563); after a reconnection,
/// whose stacks have new ids, what the transfer carried stays in the bags, and it says so.
/// The guild's chest holds no kamas and opens on one tab, the one the transfer works on.
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

    // A source closed before its takes is opened again, as the recovery ladder tries an opening three times in all (D518).
    private const int ClosingsTried = 3;

    private readonly TransferSettings _settings;
    private readonly TransferFilter _filter;
    private readonly IReadOnlySet<long> _chests;
    private readonly BankingPreferences _banking;
    private readonly RecoveryLadder _openings = new();
    private readonly BusyWait _busy = new();
    private readonly Dictionary<int, long> _taken = [];
    private readonly SortedDictionary<int, long> _moved = [];
    private readonly SortedDictionary<int, StorageMoveOutcome> _givenUp = [];
    private readonly TransferBags _bags;
    private readonly string _source;
    private readonly string _destination;
    private TransferCarry _carry = new();
    private int _rounds;
    private int _closings;
    private bool _begun;
    private bool _emptied;
    private bool _disconnected;
    private bool _leftInBags;
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
    /// <param name="bags">What each character's transfer carries between its runs, the host's one; a run's own when none is given.</param>
    public TransferActivity(TransferSettings settings, GameTables tables, GraphicPlaces places, BankingPreferences? banking = null, TransferBags? bags = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(places);

        _settings = settings;
        _filter = new TransferFilter(settings, tables.Items);
        _chests = places.MapsDrawing(GuildChest.GfxId);
        _banking = banking ?? new BankingPreferences();
        _bags = bags ?? new TransferBags();
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

        // A new game connection gives the stacks new ids (client-game.md, "The game, banked"): which are the character's own is lost.
        if (reason is Interruption.Disconnected)
            _disconnected = true;

        return base.OnInterrupted(reason);
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _busy.WaitAsync(context, Failure, cancellationToken).ConfigureAwait(false))
            return true;

        if (!_begun)
            Begin(context);

        if (_disconnected || (!_settings.DryRun && _carry.ConnectionId != context.Actor.ConnectionId))
            Forget(context);

        if (!_emptied && _settings.EmptyBagsFirst && !_settings.DryRun)
            return await EmptyBagsAsync(context, cancellationToken).ConfigureAwait(false);

        _carry.Carrying.RemoveWhere(itemId => Carried(context.State, itemId) <= 0);

        return _carry.Carrying.Count > 0
            ? await PutAsync(context, cancellationToken).ConfigureAwait(false)
            : await TakeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TakeAsync(BotContext context, CancellationToken cancellationToken)
    {
        Publish(context.State, FromBank ? "DoingTakeBank" : "DoingTakeChest");

        // The bank the bags were just emptied in is the source: its takes follow in the same opening (D575).
        if (!(FromBank && _emptied && context.State.Storage is { IsGuildChest: false }))
            await OpenAsync(context, FromBank, cancellationToken).ConfigureAwait(false);

        await GiveBackAsync(context, cancellationToken).ConfigureAwait(false);
        Remember(context.State);

        if (_settings.DryRun)
            return await SayWhatWouldMoveAsync(context, cancellationToken).ConfigureAwait(false);

        var took = false;
        var tooHeavy = false;
        var refusedForPods = false;
        var moves = 0;
        var character = context.State.Key.CharacterId;

        // Measured on the owner's run of 2026-11-25 (D563): the client leaves the storage by itself just after the bot's
        // leaving, and closed a chest the bot opened again 31 ms later. A storage closed is no storage holding nothing.
        var closed = context.State.Storage is null;

        foreach (var stack in Stored(context.State))
        {
            var state = context.State;

            closed |= state.Storage is null;

            if (closed || refusedForPods)
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

            // Counted as carried before the request: a step cut before the answer still finds in the bags what the server moved.
            _carry.Carrying.Add(stack.ItemId);

            switch (await context.TakeFromStorageAsync(stack.Uid, quantityTaken, cancellationToken).ConfigureAwait(false))
            {
                case StorageMoveOutcome.Moved:
                    took = true;
                    _taken[stack.ItemId] = _taken.GetValueOrDefault(stack.ItemId) + quantityTaken;
                    TransferLog.Took(context.Log, character, stack.ItemId, quantityTaken, _source);
                    break;
                case StorageMoveOutcome.TooHeavy:
                    // The weights said it fit: the state's weight is behind the server's, and the next takes would be refused too.
                    tooHeavy = true;
                    refusedForPods = true;
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
        {
            _closings = 0;
            return true;
        }

        if (closed)
        {
            if (++_closings < ClosingsTried)
                return true;

            throw Failure($"The {_source} closed each time it opened.", FromBank ? "FailBankNotOpened" : "FailChestNotOpened");
        }

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
        var moves = !FromBank && await DepositWholeAsync(context, cancellationToken).ConfigureAwait(false) ? 1 : 0;

        foreach (var itemId in _carry.Carrying.ToList())
        {
            var refusal = StorageMoveOutcome.Moved;

            foreach (var (stack, quantity) in Takens(context.State, itemId))
            {
                if (moves++ > 0)
                    await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

                refusal = await context.PutInStorageAsync(stack.Uid, quantity, cancellationToken).ConfigureAwait(false);

                if (refusal is not StorageMoveOutcome.Moved)
                    break;

                _moved[itemId] = _moved.GetValueOrDefault(itemId) + quantity;
                TransferLog.Put(context.Log, character, itemId, quantity, _destination);
            }

            // The storage closed under it, such as by the player: what is left goes in at the next round.
            if (refusal is StorageMoveOutcome.Busy)
                break;

            _carry.Carrying.Remove(itemId);

            if (refusal is StorageMoveOutcome.Moved || Carried(context.State, itemId) <= 0)
                continue;

            if (refusal is StorageMoveOutcome.NoRight)
            {
                await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
                throw Failure("The guild's rank has no right to put objects in the chest.", "FailNoPutRight");
            }

            GiveUp(context, itemId, refusal);

            // Measured on the owner's run of 2026-11-26 (D572): a bound object went out of the bank and back once a run, four runs in a row.
            if (refusal is StorageMoveOutcome.NotExchangeable or StorageMoveOutcome.TypeRefused)
                RefusedForGood()[itemId] = refusal;

            switch (_settings.WhenRefused)
            {
                case TransferRefusal.GiveBack:
                    _carry.GivingBack.Add(itemId);
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

    // The owner's ask after a take left in the bags by a reconnection, which no run can tell from the character's own (D564): the
    // bank is the destination, so everything the bags hold goes where the transfer takes the chest's objects anyway. From the
    // bank, the owner's ask after its mules' harvest stayed in their bags (D575): what the bags held goes to the chest with the
    // bank's objects, what the transfer carried back where it came from, which moves nothing.
    private async Task<bool> EmptyBagsAsync(BotContext context, CancellationToken cancellationToken)
    {
        Publish(context.State, "DoingEmptyBags");
        await OpenAsync(context, true, cancellationToken).ConfigureAwait(false);

        var state = context.State;
        var carried = _carry.Carrying.ToDictionary(itemId => itemId, itemId => Carried(state, itemId));
        var uids = state.Inventory.Values.Where(stack => !stack.IsWorn).Select(stack => stack.Uid).ToList();
        var outcome = uids.Count > 0 ? await context.DepositAsync(uids, 0, cancellationToken).ConfigureAwait(false) : BankOutcome.Done;

        // The bags' stacks the bank took are no longer the character's own: one taken out again for the chest is the transfer's.
        foreach (var uid in _carry.Own.Keys.Where(uid => !context.State.Inventory.ContainsKey(uid)).ToList())
            _carry.Own.Remove(uid);

        // What the transfer carried and the bank took counts as moved; the stacks it left, such as a quest's objects, stay.
        foreach (var (itemId, quantity) in FromBank ? [] : carried)
        {
            var put = quantity - Carried(context.State, itemId);

            if (put <= 0)
                continue;

            _moved[itemId] = _moved.GetValueOrDefault(itemId) + put;
            TransferLog.Put(context.Log, state.Key.CharacterId, itemId, put, _destination);
        }

        // The bank is the source: its takes follow in this step, as a bank still open at the next would be the busy wait's (D575).
        if (FromBank && context.State.Storage is { IsGuildChest: false })
        {
            _closings = 0;
            _emptied = true;

            // Measured (D577): the server refuses a bank action asked less than about 0.55 s after the last, with text 268.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            return await TakeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        await LeaveAsync(context, cancellationToken).ConfigureAwait(false);

        // A bank closed under the deposit is opened again, as one closed under the takes is.
        if (outcome is BankOutcome.Busy && ++_closings < ClosingsTried)
            return true;

        _closings = 0;
        _emptied = true;

        return true;
    }

    // Measured on the owner's captures of 20 to 26 November (D577): the client puts a list of stacks in the bank by one request,
    // which the bank's deposits already send (D80). Only a stack the transfer carries whole goes in it: one the take joined to the
    // character's own holds objects to leave, which a stack at a time puts in by their number.
    private async Task<bool> DepositWholeAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;
        var whole = _carry.Carrying.SelectMany(itemId => Takens(state, itemId)).Where(taken => taken.Quantity == taken.Stack.Quantity).Select(taken => taken.Stack.Uid).ToList();

        if (whole.Count is 0)
            return false;

        var carried = _carry.Carrying.ToDictionary(itemId => itemId, itemId => Carried(state, itemId));

        await context.DepositAsync(whole, 0, cancellationToken).ConfigureAwait(false);

        // What the bank left, which it answers with nothing, goes in a stack at a time after, as before the list (D80).
        foreach (var (itemId, quantity) in carried)
        {
            var put = quantity - Carried(context.State, itemId);

            if (put <= 0)
                continue;

            _moved[itemId] = _moved.GetValueOrDefault(itemId) + put;
            TransferLog.Put(context.Log, state.Key.CharacterId, itemId, put, _destination);
        }

        return true;
    }

    private async Task GiveBackAsync(BotContext context, CancellationToken cancellationToken)
    {
        foreach (var itemId in _carry.GivingBack.ToList())
        {
            foreach (var (stack, quantity) in Takens(context.State, itemId))
            {
                // What the source refuses back stays in the bags: it took it out a moment ago, so this is not expected.
                if (await context.PutInStorageAsync(stack.Uid, quantity, cancellationToken).ConfigureAwait(false) is not StorageMoveOutcome.Moved)
                    break;

                await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
            }

            _carry.GivingBack.Remove(itemId);
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

    // Takes up what the character's last run left carried, so that a run stopped between a take and its put leaves this one
    // to put it in (D563). A dry run moves nothing, so it neither puts that in nor notes the bags for the next run.
    private void Begin(BotContext context)
    {
        _begun = true;

        if (_settings.DryRun)
            return;

        _carry = _bags.For(context.State.Key);

        // What the destination refuses whatever the run is left out from the start, and still said in the warnings.
        foreach (var (itemId, outcome) in RefusedForGood())
            _givenUp[itemId] = outcome;

        // Between the runs the player may have moved stacks by hand: only the stacks of the items carried keep their shares.
        var bags = context.State.Inventory.Values.Where(stack => !stack.IsWorn).ToDictionary(stack => stack.Uid);

        foreach (var uid in _carry.Own.Keys.Where(uid => !bags.ContainsKey(uid)).ToList())
            _carry.Own.Remove(uid);

        foreach (var stack in bags.Values.Where(stack => !_carry.Carrying.Contains(stack.ItemId) && !_carry.GivingBack.Contains(stack.ItemId)))
            _carry.Own[stack.Uid] = stack.Quantity;
    }

    private void Forget(BotContext context)
    {
        _disconnected = false;
        _carry.ConnectionId = context.Actor.ConnectionId;

        if (!_carry.IsCarrying)
        {
            _carry.Own.Clear();
            return;
        }

        foreach (var itemId in _carry.Carrying.Concat(_carry.GivingBack).Distinct())
            TransferLog.LeftInBags(context.Log, context.State.Key.CharacterId, itemId);

        _carry.Own.Clear();
        _carry.Carrying.Clear();
        _carry.GivingBack.Clear();
        _leftInBags = true;
    }

    private Dictionary<int, StorageMoveOutcome> RefusedForGood()
    {
        if (!_carry.Refused.TryGetValue(_settings.Direction, out var refused))
        {
            refused = [];
            _carry.Refused[_settings.Direction] = refused;
        }

        return refused;
    }

    // Each stack of the bags not noted yet is the character's own, whole: called before a round's takes, once what was refused went back.
    private void Remember(CharacterState state)
    {
        foreach (var stack in state.Inventory.Values.Where(stack => !stack.IsWorn && !_carry.Own.ContainsKey(stack.Uid)))
            _carry.Own[stack.Uid] = stack.Quantity;
    }

    private int Carried(CharacterState state, int itemId)
    {
        return Takens(state, itemId).Sum(taken => taken.Quantity);
    }

    // The transfer's share of each stack of an item in the bags: what the stack holds above the character's own.
    private List<(InventoryItem Stack, int Quantity)> Takens(CharacterState state, int itemId)
    {
        return [.. state.Inventory.Values
            .Where(stack => stack.ItemId == itemId && !stack.IsWorn)
            .Select(stack => (Stack: stack, Quantity: stack.Quantity - _carry.Own.GetValueOrDefault(stack.Uid)))
            .Where(taken => taken.Quantity > 0)];
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
        var total = moved + _carry.Carrying.Sum(itemId => (long)Carried(state, itemId)) + Stored(state).Sum(stack => (long)Math.Min(stack.Quantity, Allowance(state, stack.ItemId)));

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
        var givenUp = _givenUp.Select(entry => GameText.Of(TransferTexts.Text($"Warning{entry.Value}"), GameTextValue.Item(entry.Key)));

        return [.. givenUp, .. _leftInBags ? [GameText.Of(TransferTexts.Text("WarningLeftInBags"))] : Array.Empty<GameText>()];
    }
}
