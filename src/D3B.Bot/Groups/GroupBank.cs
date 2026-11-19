using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Protocol.Game;

namespace D3B.Bot.Groups;

/// <summary>The group's bank: a member full takes the whole group to the master's bank, each follower gives the master what it carries, and the master deposits it (D433, D442).</summary>
/// <remarks>
/// <para>
/// One per bot, which does nothing while nobody follows it. The master's run asks it before each step: a follower is full at
/// the share of pods the master's profile banks at, the pods its activity watches (D325), the master by its own activity's
/// reckoning. Then, instead of its activity's own unloading, the master goes to the nearest bank's banker, its gate gathering
/// the group map after map (D437), and takes each follower's objects in turn, in the chain's order.
/// </para>
/// <para>
/// Measured on 3.7.14.18 (D435, D442): the follower asks the master for an exchange; the master accepts and the exchange
/// opens for both; the follower moves its stacks in one by one, each change told to both; it says it is ready naming how
/// many changes the exchange had, then the master the same; the exchange concludes as both are, the objects in the master's
/// inventory. A follower gives every stack it carries and does not wear, never its kamas, at most what the master's pods
/// still hold (<see cref="GroupGifts"/>): the master deposits first when they hold none of the follower's next stack. Then
/// the master deposits what it was given and what its own profile banks (<see cref="BotActivity.Deposits"/>), in one opening
/// of the bank, the owner's choice.
/// </para>
/// </remarks>
internal sealed class GroupBank : IGroupBank
{
    // Exchanges with one follower at most: a follower carrying more than the master's pods is emptied in several, the
    // master depositing between them.
    private const int MostRounds = 10;

    // Journeys to the banker while the character is busy, as an activity's own banking tries (D405).
    private const int MostJourneys = 3;

    // How long the master waits for its follower's asking, then for each of its moves and its readiness: each move is
    // answered within 30 ms (D442), the client's display time between them.
    private static readonly TimeSpan _exchangeWait = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan _busyWait = TimeSpan.FromSeconds(10);

    private readonly BotActor _bot;
    private readonly GroupDirectory _groups;
    private readonly GroupGate _gate;
    private readonly GameTables _tables;

    // The stacks the bank left, such as a quest's objects, left in the master's bags for the rest of one unloading (D457):
    // a refusal of another cause, such as a bank full that day, is tried again at the next. Only the master's run, one
    // step at a time, reads and writes them.
    private readonly HashSet<int> _refused = [];

    // Whether the master's profile banks all but what is worn, its food too (D459), as the unloading begins.
    private bool _food;

    public GroupBank(BotActor bot, GroupDirectory groups, GroupGate gate, GameTables tables)
    {
        _bot = bot;
        _groups = groups;
        _gate = gate;
        _tables = tables;
    }

    /// <summary>Gets the share of pods, in percent, a master's profile banks at: the pods its activity watches (D325).</summary>
    /// <param name="activity">The master's activity.</param>
    /// <returns>The share; none when the activity banks not.</returns>
    public static int? ShareOf(BotActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        foreach (var watch in activity.Watches)
        {
            if (watch.Gauge is WatchedGauge.Pods)
                return watch.Threshold;
        }

        return null;
    }

    /// <inheritdoc/>
    public bool Takes(BotActivity activity)
    {
        return _groups.FollowersOf(_bot.Key).Count > 0 && ShareOf(activity) is not null;
    }

    /// <inheritdoc/>
    public bool IsFull(BotActivity activity)
    {
        if (ShareOf(activity) is not { } share)
            return false;

        return _groups.FollowersOf(_bot.Key).Any(follower => follower.State is { MaxWeight: > 0 } state && state.Weight * 100L >= (long)share * state.MaxWeight);
    }

    /// <inheritdoc/>
    public async Task<bool> UnloadAsync(BotContext context, BotActivity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(activity);

        GroupLog.Banking(context.Log, _bot.Key.CharacterId);
        _refused.Clear();
        _food = activity.DepositsAll;

        if (!await ToBankerAsync(context, activity, cancellationToken).ConfigureAwait(false))
            return false;

        // The owner's report from game (D452): full, the master took its followers' objects all the same, and the server
        // never told it their readiness, nor concluded. It empties itself first; a stack the bank refuses stops nothing (D457).
        if (await DepositAsync(context, cancellationToken).ConfigureAwait(false) is not BankOutcome.Done)
            return false;

        foreach (var follower in GroupChain.Order(context.State, _groups.FollowersOf(_bot.Key)))
        {
            var deposited = false;

            // Measured on the owner's test of 2026-11-19 (D459): a stack the exchange told no change for was offered again in
            // two more exchanges, each concluding with nothing 8.1 s later. The stacks an exchange leaves are left.
            var left = new HashSet<int>();

            for (var round = 0; round < MostRounds; round++)
            {
                // The whole group with the master, as before any action (D437).
                await _gate.GatherAsync(context, cancellationToken).ConfigureAwait(false);

                if (!follower.IsConnected || follower.IsPaused || !GroupGifts.Carries(follower.State, _tables.Items, _food, left))
                    break;

                var offered = GroupGifts.Of(follower.State, context.State, _tables.Items, _food, left);

                // Its pods holding none of the follower's next stack, or an exchange that did not conclude, such as more
                // than its pods hold by the server's reckoning, the master deposits, once, then takes the rest.
                if (offered.Count > 0)
                {
                    var carried = offered.ToDictionary(gift => gift.Uid, gift => follower.State.Inventory.TryGetValue(gift.Uid, out var stack) ? stack.Quantity : 0);

                    if (await ExchangeAsync(context, follower, left, cancellationToken).ConfigureAwait(false) is not null)
                    {
                        // Concluded: a stack offered whose quantity did not go down, the exchange takes not; one given in
                        // part, the master's pods full, is taken again once it deposited (D452).
                        await SettledAsync(follower, cancellationToken).ConfigureAwait(false);
                        left.UnionWith(carried.Where(gift => follower.State.Inventory.TryGetValue(gift.Key, out var stack) && stack.Quantity >= gift.Value).Select(gift => gift.Key));
                        deposited = false;

                        continue;
                    }
                }

                if (deposited || !Carries(context.State) || await DepositAsync(context, cancellationToken).ConfigureAwait(false) is not BankOutcome.Done)
                    break;

                deposited = true;
            }
        }

        return await DepositAsync(context, cancellationToken).ConfigureAwait(false) is BankOutcome.Done;
    }

    // The owner's choice (D452): the master deposits all it does not wear, but the food it eats, whatever its profile banks,
    // so that its pods go down whatever it carries, objects put in its bags by the player's hand too.
    private bool Deposited(InventoryItem stack)
    {
        return !stack.IsWorn && stack.Quantity > 0 && !_refused.Contains(stack.Uid) && (_food || !(_tables.Items.TryGetItem(stack.ItemId, out var item) && item.Heals));
    }

    private bool Carries(CharacterState state)
    {
        return state.Inventory.Values.Any(Deposited);
    }

    // Until the follower's own state shows the exchange over, its stacks given gone from it: its loop reads the conclusion
    // apart from the master's.
    private Task SettledAsync(BotActor follower, CancellationToken cancellationToken)
    {
        return GroupWaits.AnyAsync([(follower, bot => bot.State.Exchange is null)], Task.Delay(Timeout.Infinite, cancellationToken), _bot.Time, _bot.Delays.Answer, cancellationToken);
    }

    // The master's deposit, in one opening of the bank; none while it carries nothing the bank takes. The owner's report from
    // game (D457): the bank took none of the one stack the master carried, and the master stopped there, its full follower
    // never emptied. The stacks the bank leaves are left from then on, and the unloading goes on.
    private async Task<BankOutcome> DepositAsync(BotContext context, CancellationToken cancellationToken)
    {
        var uids = context.State.Inventory.Values.Where(Deposited).Select(stack => stack.Uid).ToList();

        if (uids.Count is 0)
            return BankOutcome.Done;

        var outcome = await context.OpenBankAsync(cancellationToken).ConfigureAwait(false);

        if (outcome is not BankOutcome.Done)
            return outcome;

        var deposit = await context.DepositAsync(uids, 0, cancellationToken).ConfigureAwait(false);
        var left = await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);

        if (deposit is BankOutcome.Done or BankOutcome.Refused)
        {
            _refused.UnionWith(uids.Where(context.State.Inventory.ContainsKey));

            return left;
        }

        return deposit;
    }

    private static async Task LeaveAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (context.State.Exchange is { IsOpen: true })
            await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);
    }

    // To the nearest banker, as the activity's own unloading goes there: by a zaap only as the general banking settings
    // allow (D405, D436), again while the character is busy.
    private static async Task<bool> ToBankerAsync(BotContext context, BotActivity activity, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MostJourneys; attempt++)
        {
            switch (await context.GoToBankerAsync(activity.BanksByTeleport, cancellationToken).ConfigureAwait(false))
            {
                case BankOutcome.Done:
                    return true;
                case BankOutcome.Busy:
                    await context.WaitUntilAsync(state => !state.IsBusy, _busyWait, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    // One exchange with a follower: it asks, the master accepts, it moves its stacks in and says it is ready, then the master.
    // Gives how many of the master's stacks grew, or nothing when the exchange did not conclude.
    private async Task<int?> ExchangeAsync(BotContext context, BotActor follower, IReadOnlySet<int> left, CancellationToken cancellationToken)
    {
        var id = follower.Key.CharacterId;
        var before = context.State.Inventory;

        using var collecting = _groups.Collect(_bot.Key, follower.Key, _food, left);

        if (!await context.WaitUntilAsync(state => state.Exchange is { IsOpen: false } asked && asked.SourceId == id, _exchangeWait, cancellationToken).ConfigureAwait(false))
            return null;

        // The client shows the asking first, as it does a map before a walk (D77).
        await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
        await context.SendAsync(new ExchangeAcceptRequest(), cancellationToken).ConfigureAwait(false);

        if (!await context.WaitUntilAsync(state => state.Exchange is { IsOpen: true }, context.Delays.Answer, cancellationToken).ConfigureAwait(false)
            || await ReadiedAsync(context, cancellationToken).ConfigureAwait(false) is not { } open)
        {
            await LeaveAsync(context, cancellationToken).ConfigureAwait(false);

            return null;
        }

        await context.SendAsync(new ExchangeReadyRequest { Step = open.Changes, Ready = true }, cancellationToken).ConfigureAwait(false);

        if (!await context.WaitUntilAsync(state => state.Exchange is null, context.Delays.Answer, cancellationToken).ConfigureAwait(false))
        {
            await LeaveAsync(context, cancellationToken).ConfigureAwait(false);

            return null;
        }

        var grew = 0;

        foreach (var stack in context.State.Inventory.Values)
        {
            var was = before.TryGetValue(stack.Uid, out var old) ? old.Quantity : 0;

            if (stack.Quantity <= was)
                continue;

            grew++;
        }

        GroupLog.Given(context.Log, _bot.Key.CharacterId, id, grew);

        return grew;
    }

    // Until the follower says it is ready, as long as each of its moves comes within the wait: a follower carrying many
    // stacks moves them one by one, the client's display time between them.
    private static async Task<PlayerExchange?> ReadiedAsync(BotContext context, CancellationToken cancellationToken)
    {
        while (context.State.Exchange is { IsOpen: true, IsOtherReady: false } waiting)
        {
            var changes = waiting.Changes;

            if (!await context.WaitUntilAsync(state => state.Exchange is not { IsOpen: true, IsOtherReady: false } now || now.Changes != changes, _exchangeWait, cancellationToken).ConfigureAwait(false))
                return null;
        }

        return context.State.Exchange is { IsOpen: true, IsOtherReady: true } open ? open : null;
    }
}
