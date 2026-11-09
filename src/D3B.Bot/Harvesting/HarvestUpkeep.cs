using D3B.Bot.Characters;
using D3B.Bot.Inventory;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Data;

namespace D3B.Bot.Harvesting;

/// <summary>What a harvesting character does for itself between two maps: it opens the bags it carries, eats to heal, and waits for its life to regenerate (D148, D153, 9g5).</summary>
internal sealed class HarvestUpkeep
{
    // Item type 100, "Sac de ressources": 73 items of the pack, opened by the same request as food (D148, D153).
    private const int BagType = 100;

    // Tenths of a second a life point takes, when the server did not say: as measured in every capture (D153).
    private const int RegainPoll = 5;

    private static readonly TimeSpan _regainStep = TimeSpan.FromSeconds(10);

    private readonly HarvestSettings _settings;
    private readonly GameTables _tables;
    private readonly Action<CharacterState, GameText> _publish;
    private readonly HashSet<int> _unusable = [];
    private bool _healing;

    /// <summary>Gets how many bags were opened.</summary>
    public int Bags { get; private set; }

    /// <summary>Gets how many food items were eaten.</summary>
    public int Eaten { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="HarvestUpkeep"/> class.</summary>
    /// <param name="settings">What the profile says of bags, food and when to heal.</param>
    /// <param name="tables">The installed build's tables: the items.</param>
    /// <param name="publish">What says on the activity's card what the character does.</param>
    public HarvestUpkeep(HarvestSettings settings, GameTables tables, Action<CharacterState, GameText> publish)
    {
        _settings = settings;
        _tables = tables;
        _publish = publish;
    }

    /// <summary>Does one thing of upkeep, if the character needs one: opens a bag, eats, or waits for its life.</summary>
    /// <param name="context">The bot's context.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>Whether the step was spent on it.</returns>
    public async Task<bool> UpkeepAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (_settings.OpenBags && StackOf(state, stack => _tables.Items.TryGetItem(stack.ItemId, out var item) && item.TypeId == BagType) is { } bag)
        {
            _publish(state, GameText.Of(HarvestTexts.Text("DoingOpenBag"), GameTextValue.Item(bag.ItemId)));

            if (await context.UseObjectAsync(bag.Uid, cancellationToken).ConfigureAwait(false) is ObjectUseOutcome.Used)
                Bags++;
            else
                _unusable.Add(bag.Uid);

            // Measured: used one after another, the objects went faster than the client showed them.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (!_settings.Heal || context.Life is not { } life || state.MaxLife is not { } most || most <= 0)
            return false;

        // Once under the share, the character eats until its life is whole (D148), or regenerates up to the share (9g5).
        if (life >= most || (!_healing && life * 100L >= (long)_settings.HealBelow * most))
        {
            _healing = false;

            return false;
        }

        _healing = true;

        if (Food.StackToEat(state, _settings.Food, _tables.Items, _unusable) is { } food)
        {
            _publish(state, GameText.Of(HarvestTexts.Text("DoingEat"), GameTextValue.Item(food.ItemId)));

            // In one request: measured, the server eats only as much of the stack as the life lacks (D156).
            var before = food.Quantity;

            switch (await context.UseObjectsAsync(food.Uid, food.Quantity, cancellationToken).ConfigureAwait(false))
            {
                case ObjectUseOutcome.Used:
                    Eaten += Math.Max(1, before - (context.State.Inventory.TryGetValue(food.Uid, out var left) ? left.Quantity : 0));
                    break;
                case ObjectUseOutcome.Refused:
                    // Measured: the server refuses food at full life, which it knows better than the reckoning.
                    _healing = false;
                    break;
                default:
                    _unusable.Add(food.Uid);
                    break;
            }

            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // Nothing left to eat: measured, the life regenerates by itself a point each interval the server gave (D153). The
        // owner's choice (9g5): nearly whole, the character waited its whole life back without looking at it again; it waits
        // for the share it heals under alone, as after a lost fight (D198), looking again at least each 10 seconds.
        var share = (((long)_settings.HealBelow * most) + 99) / 100;

        if (life >= share)
        {
            _healing = false;

            return false;
        }

        var point = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
        var regenerating = TimeSpan.FromTicks(point.Ticks * (share - life));

        _publish(state, GameText.Of(HarvestTexts.Text("DoingRegenerate"), GameTextValue.Number((long)Math.Ceiling(regenerating.TotalSeconds))));
        await context.WaitUntilAsync(_ => false, regenerating < _regainStep ? regenerating : _regainStep, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>Waits for the character's life to regenerate up to the share it heals under, once a fight was lost, unless it carries food that heals it at the next step.</summary>
    /// <param name="context">The bot's context.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes once the life is back.</returns>
    public async Task RegainAsync(BotContext context, CancellationToken cancellationToken)
    {
        // Food the profile carries heals at the next step (D148); else the life regenerates, a point each interval (D153).
        var state = context.State;

        if (_settings.Heal && Food.StackToEat(state, _settings.Food, _tables.Items, _unusable) is not null)
            return;

        for (var life = context.Life; life is { } now && state.MaxLife is { } most && now * 100L < (long)_settings.HealBelow * most; life = context.Life)
        {
            _publish(state, GameText.Of(HarvestTexts.Text("DoingRegain"), GameTextValue.Number(now), GameTextValue.Number(most)));

            var interval = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
            var missing = (((long)_settings.HealBelow * most) + 99) / 100 - now;

            await Task.Delay(TimeSpan.FromTicks(Math.Min(interval.Ticks * Math.Max(1, missing), _regainStep.Ticks)), context.Time, cancellationToken).ConfigureAwait(false);
            state = context.State;
        }
    }

    private InventoryItem? StackOf(CharacterState state, Func<InventoryItem, bool> which)
    {
        return state.Inventory.Values.FirstOrDefault(stack => !stack.IsWorn && !_unusable.Contains(stack.Uid) && which(stack));
    }
}
