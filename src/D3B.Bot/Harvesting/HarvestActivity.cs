using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Interactives;
using D3B.Bot.Inventory;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Harvesting;

/// <summary>Harvesting: resources harvested map after map through a zone, banked once the character is full (D127 to D132).</summary>
/// <remarks>
/// <para>
/// Each step is chosen from the state (D87). On a map of the zone, the step harvests the element the server offers a
/// wanted resource's skill on, at the character's level, the resource asked for first, then the nearest; an element taken
/// by another, refused or out of reach is left for the rest of the visit. A map with nothing left is gone through, and the
/// step travels to the nearest map of the tour still to go through (<see cref="HarvestTour"/>), the probable ones first,
/// stopping on the first such map of the way. The journey also stops on a map of the zone it crosses, not yet gone through in the tour,
/// where the server offers a wanted element, even when the tour holds the map back as growing (D392). Off the zone, after banking or a
/// fight, it travels back the same way.
/// </para>
/// <para>
/// When the activity chooses the place, it goes through several sub-areas in turn (<see cref="PlaceRotation"/>, D391): a tour done, it
/// goes on to the sub-area of the rotation left longest ago that has maps to go through, each keeping its own tour, and waits only when
/// every one still grows back or was swept for nothing. With <see cref="HarvestOrder.Balanced"/> the zones are shared between the
/// professions and the one harvested least lately comes first on a map (<see cref="HarvestBalance"/>).
/// </para>
/// <para>
/// Full at the settings' share of its pods, the character banks at the nearest bank (D79, D80), what harvesting brings or
/// all it does not wear, no kamas, two more times after a failure that may pass; then the next step goes on from where it
/// stands. A fight, a new connection of the client and the player's pause resume the activity, which waits as long as it
/// must (D87, D88, D89). Three failed journeys in a row end it.
/// </para>
/// </remarks>
public sealed class HarvestActivity : BotActivity
{
    private const int MostLostInARow = 3;

    // Tenths of a second a life point takes, when the server did not say: as measured in every capture (D153).
    private const int RegainPoll = 5;

    private static readonly TimeSpan _regainStep = TimeSpan.FromSeconds(10);

    private const int MostFailedJourneys = 3;
    private const int MostBankings = 3;

    // Item type 100, "Sac de ressources": 73 items of the pack, opened by the same request as food (D148, D153).
    private const int BagType = 100;

    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _regrowthDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _objectsDelay = TimeSpan.FromSeconds(1);

    // Measured: a protector attacks 2 ms after the harvest's end; a second leaves room for the objects' events between.
    private static readonly TimeSpan _protectorDelay = TimeSpan.FromSeconds(1);

    private readonly HarvestSettings _settings;
    private readonly WorldAtlas _atlas;
    private readonly WorldGraph _graph;
    private readonly GameTables _tables;
    private readonly HarvestSightings _sightings;
    private readonly GameMaps _maps;
    private readonly GraphicPlaces _places;
    private readonly GraphicSkills _graphics;
    private readonly Regrowths _regrowths;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private readonly HarvestBalance _balance;
    private readonly Dictionary<int, long> _harvested = [];
    private readonly HashSet<int> _skipped = [];
    private readonly Dictionary<FightKind, int> _fights = [];
    private readonly HashSet<int> _unusable = [];
    private List<HarvestRotationZone> _rotation = [];
    private HarvestRotationZone? _current;
    private FrozenSet<int>? _deposited;
    private long? _skippedOn;
    private int _failedJourneys;
    private int _banks;
    private ActivityReport? _report;
    private ActivityStatistics? _statistics;
    private DateTimeOffset? _startedAt;
    private ImmutableSortedDictionary<int, CharacterJob>? _startJobs;
    private BotContext? _context;
    private int? _lastTarget;
    private bool _healing;
    private int _bags;
    private int _eaten;
    private string? _chestWarning;
    private FightKind? _fight;
    private int? _fightItem;
    private int _lostInARow;
    private bool _regaining;

    /// <summary>Gets the item types offered as food: breads, measured to heal, and drinks, fish, meat, sweets and potions (D148, D153).</summary>
    public static IReadOnlyList<int> FoodTypes { get; } = [12, 33, 37, 42, 49, 69, 79, 339];

    private HarvestZone? Zone => _current?.Zone;

    /// <inheritdoc/>
    public override string Name => "Harvesting";

    /// <inheritdoc/>
    public override ActivityReport? Report => Volatile.Read(ref _report);

    /// <inheritdoc/>
    public override ActivityStatistics? Statistics => Volatile.Read(ref _statistics);

    /// <inheritdoc/>
    public override ImmutableArray<ActivityWatch> Watches => _settings.Heal
        ? [new ActivityWatch(WatchedGauge.Pods, _settings.FullAt, WatchAction.Bank), new ActivityWatch(WatchedGauge.Life, _settings.HealBelow, WatchAction.Heal)]
        : [new ActivityWatch(WatchedGauge.Pods, _settings.FullAt, WatchAction.Bank)];

    /// <inheritdoc/>
    public override int TeleportChanges => _settings.ZaapSaving;

    /// <inheritdoc/>
    /// <remarks>The AI plays a protector's fight and an aggression instead of the player, as the profile says, then the settings answer it (D144, D198, D235).</remarks>
    public override bool FightsItsFights => _settings.AiFights;

    /// <summary>Initializes a new instance of the <see cref="HarvestActivity"/> class.</summary>
    /// <param name="settings">What to harvest, where, and when to bank.</param>
    /// <param name="world">The installed build's world: the zone's maps.</param>
    /// <param name="tables">The installed build's tables: the professions' skills.</param>
    /// <param name="maps">The installed build's maps: the graphics of the zone's elements.</param>
    /// <param name="sightings">What the host's bots saw of the maps.</param>
    /// <param name="graphics">What the host learned of the maps' graphics.</param>
    /// <param name="places">Where each graphic is drawn, which places the resources of <c>Base</c> (D150).</param>
    /// <param name="regrowths">What the host learned of the resources' growing back (D145).</param>
    /// <param name="time">The bots' clock.</param>
    /// <param name="random">The draw of the sub-areas the activity goes through in turn when it chooses the place (D391); the shared one when none is given.</param>
    public HarvestActivity(HarvestSettings settings, GameWorld world, GameTables tables, GameMaps maps, HarvestSightings sightings, GraphicSkills graphics, GraphicPlaces places, Regrowths regrowths, TimeProvider time, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(sightings);
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(regrowths);
        ArgumentNullException.ThrowIfNull(time);

        _settings = settings;
        _atlas = world.Atlas;
        _graph = world.Graph;
        _tables = tables;
        _maps = maps;
        _sightings = sightings;
        _places = places;
        _graphics = graphics;
        _regrowths = regrowths;
        _time = time;
        _random = random ?? Random.Shared;
        _balance = new HarvestBalance(settings.Jobs);
        _report = new ActivityReport { Doing = GameText.Of(HarvestTexts.Text("DoingStart")) };
    }

    private static int LevelOf(CharacterState state, int jobId)
    {
        return MapHarvest.LevelOf(state, jobId);
    }

    private static int Carried(CharacterState state, int itemId)
    {
        return MapHarvest.Carried(state, itemId);
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(HarvestTexts.Text(key)));
    }

    private static bool HasWanted(HarvestZone zone, CharacterState state)
    {
        return state.Elements.Values.Any(element => element.EnabledSkills.Any(skill => zone.Targets.TryGetValue(skill.SkillId, out var target) && LevelOf(state, target.JobId) >= target.Level));
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _startedAt ??= context.Time.GetUtcNow();
        _startJobs ??= context.State.Jobs;

        var state = context.State;

        if (state.IsBusy || state.MapId is not { } mapId)
        {
            await context.WaitUntilAsync(waited => !waited.IsBusy && waited.MapId is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

            return true;
        }

        var current = CurrentOf(context);
        var zone = current.Zone;

        if (await UpkeepAsync(context, cancellationToken).ConfigureAwait(false))
            return true;

        if (zone.Maps.ContainsKey(mapId))
        {
            See(state, mapId, zone, context.Time.GetUtcNow());

            if (await HarvestAsync(context, zone, cancellationToken).ConfigureAwait(false))
                return true;

            current.Tour.Visit(mapId);
        }

        await GoOnAsync(context, current, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public override bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.MaxWeight > 0 && state.Weight * 100L >= (long)_settings.FullAt * state.MaxWeight;
    }

    /// <inheritdoc/>
    public override async Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deposited = DepositedItems();

        Publish(context.State, GameText.Of(HarvestTexts.Text("DoingBank")));

        if (_settings.DepositTo is HarvestDepositPlace.GuildChest && await GuildChestAsync(context, deposited, cancellationToken).ConfigureAwait(false))
            return true;

        for (var attempt = 0; attempt < MostBankings; attempt++)
        {
            var outcome = await context.BankAsync(stack => _settings.Deposit is HarvestDeposit.AllButWorn || deposited.Contains(stack.ItemId), 0, cancellationToken).ConfigureAwait(false);

            switch (outcome)
            {
                case BankOutcome.Done:
                    _banks++;

                    // Measured: what the settings leave in the inventory may weigh more than the share the character banks at;
                    // the activity says so, rather than go back to the bank at every step.
                    if (IsFull(context.State))
                        throw StillFull(context.State);

                    Publish(context.State, GameText.Of(HarvestTexts.Text("DoingReturn")));

                    return true;
                case BankOutcome.NoBank:
                    return false;
                case BankOutcome.Busy:
                    await context.WaitUntilAsync(state => !state.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        _fight = null;

        if (reason is Interruption.Fight)
        {
            // Measured: a protector attacks as the character's own harvest ends, the objects' events between (D144); the fight
            // may be answered once over, when it began between two steps (D266).
            var kind = _context is { UseEndedAt: { } ended, FightBeganAt: { } began } && began >= ended && began - ended <= _protectorDelay ? FightKind.Protector : FightKind.Aggression;

            _fight = kind;
            _fightItem = kind is FightKind.Protector ? _lastTarget : null;
            _fights[kind] = _fights.GetValueOrDefault(kind) + 1;
        }

        var key = reason switch
        {
            Interruption.Fight => _fight is FightKind.Protector ? "DoingFightProtector" : "DoingFightAggression",
            Interruption.Player => "DoingPlayer",
            Interruption.Disconnected => "DoingDisconnected",
            _ => null,
        };

        if (key is not null && Report is { } report)
        {
            Tally();

            var counts = Zone is { } zone ? CountsOf(zone) : report.Counts;

            Volatile.Write(ref _report, report with { Doing = GameText.Of(HarvestTexts.Text(key)), Counts = counts });
        }

        if (reason is not Interruption.Fight)
            return InterruptionResponse.Resume;

        return _settings.AfterFight switch
        {
            HarvestAfterFight.Pause => InterruptionResponse.Pause,
            HarvestAfterFight.Stop => InterruptionResponse.Stop,
            _ => InterruptionResponse.Resume,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A fight lost, the character comes back to its save point with little life (D178): the activity answers it as its
    /// settings say, resumed once it regained its share of life, the owner's choice; three lost in a row stop it, so that it
    /// does not lose again and again (D198).
    /// </remarks>
    public override InterruptionResponse OnFightOver(FightEnd? ending, InterruptionResponse response)
    {
        if (ending is not { Outcome: FightOutcome.Lost })
        {
            _lostInARow = 0;

            return response;
        }

        _lostInARow++;
        _regaining = true;

        if (_lostInARow >= MostLostInARow)
            return InterruptionResponse.Stop;

        return _settings.AfterLostFight switch
        {
            HarvestAfterFight.Pause => InterruptionResponse.Pause,
            HarvestAfterFight.Stop => InterruptionResponse.Stop,
            _ => InterruptionResponse.Resume,
        };
    }

    /// <inheritdoc/>
    public override async Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);

        if (_regaining)
            await RegainAsync(context, cancellationToken).ConfigureAwait(false);

        _regaining = false;
    }

    /// <inheritdoc/>
    public override GameText? Describe(Interruption reason)
    {
        return (reason, _fight, _fightItem) switch
        {
            (Interruption.Fight, FightKind.Protector, { } item) => GameText.Of(HarvestTexts.Text("NoteProtectorOf"), GameTextValue.Item(item)),
            (Interruption.Fight, FightKind.Protector, null) => GameText.Of(HarvestTexts.Text("NoteProtector")),
            (Interruption.Fight, FightKind.Aggression, _) => GameText.Of(HarvestTexts.Text("NoteAggression")),
            _ => null,
        };
    }

    private async Task RegainAsync(BotContext context, CancellationToken cancellationToken)
    {
        // Food the profile carries heals at the next step (D148); else the life regenerates, a point each interval (D153).
        var state = context.State;

        if (_settings.Heal && Food.StackToEat(state, _settings.Food, _tables.Items, _unusable) is not null)
            return;

        for (var life = context.Life; life is { } now && state.MaxLife is { } most && now * 100L < (long)_settings.HealBelow * most; life = context.Life)
        {
            Publish(state, GameText.Of(HarvestTexts.Text("DoingRegain"), GameTextValue.Number(now), GameTextValue.Number(most)));

            var interval = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
            var missing = (((long)_settings.HealBelow * most) + 99) / 100 - now;

            await Task.Delay(TimeSpan.FromTicks(Math.Min(interval.Ticks * Math.Max(1, missing), _regainStep.Ticks)), context.Time, cancellationToken).ConfigureAwait(false);
            state = context.State;
        }
    }

    private ActivityException StillFull(CharacterState state)
    {
        var key = _settings.Deposit is HarvestDeposit.Harvest ? "FailStillFullHarvest" : "FailStillFullAll";
        var reason = GameText.Of(HarvestTexts.Text(key), GameTextValue.Number(state.Weight), GameTextValue.Number(state.MaxWeight), GameTextValue.Number(_settings.FullAt));

        return new ActivityException($"The character is still full once banked, {state.Weight} of {state.MaxWeight} pods.", reason);
    }

    private async Task<bool> UpkeepAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (_settings.OpenBags && StackOf(state, stack => _tables.Items.TryGetItem(stack.ItemId, out var item) && item.TypeId == BagType) is { } bag)
        {
            Publish(state, GameText.Of(HarvestTexts.Text("DoingOpenBag"), GameTextValue.Item(bag.ItemId)));

            if (await context.UseObjectAsync(bag.Uid, cancellationToken).ConfigureAwait(false) is ObjectUseOutcome.Used)
                _bags++;
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
            Publish(state, GameText.Of(HarvestTexts.Text("DoingEat"), GameTextValue.Item(food.ItemId)));

            // In one request: measured, the server eats only as much of the stack as the life lacks (D156).
            var before = food.Quantity;

            switch (await context.UseObjectsAsync(food.Uid, food.Quantity, cancellationToken).ConfigureAwait(false))
            {
                case ObjectUseOutcome.Used:
                    _eaten += Math.Max(1, before - (context.State.Inventory.TryGetValue(food.Uid, out var left) ? left.Quantity : 0));
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

        Publish(state, GameText.Of(HarvestTexts.Text("DoingRegenerate"), GameTextValue.Number((long)Math.Ceiling(regenerating.TotalSeconds))));
        await context.WaitUntilAsync(_ => false, regenerating < _regainStep ? regenerating : _regainStep, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private InventoryItem? StackOf(CharacterState state, Func<InventoryItem, bool> which)
    {
        return state.Inventory.Values.FirstOrDefault(stack => !stack.IsWorn && !_unusable.Contains(stack.Uid) && which(stack));
    }

    private async Task<bool> GuildChestAsync(BotContext context, FrozenSet<int> deposited, CancellationToken cancellationToken)
    {
        var chests = _places.MapsDrawing(GuildChest.GfxId);
        var outcome = await context.GuildChestAsync(chests, stack => _settings.Deposit is HarvestDeposit.AllButWorn || deposited.Contains(stack.ItemId), cancellationToken).ConfigureAwait(false);

        if (outcome is BankOutcome.Done)
        {
            _banks++;
            _chestWarning = null;

            // Measured: what the harvest does not bring may weigh above the share; the character's own chest takes what is left.
            if (!IsFull(context.State))
            {
                Publish(context.State, GameText.Of(HarvestTexts.Text("DoingReturn")));

                return true;
            }
        }
        else
        {
            // What the chest refused, or all when none was reached, goes to the character's own chest, and the card says so (D147).
            _chestWarning = outcome is BankOutcome.Refused ? "WarningChestRefused" : "WarningChestNone";
        }

        return false;
    }

    private HarvestRotationZone CurrentOf(BotContext context)
    {
        if (_current is { } current)
            return current;

        if (_settings.Place is HarvestPlace.Automatic)
        {
            Draw(context);

            if (_current is null)
                throw Failure("No sub-area holds a resource the character can harvest.", "FailNoPlace");

            return _current;
        }

        var zone = HarvestZone.Of(_settings, _atlas, _graph, _tables.Jobs, _maps, HarvestZone.PlacedFor(_settings, _tables.Jobs, _places, _graphics));

        if (zone.Maps.Count is 0 || zone.Targets.Count is 0)
            throw Failure("The zone holds no map to harvest.", "FailNoZone");

        _rotation = [new HarvestRotationZone(null, zone, NewTour())];

        return _current = _rotation[0];
    }

    private HarvestTour NewTour()
    {
        return new HarvestTour(_sightings, _graphics, _regrowths, _time);
    }

    // Draws the sub-areas the activity goes through in turn, the ones it already went through keeping their tour (D391).
    private void Draw(BotContext context)
    {
        var drawn = new List<HarvestRotationZone>();

        foreach (var subArea in PlacesOf(context))
        {
            var member = _rotation.Find(zone => zone.SubAreaId == subArea);

            if (member is null)
            {
                var zone = HarvestZone.Of(_settings, _atlas, _graph, _tables.Jobs, _maps, chosen: [subArea]);

                if (zone.Maps.Count is 0 || zone.Targets.Count is 0)
                    continue;

                member = new HarvestRotationZone(subArea, zone, NewTour());
            }

            member.Done = false;
            drawn.Add(member);
        }

        // Nothing drawn: the zones the activity has stay, as a level gained moves nothing for want of a sub-area (D166).
        if (drawn.Count is 0)
            return;

        _rotation = drawn;

        if (_current is null || !drawn.Contains(_current))
            _current = drawn[0];
    }

    // The sub-areas richest in what the character can harvest now, the rotation drawn from the best ten it can travel to (D166, D391).
    private IReadOnlyList<int> PlacesOf(BotContext context)
    {
        var state = context.State;
        var levels = state.Jobs.Values.ToDictionary(job => job.JobId, job => job.Level);
        var ranking = AutomaticPlace.Reckon(HarvestZone.TargetsOf(_settings, _tables.Jobs).Values, state.Level ?? 0, levels, _settings.SafeLevel, _settings.IsolatedMaps, _atlas, _graph, _places, _graphics);

        if (ranking.Candidates.Count is 0)
            return [];

        // The zone of each profession's best resource stays in the rotation, the professions in the order they are to be harvested.
        var order = _settings.Mode is HarvestMode.ByProfession && _settings.Order is HarvestOrder.Balanced
            ? _balance.Order()
            : [.. ranking.Tops.Keys.OrderByDescending(job => levels.GetValueOrDefault(job)).ThenBy(job => job)];
        var given = order.Where(ranking.Tops.ContainsKey).Select(job => ranking.Tops[job]);
        var drawn = PlaceRotation.Draw(ranking.Candidates.Select(candidate => new RotationCandidate(candidate.SubAreaId, candidate.Score)), given, _settings.Rotation, SubAreaReach.Of(context, _atlas), _random);

        // None reachable: the richest, which the journey will say it cannot reach.
        return drawn.Count > 0 ? drawn : [ranking.Candidates[0].SubAreaId];
    }

    private FrozenSet<int> DepositedItems()
    {
        return _deposited ??= _tables.Jobs.Skills.ToArray()
            .Where(skill => skill.GatheredItemId != Skill.NoItem)
            .Select(skill => skill.GatheredItemId)
            .Concat(_atlas.Harvestables.ToArray())
            .ToFrozenSet();
    }

    private void See(CharacterState state, long mapId, HarvestZone zone, DateTimeOffset now)
    {
        var elements = new List<SightedElement>();

        foreach (var element in state.Elements.Values)
        {
            foreach (var skill in element.EnabledSkills)
            {
                if (zone.Harvests.TryGetValue(skill.SkillId, out var item))
                    elements.Add(new SightedElement(element.ElementId, skill.SkillId, item, true));
            }

            foreach (var skill in element.DisabledSkills)
            {
                if (zone.Harvests.TryGetValue(skill.SkillId, out var item))
                    elements.Add(new SightedElement(element.ElementId, skill.SkillId, item, false));
            }
        }

        _sightings.SeenElements(mapId, elements, now);
    }

    private async Task<bool> HarvestAsync(BotContext context, HarvestZone zone, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (_skippedOn != state.MapId)
        {
            _skipped.Clear();
            _skippedOn = state.MapId;
        }

        var candidates = Candidates(context, zone, state);

        if (candidates.Count is 0)
            return false;

        var (elementId, target) = candidates[0];
        var doing = candidates.Count > 1
            ? GameText.Counted(HarvestTexts.Text("DoingHarvestOne"), HarvestTexts.Text("DoingHarvestMany"), candidates.Count - 1, GameTextValue.Item(target.ItemId))
            : GameText.Of(HarvestTexts.Text("DoingHarvestLast"), GameTextValue.Item(target.ItemId));

        Publish(state, doing, elementId);

        var before = Carried(state, target.ItemId);

        _lastTarget = target.ItemId;

        var outcome = await context.UseAsync(elementId, target.SkillId, cancellationToken).ConfigureAwait(false);

        if (outcome is UseOutcome.Used)
        {
            // The objects come within milliseconds of the harvest's end, in no set order (D75).
            await context.WaitUntilAsync(after => Carried(after, target.ItemId) != before, _objectsDelay, cancellationToken).ConfigureAwait(false);

            _harvested[target.ItemId] = _harvested.GetValueOrDefault(target.ItemId) + Math.Max(0, Carried(context.State, target.ItemId) - before);
            _balance.Record(target.JobId);
            _current?.Tour.Harvested();
        }
        else if (outcome is not UseOutcome.Busy)
        {
            _skipped.Add(elementId);
        }


        Publish(context.State, doing);

        return true;
    }

    private List<(int ElementId, HarvestTarget Target)> Candidates(BotContext context, HarvestZone zone, CharacterState state)
    {
        return MapHarvest.Candidates(context, TargetsOf(zone), state, _skipped);
    }

    private FrozenDictionary<int, HarvestTarget> TargetsOf(HarvestZone zone)
    {
        return _settings.Mode is HarvestMode.ByProfession && _settings.Order is HarvestOrder.Balanced ? _balance.Prioritise(zone.Targets) : zone.Targets;
    }

    private async Task GoOnAsync(BotContext context, HarvestRotationZone current, CancellationToken cancellationToken)
    {
        var zone = current.Zone;
        var tour = current.Tour;
        var remaining = tour.Remaining(zone);

        if (remaining.Count is 0)
        {
            await RotateAsync(context, current, cancellationToken).ConfigureAwait(false);

            return;
        }

        var state = context.State;
        var target = TargetOf(context, state, remaining, out var next);
        var inZone = state.MapId is { } mapId && zone.Maps.ContainsKey(mapId);

        Publish(state, GameText.Of(HarvestTexts.Text(inZone ? "DoingTravel" : "DoingReturn")), next: next);

        // A map of the zone the journey crosses, not yet gone through in this tour, is harvested if the server offers a wanted element
        // on it, whether the tour holds it back as growing or not: the reckoned time is only a guess (D392). The journey asks it of the
        // map's own information, with a list of its own, so that nothing is left skipped for the map the character stands on.
        bool Harvestable(CharacterState crossed)
        {
            return crossed.MapId is { } crossedId && zone.Maps.ContainsKey(crossedId) && !tour.IsVisited(crossedId)
                && MapHarvest.Candidates(context, TargetsOf(zone), crossed, []).Count > 0;
        }

        switch (await context.TravelAsync(remaining, Harvestable, cancellationToken).ConfigureAwait(false))
        {
            case TravelOutcome.Arrived:
                _failedJourneys = 0;
                break;
            case TravelOutcome.NoPath:
                foreach (var map in remaining)
                    tour.Unreach(map);

                // A zone of the rotation the character cannot reach is left for the others; the activity ends when none is left (D391).
                if (_rotation.Count > 1 && _settings.Place is HarvestPlace.Automatic)
                {
                    _rotation.Remove(current);
                    _current = _rotation[0];
                    _failedJourneys = 0;

                    break;
                }

                if (++_failedJourneys >= MostFailedJourneys)
                    throw Failure("No map of the zone can be reached.", "FailUnreachable");

                break;
            default:
                if (target is { } failed)
                    tour.Unreach(failed);

                if (++_failedJourneys >= MostFailedJourneys)
                    throw Failure("The way through the zone failed three times in a row.", "FailLost");

                break;
        }

        Publish(context.State, GameText.Of(HarvestTexts.Text("DoingTravel")));
    }

    // Nothing of the zone is ready now: the activity goes on to the zone of the rotation left longest ago that has maps to go through,
    // and waits only when every one still grows back or was swept for nothing (D145, D391).
    private async Task RotateAsync(BotContext context, HarvestRotationZone current, CancellationToken cancellationToken)
    {
        var now = context.Time.GetUtcNow();
        var zone = current.Zone;
        var tour = current.Tour;

        // Some maps still growing back leave the tour unfinished: it goes on from them when the rotation comes back.
        if (tour.NextReady(zone) is null)
        {
            var harvested = tour.HasHarvested;

            if (!tour.Next(zone))
            {
                _rotation.Remove(current);

                if (_rotation.Count is 0)
                    throw Failure("No map of the zone holds a resource to harvest.", "FailNothing");

                _current = _rotation[0];

                return;
            }

            // A tour that harvested nothing leaves the zone to grow back before the next sweeps it again: a minute, or until the
            // first map should have grown back, when the host knows (D128, D145).
            current.DryUntil = harvested ? null : tour.NextReady(zone) ?? now + _regrowthDelay;
        }

        current.CompletedAt = now;
        current.Done = true;

        // A level gained, or what the host learned since, moves the zones of the next round (D166).
        if (_settings.Place is HarvestPlace.Automatic && _rotation.TrueForAll(member => member.Done))
            Draw(context);

        if (HarvestRotation.Next(_rotation, now) is { } next)
        {
            _current = next;

            return;
        }

        // Every zone of the rotation still grows back or was swept for nothing: waited for here, until the first should be ready.
        if (HarvestRotation.WakeAt(_rotation, now) is { } ready)
        {
            await WaitGrowAsync(context, zone, ready, cancellationToken).ConfigureAwait(false);

            return;
        }

        Publish(context.State, GameText.Of(HarvestTexts.Text("DoingWaitGrow")));
        await context.WaitUntilAsync(state => HasWanted(zone, state), _regrowthDelay, cancellationToken).ConfigureAwait(false);
    }

    private long? TargetOf(BotContext context, CharacterState state, HashSet<long> remaining, out long? next)
    {
        next = null;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || !context.Maps.TryGet(mapId, out var map)
            || !context.World.TryGetVertex(map, cell, out var vertex)
            || !context.World.TryFind(vertex, remaining, WorldTravel.RulesOf(context), out var path))
        {
            return null;
        }

        var vertices = _graph.Vertices.Span;

        next = path.Steps.Count > 0 ? vertices[path.Steps[0].To].MapId : null;

        return vertices[path.To].MapId;
    }

    private async Task WaitGrowAsync(BotContext context, HarvestZone zone, DateTimeOffset ready, CancellationToken cancellationToken)
    {
        var wait = ready - context.Time.GetUtcNow();

        if (wait <= TimeSpan.Zero)
            return;

        Publish(context.State, GameText.Of(HarvestTexts.Text("DoingWaitGrowFor"), GameTextValue.Number((long)Math.Ceiling(wait.TotalSeconds))));
        await context.WaitUntilAsync(state => HasWanted(zone, state), wait, cancellationToken).ConfigureAwait(false);
    }

    private void Publish(CharacterState state, GameText doing, int? target = null, long? next = null)
    {
        if (_current is not { } current)
        {
            Volatile.Write(ref _report, new ActivityReport { Doing = doing });

            return;
        }

        var zone = current.Zone;
        var tour = current.Tour;

        Tally();

        var total = tour.Total(zone);
        var growing = tour.Growing(zone);

        // The place the activity chose names itself first, the 5th value (D166).
        var automatic = _settings.Place is HarvestPlace.Automatic && zone.SubAreas.Count > 0;
        var values = new[]
        {
            GameTextValue.Number(tour.Round),
            GameTextValue.Number(Math.Min(tour.Visited + 1, total)),
            GameTextValue.Number(total),
            GameTextValue.Number(growing),
            GameTextValue.SubArea(automatic ? zone.SubAreas[0] : 0),
        };
        var key = (growing > 0, automatic) switch
        {
            (true, true) => "ProgressAtGrowing",
            (true, false) => "ProgressGrowing",
            (false, true) => "ProgressAt",
            _ => "Progress",
        };
        var progress = new ActivityProgress(GameText.Of(HarvestTexts.Text(key), values), tour.Visited, total);

        Volatile.Write(ref _report, new ActivityReport
        {
            Doing = doing,
            Progress = progress,
            Counts = CountsOf(zone),
            Warnings = WarningsOf(zone, state),
            Focus = new ActivityFocus
            {
                MapId = state.MapId,
                TargetElementId = target,
                WantedElementIds = [.. state.Elements.Values.Where(element => element.EnabledSkills.Any(skill => zone.Targets.ContainsKey(skill.SkillId))).Select(element => element.ElementId)],
                NextMapId = next,
                Zone = tour.Maps(zone),
            },
        });
    }

    private void Tally()
    {
        Volatile.Write(ref _statistics, new ActivityStatistics
        {
            Items = _harvested.ToImmutableDictionary(),
            Banks = _banks,
            Fights = _fights.ToImmutableDictionary(),
        });
    }

    private ImmutableArray<ActivityCount> CountsOf(HarvestZone zone)
    {
        List<int> items = _settings.Mode is HarvestMode.ByProfession
            ? [.. _harvested.Keys.Order()]
            : [.. _settings.Resources.Where(zone.Wanted.Contains).Concat(_harvested.Keys.Where(item => !_settings.Resources.Contains(item))).Distinct()];

        // A rate over less than a minute says little: shown once the activity ran that long (D146).
        var hours = _context is { } context && _startedAt is { } started ? (context.Time.GetUtcNow() - started).TotalHours : 0;
        var counts = items.Select(item => new ActivityCount(
            hours >= 1.0 / 60
                ? GameText.Of(HarvestTexts.Text("CountItemRate"), GameTextValue.Item(item), GameTextValue.Number(_harvested.GetValueOrDefault(item)), GameTextValue.Number((long)Math.Round(_harvested.GetValueOrDefault(item) / hours)))
                : GameText.Of(HarvestTexts.Text("CountItem"), GameTextValue.Item(item), GameTextValue.Number(_harvested.GetValueOrDefault(item))),
            item)).ToList();

        if (_context?.State is { } now && _startJobs is { } before)
        {
            foreach (var job in now.Jobs.Values)
            {
                var gained = job.Experience - (before.TryGetValue(job.JobId, out var then) ? then.Experience : job.Experience);

                if (gained > 0)
                    counts.Add(new ActivityCount(GameText.Of(HarvestTexts.Text("CountExperience"), GameTextValue.Job(job.JobId), GameTextValue.Number(gained))));
            }
        }

        if (_banks > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountBanksOne"), HarvestTexts.Text("CountBanksMany"), _banks)));

        if (_bags > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountBagsOne"), HarvestTexts.Text("CountBagsMany"), _bags)));

        if (_eaten > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountEatenOne"), HarvestTexts.Text("CountEatenMany"), _eaten)));

        var fights = _fights.Values.Sum();

        if (fights > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountFightsOne"), HarvestTexts.Text("CountFightsMany"), fights, GameTextValue.Number(_fights.GetValueOrDefault(FightKind.Protector)))));

        return [.. counts];
    }

    private ImmutableArray<GameText> WarningsOf(HarvestZone zone, CharacterState state)
    {
        var warnings = new List<GameText>();

        if (_chestWarning is { } chest)
            warnings.Add(GameText.Of(HarvestTexts.Text(chest)));

        if (_settings.Mode is HarvestMode.ByProfession)
            return [.. warnings];

        foreach (var item in _settings.Resources)
        {
            var lowest = zone.Targets.Values.Where(target => target.ItemId == item).OrderBy(target => target.Level).FirstOrDefault();

            if (lowest.SkillId is 0)
                continue;

            var level = LevelOf(state, lowest.JobId);

            if (level < lowest.Level)
            {
                warnings.Add(GameText.Of(
                    HarvestTexts.Text("WarningLevel"),
                    GameTextValue.Item(item),
                    GameTextValue.Job(lowest.JobId),
                    GameTextValue.Number(lowest.Level),
                    GameTextValue.Number(level)));
            }
        }

        return [.. warnings];
    }
}
