using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Fighting;
using D3B.Bot.Fights;
using D3B.Bot.Groups;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Dungeons;
using D3B.Protocol.Game;

namespace D3B.Bot.Dungeons;

/// <summary>Running the dungeons of the client's table a profile lists, in turn, each door reached and its keeper talked to, the server moving the character on (D520, D524).</summary>
/// <remarks>
/// <para>
/// Each step is chosen from where the character is (D87). In a room of a dungeon of the profile, that dungeon is the one run.
/// Else the dungeon to enter is the next of the profile's in turn whose level the character has and whose key or keyring it
/// carries (D523, D524), those it cannot enter left out; with none left the activity ends saying why, which no restart changes.
/// The character travels to that dungeon's door. On the door's map it talks to the NPCs the server shows there, bankers aside, until one lets it in: each reply chosen
/// among those a question offers, the keyring's first when offered, then the key's, then the keeper's confirmation, by the ids
/// the pack names from their French words (D522). Measured on the owner's captures (client-game.md): the first room comes
/// 28 to 41 ms after the confirmation, a key spent and the keyring nothing.
/// </para>
/// <para>
/// In a room the character regenerates to its share of life, then fights the room's groups, the AI playing each fight. At a
/// room's win the server names the next room in the fight end's millisecond, unasked (client-game.md): the next step is that
/// room's. A room whose groups are beaten and that the server did not leave is left on foot, through the world graph, for the
/// nearest room of the dungeon not yet won (D525). A fight lost leaves the dungeon for the run, the others of the profile still
/// entered.
/// </para>
/// <para>
/// On the map the boss's win leads to, or once every room is won, the character talks to the NPCs there until one lets it
/// out, by a reply of the pack's exits, and the run of the dungeon is counted (D526). Out of every dungeon, it banks the loot of
/// its fights, keys and the keyring kept, once its weight and the pods the next run is likely to bring reach the banking share:
/// the most a run of that dungeon brought, else the most any run did (D527); the activity is done once the profile's count
/// of runs is, or goes on until no dungeon of the profile can be entered.
/// </para>
/// <para>
/// A master's followers each enter and leave by themselves behind it, as measured in every captured party (D528): the
/// activity says which dungeon the character is in (<see cref="DungeonInside"/>), leaves out of its turn a dungeon a follower
/// cannot enter, and its group banks together outside the dungeons, every member keeping its keys and the keyring (D529).
/// At the door, before the keeper, it gives each follower lacking a key one of its own by an exchange, keeping what its own
/// entry needs (<see cref="DungeonKeys"/>, D569): a dungeon is left out only when its spare keys do not cover the followers.
/// </para>
/// </remarks>
public sealed class DungeonActivity : BotActivity
{
    private const int MostFailedJourneys = 3;
    private const int MostUnanswered = 3;

    // Tenths of a second a life point takes, when the server did not say: as measured in every capture (D153).
    private const int RegainPoll = 5;

    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _regainStep = TimeSpan.FromSeconds(10);

    // A room's groups come with its map's information; a room left with none is walked on from after this.
    private static readonly TimeSpan _groupDelay = TimeSpan.FromSeconds(5);

    private readonly DungeonSettings _settings;
    private readonly GameTables _tables;
    private readonly BusyWait _busy = new();
    private readonly Dictionary<int, ActivityException> _shut = [];
    private readonly HashSet<int> _beaten = [];
    private readonly HashSet<long> _won = [];
    private readonly Dictionary<int, long> _loot = [];
    private readonly Dictionary<int, int> _gains = [];
    private readonly RecoveryLadder _bankings = new();
    private readonly BankingPreferences _banking;
    private readonly Bots? _bots;
    private ActivityReport? _report;
    private int? _inside;

    // The dungeon the character is in, as its followers read it from their own threads (D528): 0 for none, as no id of the table is.
    private int _shownInside;
    private long? _fightingIn;
    private int _failedJourneys;
    private int _unanswered;
    private int _unhanded;
    private int _entered;
    private int _rooms;
    private int _lost;
    private int _runs;
    private int _banks;
    private int? _weightAtEntry;
    private bool _estimateWaived;
    private int _next;

    /// <inheritdoc/>
    public override bool RestartsAfterFailure => _settings.RestartAfterFailure;

    /// <inheritdoc/>
    public override string Name => "Dungeon";

    /// <inheritdoc/>
    public override ActivityReport? Report => Volatile.Read(ref _report);

    /// <inheritdoc/>
    public override bool UsesTeleports => _settings.UseZaaps;

    /// <inheritdoc/>
    public override bool FightsItsFights => true;

    /// <inheritdoc/>
    public override int? DungeonInside => Volatile.Read(ref _shownInside) is > 0 and var id ? id : null;

    /// <inheritdoc/>
    /// <remarks>
    /// No pods watch, whose group's bank would deposit all but what is worn and bank inside a dungeon: the group banks as the
    /// activity says each member is full, out of every dungeon, each keeping its keys and the keyring (D529).
    /// </remarks>
    public override bool BanksTheGroup => true;

    /// <inheritdoc/>
    public override bool BanksByTeleport => _banking.UseZaapsOf(false, _settings.UseZaaps);

    private int? Inside
    {
        get => _inside;
        set
        {
            _inside = value;
            Volatile.Write(ref _shownInside, value ?? 0);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="DungeonActivity"/> class.</summary>
    /// <param name="settings">Which dungeon, and the journey there.</param>
    /// <param name="tables">The installed build's tables: the dungeons, and the bankers no keeper is.</param>
    /// <param name="banking">The general banking settings (D405); the defaults when none is given.</param>
    /// <param name="bots">The host's bots, whose followers of the character must each be able to enter a dungeon too (D528); none for a character alone.</param>
    public DungeonActivity(DungeonSettings settings, GameTables tables, BankingPreferences? banking = null, Bots? bots = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);

        _settings = settings;
        _tables = tables;
        _banking = banking ?? new BankingPreferences();
        _bots = bots;
        _report = new ActivityReport { Doing = GameText.Of(DungeonTexts.Text("DoingStart")) };
    }

    /// <summary>Gets whether a character may enter a dungeon: with the keyring when the dungeon is on it, else with every object it requires.</summary>
    /// <param name="state">The character.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Whether it carries what the keeper asks.</returns>
    public static bool CarriesKey(CharacterState state, DungeonCatalog dungeons, Dungeon dungeon)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dungeons);

        return (dungeon.IsOnKeyring && Carried(state, DungeonCatalog.KeyringItemId) > 0) || CarriesObjects(state, dungeons.ObjectsOf(dungeon));
    }

    /// <summary>Tells whether a character carries every object a dungeon requires.</summary>
    /// <param name="state">The character.</param>
    /// <param name="objects">The objects, such as the dungeon's key.</param>
    /// <returns>Whether it does.</returns>
    internal static bool CarriesObjects(CharacterState state, ReadOnlySpan<DungeonObject> objects)
    {
        foreach (var required in objects)
        {
            if (Carried(state, required.ItemId) < required.Quantity)
                return false;
        }

        return true;
    }

    /// <summary>Gets how many of an item a character carries.</summary>
    /// <param name="state">The character.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The quantity, in all its stacks.</returns>
    internal static int Carried(CharacterState state, int itemId)
    {
        return state.Inventory.Values.Where(stack => stack.ItemId == itemId).Sum(stack => stack.Quantity);
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(DungeonTexts.Text(key)));
    }

    private static ActivityException Failure(string message, string key, GameTextValue value)
    {
        return new ActivityException(message, GameText.Of(DungeonTexts.Text(key), value));
    }

    // What the profile, the character's level or what it carries makes impossible, which no restart would change (D519, D523).
    private static ActivityException Impossible(string message, GameText reason)
    {
        return new ActivityException(message, reason) { IsFinal = true };
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _busy.WaitAsync(context, Failure, cancellationToken).ConfigureAwait(false) || context.State is not { MapId: { } mapId } state)
            return true;

        var dungeons = _tables.Dungeons;

        foreach (var listed in _settings.Dungeons)
        {
            // A dungeon a fight was lost in is left for the run, a room of it as any other map (D525).
            if (!dungeons.TryGetDungeon(listed, out var inside) || _beaten.Contains(inside.Id))
                continue;

            if (dungeons.RoomsOf(inside).Contains(mapId))
            {
                if (_inside != inside.Id)
                    _won.Clear();

                Inside = inside.Id;

                return await RoomAsync(context, inside, mapId, cancellationToken).ConfigureAwait(false);
            }

            // The boss's win leads to a map of the dungeon's sub-area that is no room, where its exit stands (client-game.md).
            if (_inside == inside.Id && DungeonDoors.IsFinal(state, dungeons, inside))
                return await ExitAsync(context, inside, cancellationToken).ConfigureAwait(false);
        }

        var (index, dungeon) = NextEnterable(context);

        if (mapId == dungeon.EntranceMapId)
        {
            // A follower lacking a key is given the master's spare one first, so that each enters by itself (D528, D569).
            if (await HandKeysAsync(context, dungeon, cancellationToken).ConfigureAwait(false))
                return true;

            // The next run enters the dungeon after this one in the profile's list (D524).
            if (await EnterAsync(context, dungeon, cancellationToken).ConfigureAwait(false))
                _next = (index + 1) % _settings.Dungeons.Count;
        }
        else
        {
            await TravelAsync(context, dungeon, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        _busy.Reset();

        return InterruptionResponse.Resume;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnFightOver(FightEnd? ending, InterruptionResponse response)
    {
        if (ending is { } end)
        {
            foreach (var (item, quantity) in end.Items)
                _loot[item] = _loot.GetValueOrDefault(item) + quantity;
        }

        if (ending is { Outcome: FightOutcome.Lost })
        {
            // Measured nowhere yet: the references wake the character at its saved zaap. The dungeon is left for the run (D525).
            _lost++;

            if (_inside is { } dungeon)
                _beaten.Add(dungeon);

            Inside = null;
        }
        else if (ending is not null && _fightingIn is { } room)
        {
            // A room won is never walked back to, whether the server moves the character on or not (D525).
            Won(room);
        }

        _fightingIn = null;
        Publish(_inside is null ? "DoingStart" : "DoingRooms");

        return response;
    }

    /// <inheritdoc/>
    /// <remarks>Inside a dungeon the character never banks: leaving would lose the run, the bank waits for the exit (D526).</remarks>
    public override bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (InDungeon(state) || state.MaxWeight <= 0)
            return false;

        // The pods the next run is likely to bring: banked before the door rather than full in the middle of a dungeon (D527).
        var expected = _estimateWaived ? 0 : ExpectedGain();

        return (state.Weight + (long)expected) * 100L >= (long)_banking.FullAt * state.MaxWeight;
    }

    /// <summary>Gets the pods a run of the next dungeon of the profile is likely to bring: the most one brought, else the most any run did.</summary>
    /// <returns>The pods; none before a run was measured (D527).</returns>
    public int ExpectedGain()
    {
        var listed = _settings.Dungeons;

        if (listed.Count > 0 && _gains.TryGetValue(listed[_next % listed.Count], out var known))
            return known;

        return _gains.Count is 0 ? 0 : _gains.Values.Max();
    }

    // In a dungeon run, or in a room of one of the profile's, as a run started or restarted there is.
    private bool InDungeon(CharacterState state)
    {
        if (_inside is not null)
            return true;

        if (state.MapId is not { } mapId)
            return false;

        var dungeons = _tables.Dungeons;

        foreach (var listed in _settings.Dungeons)
        {
            if (dungeons.TryGetDungeon(listed, out var dungeon) && dungeons.RoomsOf(dungeon).Contains(mapId))
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    /// <remarks>The loot of its fights, but a dungeon's key and the keyring, which the next runs open the dungeons with (D526).</remarks>
    public override bool Deposits(InventoryItem stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        return _loot.ContainsKey(stack.ItemId) && !IsKey(stack.ItemId);
    }

    /// <inheritdoc/>
    /// <remarks>A dungeon's key and the keyring, which each member of a group enters by itself with (D528, D529).</remarks>
    public override bool Keeps(InventoryItem stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        return IsKey(stack.ItemId);
    }

    /// <inheritdoc/>
    public override async Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Publish("DoingBank");

        // Any banking that fails is tried again up the ladder, as the hunt's (D518).
        var useZaaps = _banking.UseZaapsOf(false, _settings.UseZaaps);
        var outcome = await _bankings.TryAsync(context, "the bank", token => context.BankAsync(Deposits, 0, useZaaps, token), banked => banked is BankOutcome.Done, cancellationToken).ConfigureAwait(false);

        if (outcome is not BankOutcome.Done)
            return false;

        _banks++;

        // What the bank could not take stays: the estimate waits for the next run's exit, rather than banking again for nothing.
        _estimateWaived = true;
        Publish("DoingStart");

        return true;
    }

    /// <inheritdoc/>
    public override void OnGroupUnloaded()
    {
        // As its own unloading: what the bank or an exchange left waits for the next run's exit (D527, D529).
        _banks++;
        _estimateWaived = true;
        Publish("DoingStart");
    }

    /// <inheritdoc/>
    public override void Restart()
    {
        // A dungeon shut by a journey or a keeper that did not answer may open once the character is put in order (D519).
        _busy.Reset();
        _bankings.Reset();
        _shut.Clear();
        _failedJourneys = 0;
        _unanswered = 0;
        _unhanded = 0;
    }

    private (int Index, Dungeon Dungeon) NextEnterable(BotContext context)
    {
        var listed = _settings.Dungeons;

        if (listed.Count is 0)
            throw Impossible("The profile lists no dungeon.", GameText.Of(DungeonTexts.Text("FailNoDungeon")));

        ActivityException? refused = null;
        var final = true;

        for (var turn = 0; turn < listed.Count; turn++)
        {
            var index = (_next + turn) % listed.Count;

            if (Refusal(context, listed[index], out var dungeon) is not { } why)
                return (index, dungeon);

            refused ??= why;
            final &= why.IsFinal;
        }

        // One dungeon says what keeps it shut; several, that none opens. A journey's failure may come right on a restart.
        if (listed.Count is 1)
            throw refused!;

        throw new ActivityException("No dungeon of the profile can be entered: the character lacks the level or the key of each, or no way leads to them.", GameText.Of(DungeonTexts.Text("FailNoneEnterable")))
        {
            IsFinal = final,
        };
    }

    private ActivityException? Refusal(BotContext context, int id, out Dungeon dungeon)
    {
        var dungeons = _tables.Dungeons;
        var state = context.State;

        if (!dungeons.TryGetDungeon(id, out dungeon))
            return Impossible($"Dungeon {id} is not in the client's table.", GameText.Of(DungeonTexts.Text("FailUnknownDungeon")));

        if (_shut.TryGetValue(id, out var shut))
            return shut;

        if (_beaten.Contains(id))
            return Impossible($"A fight was lost in dungeon {id}: it is left for the run.", GameText.Of(DungeonTexts.Text("FailLost")));

        if (state.Level is { } level && level < dungeon.MinLevel)
        {
            return Impossible(
                $"The character is level {level}; dungeon {dungeon.Id} lets in from level {dungeon.MinLevel}.",
                GameText.Of(DungeonTexts.Text("FailTooLow"), GameTextValue.Number(level), GameTextValue.Number(dungeon.MinLevel)));
        }

        if (!CarriesKey(state, dungeons, dungeon))
            return Impossible($"The character carries neither the key of dungeon {dungeon.Id} nor a keyring that opens it.", GameText.Of(DungeonTexts.Text("FailNoKey")));

        return FollowersRefusal(context, dungeon);
    }

    // The owner's choice (D528): a dungeon one of the character's followers cannot enter is left out of the group's turn. A follower
    // not connected says nothing of what it carries, and one left behind (D490) is not waited for.
    private ActivityException? FollowersRefusal(BotContext context, Dungeon dungeon)
    {
        if (_bots is null)
            return null;

        var gate = context.Actor.Gate as GroupGate;
        var dungeons = _tables.Dungeons;

        // What the master carries beyond its own entry, which covers the followers lacking a key in turn (D569).
        var spare = DungeonKeys.Spare(context.State, dungeons, dungeon);

        foreach (var follower in GroupChain.Order(context.State, _bots.Groups.FollowersOf(context.Actor.Key)))
        {
            if (!follower.IsConnected || gate?.IsBehind(follower.Key) == true)
                continue;

            var theirs = follower.State;
            var name = GameTextValue.Of(theirs.Name);

            if (theirs.Level is { } level && level < dungeon.MinLevel)
            {
                return Impossible(
                    $"Follower {follower.Key.CharacterId} is level {level}; dungeon {dungeon.Id} lets in from level {dungeon.MinLevel}.",
                    GameText.Of(DungeonTexts.Text("FailFollowerTooLow"), name, GameTextValue.Number(level), GameTextValue.Number(dungeon.MinLevel)));
            }

            if (!DungeonKeys.TryCover(spare, DungeonKeys.Lacking(theirs, dungeons, dungeon)))
                return Impossible($"Follower {follower.Key.CharacterId} carries neither the key of dungeon {dungeon.Id} nor a keyring that opens it, and the character has no key to spare.", GameText.Of(DungeonTexts.Text("FailFollowerNoKey"), name));

            // A keeper turning a follower away, such as its keyring's charge spent, shuts the dungeon for the group's run (D524).
            if (follower.Activity is { IsOver: false, Activity: FollowActivity follow } && follow.TurnedAwayFrom.Contains(dungeon.Id))
                return Impossible($"The keeper of dungeon {dungeon.Id} turned follower {follower.Key.CharacterId} away.", GameText.Of(DungeonTexts.Text("FailFollowerTurnedAway"), name));
        }

        return null;
    }

    // Whether it gave a follower keys this step, one follower an exchange, in the chain's order: at the door, the whole group
    // gathered there, each follower lacking a key is given what it lacks of the master's spare ones (D569). An exchange that does
    // not conclude is tried again a few times, then shuts the dungeon for the run, lest the group wait at its door for ever.
    private async Task<bool> HandKeysAsync(BotContext context, Dungeon dungeon, CancellationToken cancellationToken)
    {
        if (_bots is null || context.Actor.Gate is not GroupGate gate)
            return false;

        var dungeons = _tables.Dungeons;

        bool Keyless(BotActor follower)
        {
            return follower.IsConnected && !gate.IsBehind(follower.Key) && DungeonKeys.Lacking(follower.State, dungeons, dungeon).Count > 0;
        }

        if (!_bots.Groups.FollowersOf(context.Actor.Key).Any(Keyless))
            return false;

        await gate.GatherAsync(context, cancellationToken).ConfigureAwait(false);

        var spare = DungeonKeys.Spare(context.State, dungeons, dungeon);

        foreach (var follower in GroupChain.Order(context.State, _bots.Groups.FollowersOf(context.Actor.Key)).Where(Keyless))
        {
            var lacking = DungeonKeys.Lacking(follower.State, dungeons, dungeon);

            // Its turn already reckoned the keys: a follower they no longer cover, such as one a key was spent for meanwhile, is
            // left to it, which leaves the dungeon out at the next step.
            if (!DungeonKeys.TryCover(spare, lacking))
                return false;

            Volatile.Write(ref _report, Report! with { Doing = GameText.Of(DungeonTexts.Text("DoingHandKeys"), GameTextValue.Of(follower.State.Name)) });

            if (await GroupHand.GiveAsync(context, _bots.Groups, follower, DungeonKeys.StacksOf(context.State, lacking), cancellationToken).ConfigureAwait(false))
            {
                _unhanded = 0;
            }
            else if (++_unhanded >= MostUnanswered)
            {
                _unhanded = 0;
                _shut[dungeon.Id] = Failure($"Follower {follower.Key.CharacterId} took none of the keys the character gave it at the door of dungeon {dungeon.Id}, {MostUnanswered} times in a row.", "FailNoKeyHanded", GameTextValue.Of(follower.State.Name));
            }

            return true;
        }

        return false;
    }

    private bool IsKey(int itemId)
    {
        if (itemId is DungeonCatalog.KeyringItemId)
            return true;

        var dungeons = _tables.Dungeons;

        foreach (var listed in _settings.Dungeons)
        {
            if (dungeons.TryGetDungeon(listed, out var dungeon))
            {
                foreach (var required in dungeons.ObjectsOf(dungeon))
                {
                    if (required.ItemId == itemId)
                        return true;
                }
            }
        }

        return false;
    }

    // Measured (client-game.md): the exit's NPC asks a question offering a reply that leaves, the exit map coming after it.
    private async Task<bool> ExitAsync(BotContext context, Dungeon dungeon, CancellationToken cancellationToken)
    {
        Publish("DoingExit");

        var outcome = await DungeonDoors.ExitAsync(context, _tables, cancellationToken).ConfigureAwait(false);

        if (outcome is DoorOutcome.Through)
        {
            // Out of the dungeon: its run is counted, what it brought measured, and the next is the next dungeon's of the profile (D524, D527).
            if (_weightAtEntry is { } before)
                _gains[dungeon.Id] = Math.Max(_gains.GetValueOrDefault(dungeon.Id), Math.Max(0, context.State.Weight - before));

            _weightAtEntry = null;
            _estimateWaived = false;
            _runs++;
            Inside = null;
            _won.Clear();
            _unanswered = 0;
            Publish("DoingStart");

            return _settings.Runs is 0 || _runs < _settings.Runs;
        }

        if (outcome is DoorOutcome.Unanswered && ++_unanswered < MostUnanswered)
            return true;

        _unanswered = 0;

        throw Failure($"No NPC of the last map of dungeon {dungeon.Id} let the character out.", "FailNoExit");
    }

    private async Task<bool> RoomAsync(BotContext context, Dungeon dungeon, long mapId, CancellationToken cancellationToken)
    {
        // The map's information names the room's groups: until it came, the character has no cell.
        if (context.State.Cell is null)
        {
            await context.WaitUntilAsync(state => state.Cell is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

            return true;
        }

        Publish("DoingRooms");

        if (context.State.MonsterGroups.Count is 0)
            await context.WaitUntilAsync(state => state.MapId != mapId || state.MonsterGroups.Count > 0, _groupDelay, cancellationToken).ConfigureAwait(false);

        if (context.State.MapId != mapId)
            return true;

        if (context.State.MonsterGroups.Count > 0)
        {
            if (await RegainAsync(context, cancellationToken).ConfigureAwait(false))
                return true;

            // A fight that begins cuts the step; the run plays it, and the next step is the next room's. Groups none of which
            // could be attacked, out of reach or unanswered, leave the room as won, so that the character is not left idle.
            _fightingIn = mapId;
            await MapFight.FightAsync(context, [], 1, 8, cancellationToken).ConfigureAwait(false);
            _fightingIn = null;
        }

        // The room is won and the server did not move the character: the next room is walked to (D525).
        Won(mapId);

        var left = new HashSet<long>();

        foreach (var room in _tables.Dungeons.RoomsOf(dungeon))
        {
            if (!_won.Contains(room))
                left.Add(room);
        }

        // Every room won and no map the boss's win led to: the exit is in the last room, as Forgerons' (client-game.md).
        if (left.Count is 0)
            return await ExitAsync(context, dungeon, cancellationToken).ConfigureAwait(false);

        switch (await context.TravelAsync(left, state => state.MonsterGroups.Count > 0, _settings.UseZaaps, cancellationToken).ConfigureAwait(false))
        {
            case TravelOutcome.Arrived:
                _failedJourneys = 0;
                break;
            case TravelOutcome.Busy:
                await context.WaitUntilAsync(state => !state.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);
                break;
            default:
                if (++_failedJourneys >= MostFailedJourneys)
                    throw Failure($"No room of dungeon {dungeon.Id} left to win can be walked to from room {mapId}.", "FailNoRoom");

                break;
        }

        return true;
    }

    private void Won(long room)
    {
        if (_won.Add(room))
            _rooms++;
    }

    // Before a room's fight the character regenerates to its share of life, a point each interval the server gave (D153).
    private async Task<bool> RegainAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Life is not { } life || state.MaxLife is not { } most || most <= 0)
            return false;

        var share = (((long)_settings.LifeShare * most) + 99) / 100;

        if (life >= share)
            return false;

        var point = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
        var regenerating = TimeSpan.FromTicks(point.Ticks * (share - life));

        Volatile.Write(ref _report, Report! with { Doing = GameText.Of(DungeonTexts.Text("DoingRegenerate"), GameTextValue.Number((long)Math.Ceiling(regenerating.TotalSeconds))) });
        await context.WaitUntilAsync(_ => false, regenerating < _regainStep ? regenerating : _regainStep, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task TravelAsync(BotContext context, Dungeon dungeon, CancellationToken cancellationToken)
    {
        Publish("DoingTravel");

        switch (await context.TravelAsync(new HashSet<long> { dungeon.EntranceMapId }, null, _settings.UseZaaps, cancellationToken).ConfigureAwait(false))
        {
            case TravelOutcome.Arrived:
                _failedJourneys = 0;
                break;
            case TravelOutcome.Busy:
                await context.WaitUntilAsync(state => !state.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);
                break;
            case var failed:
                // No path may be a transition this journey banned (D517): it counts as a failed journey. Three in a row shut the
                // dungeon for the run, the others of the profile still entered (D524).
                if (++_failedJourneys >= MostFailedJourneys)
                {
                    _failedJourneys = 0;
                    _shut[dungeon.Id] = failed is TravelOutcome.NoPath
                        ? Failure($"No way leads to the door of dungeon {dungeon.Id}, map {dungeon.EntranceMapId}.", "FailNoPath")
                        : Failure($"The journey to the door of dungeon {dungeon.Id} failed {MostFailedJourneys} times in a row.", "FailJourney");
                }

                break;
        }
    }

    private async Task<bool> EnterAsync(BotContext context, Dungeon dungeon, CancellationToken cancellationToken)
    {
        Publish("DoingEnter");

        switch (await DungeonDoors.EnterAsync(context, _tables, dungeon, cancellationToken).ConfigureAwait(false))
        {
            case DoorOutcome.Through:
                _entered++;
                _unanswered = 0;
                _weightAtEntry = context.State.Weight;
                Inside = dungeon.Id;
                _won.Clear();
                Publish("DoingRooms");

                return true;
            case DoorOutcome.Elsewhere:
                return false;
            case DoorOutcome.Unanswered:
                // An NPC who did not answer may still be the keeper: the door is tried again a few times.
                if (++_unanswered >= MostUnanswered)
                {
                    _unanswered = 0;
                    _shut[dungeon.Id] = Failure($"No NPC at the door of dungeon {dungeon.Id} answered, {MostUnanswered} times in a row.", "FailNoKeeper");
                }

                return false;
            default:
                // A keeper may turn away one dungeon of several, its keyring's charge spent: the others are still entered (D524).
                _shut[dungeon.Id] = Impossible($"No NPC at the door of dungeon {dungeon.Id} let the character in.", GameText.Of(DungeonTexts.Text("FailNoKeeper")));

                return false;
        }
    }

    private void Publish(string doing)
    {
        Volatile.Write(ref _report, new ActivityReport
        {
            Doing = GameText.Of(DungeonTexts.Text(doing)),
            Counts =
            [
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountRunOne"), DungeonTexts.Text("CountRunMany"), _runs)),
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountEnteredOne"), DungeonTexts.Text("CountEnteredMany"), _entered)),
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountRoomOne"), DungeonTexts.Text("CountRoomMany"), _rooms)),
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountLostOne"), DungeonTexts.Text("CountLostMany"), _lost)),
                .. _loot.OrderByDescending(pair => pair.Value).Select(pair => new ActivityCount(GameText.Of(DungeonTexts.Text("CountLoot"), GameTextValue.Number(pair.Value), GameTextValue.Item(pair.Key)), pair.Key)),
            ],
        });
    }
}
