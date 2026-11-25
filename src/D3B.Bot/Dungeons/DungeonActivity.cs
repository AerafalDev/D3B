using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fighting;
using D3B.Bot.Fights;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Npcs;
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
/// entered; on the map the boss's win leads to, or once every room is won, the activity ends: the exit comes next (D520).
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
    private ActivityReport? _report;
    private int? _inside;
    private int _failedJourneys;
    private int _unanswered;
    private int _entered;
    private int _rooms;
    private int _lost;
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

    /// <summary>Initializes a new instance of the <see cref="DungeonActivity"/> class.</summary>
    /// <param name="settings">Which dungeon, and the journey there.</param>
    /// <param name="tables">The installed build's tables: the dungeons, and the bankers no keeper is.</param>
    public DungeonActivity(DungeonSettings settings, GameTables tables)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);

        _settings = settings;
        _tables = tables;
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

    private static bool CarriesObjects(CharacterState state, ReadOnlySpan<DungeonObject> objects)
    {
        foreach (var required in objects)
        {
            if (Carried(state, required.ItemId) < required.Quantity)
                return false;
        }

        return true;
    }

    private static int Carried(CharacterState state, int itemId)
    {
        return state.Inventory.Values.Where(stack => stack.ItemId == itemId).Sum(stack => stack.Quantity);
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(DungeonTexts.Text(key)));
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
            if (!dungeons.TryGetDungeon(listed, out var inside))
                continue;

            if (dungeons.RoomsOf(inside).Contains(mapId))
            {
                if (_inside != inside.Id)
                    _won.Clear();

                _inside = inside.Id;

                return await RoomAsync(context, inside, mapId, cancellationToken).ConfigureAwait(false);
            }

            // The boss's win leads to a map of the dungeon's sub-area that is no room, where its exit stands (client-game.md).
            if (_inside == inside.Id && IsFinalMap(state, inside))
            {
                Publish("DoingRooms");

                return false;
            }
        }

        var (index, dungeon) = NextEnterable(state);

        if (mapId == dungeon.EntranceMapId)
        {
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
        if (ending is { Outcome: FightOutcome.Lost })
        {
            // Measured nowhere yet: the references wake the character at its saved zaap. The dungeon is left for the run (D525).
            _lost++;

            if (_inside is { } dungeon)
                _beaten.Add(dungeon);

            _inside = null;
        }
        else if (ending is not null)
        {
            _rooms++;
        }

        Publish(_inside is null ? "DoingStart" : "DoingRooms");

        return response;
    }

    /// <inheritdoc/>
    public override void Restart()
    {
        // A dungeon shut by a journey or a keeper that did not answer may open once the character is put in order (D519).
        _busy.Reset();
        _shut.Clear();
        _failedJourneys = 0;
        _unanswered = 0;
    }

    private (int Index, Dungeon Dungeon) NextEnterable(CharacterState state)
    {
        var listed = _settings.Dungeons;

        if (listed.Count is 0)
            throw Impossible("The profile lists no dungeon.", GameText.Of(DungeonTexts.Text("FailNoDungeon")));

        ActivityException? refused = null;
        var final = true;

        for (var turn = 0; turn < listed.Count; turn++)
        {
            var index = (_next + turn) % listed.Count;

            if (Refusal(state, listed[index], out var dungeon) is not { } why)
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

    private ActivityException? Refusal(CharacterState state, int id, out Dungeon dungeon)
    {
        var dungeons = _tables.Dungeons;

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

        return null;
    }

    private bool IsFinalMap(CharacterState state, Dungeon dungeon)
    {
        var dungeons = _tables.Dungeons;

        return state.MapId is { } map
            && state.SubAreaId is { } subArea
            && map != dungeon.EntranceMapId
            && map != dungeon.ExitMapId
            && dungeons.SubAreasOf(dungeon).Contains(subArea)
            && !dungeons.RoomsOf(dungeon).Contains(map);
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

            // A fight that begins cuts the step; the run plays it, and the next step is the next room's.
            await MapFight.FightAsync(context, [], 1, 8, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // The room is won and the server did not move the character: the next room is walked to (D525).
        _won.Add(mapId);

        var left = new HashSet<long>();

        foreach (var room in _tables.Dungeons.RoomsOf(dungeon))
        {
            if (!_won.Contains(room))
                left.Add(room);
        }

        if (left.Count is 0)
        {
            Publish("DoingRooms");

            return false;
        }

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

        var dungeons = _tables.Dungeons;
        var keyring = dungeon.IsOnKeyring && Carried(context.State, DungeonCatalog.KeyringItemId) > 0;
        var key = CarriesObjects(context.State, dungeons.ObjectsOf(dungeon));

        // The keyring spends nothing, a key is spent (D523): the first is chosen whenever the keeper offers it.
        int? Choose(NpcDialog dialog)
        {
            var replies = dialog.Replies;

            if (keyring && replies.FirstOrDefault(dungeons.IsKeyringReply) is > 0 and var byKeyring)
                return byKeyring;

            if (key && replies.FirstOrDefault(dungeons.IsKeyReply) is > 0 and var byKey)
                return byKey;

            return replies.FirstOrDefault(dungeons.IsConfirmation) is > 0 and var confirmed ? confirmed : null;
        }

        var unanswered = false;

        // The server places the NPCs with the map, and no table says which is the keeper: each is asked in turn.
        foreach (var npcId in context.State.Npcs.Values.Select(npc => npc.NpcId).Where(npc => !_tables.Banks.TryGetBanker(npc, out _)).Distinct().ToList())
        {
            var outcome = await context.TalkAsync(npcId, Choose, cancellationToken).ConfigureAwait(false);

            if (context.State.MapId != dungeon.EntranceMapId)
            {
                if (context.State.MapId is not { } arrived || !dungeons.RoomsOf(dungeon).Contains(arrived))
                    return false;

                _entered++;
                _unanswered = 0;
                _inside = dungeon.Id;
                _won.Clear();
                Publish("DoingRooms");

                return true;
            }

            // An NPC who did not answer may still be the keeper: the others are asked, and the door tried again a few times.
            unanswered |= outcome is NpcTalkOutcome.NoAnswer or NpcTalkOutcome.Busy or NpcTalkOutcome.NotPlaced;
        }

        if (unanswered)
        {
            if (++_unanswered >= MostUnanswered)
            {
                _unanswered = 0;
                _shut[dungeon.Id] = Failure($"No NPC at the door of dungeon {dungeon.Id} answered, {MostUnanswered} times in a row.", "FailNoKeeper");
            }

            return false;
        }

        // A keeper may turn away one dungeon of several, its keyring's charge spent: the others are still entered (D524).
        _shut[dungeon.Id] = Impossible($"No NPC at the door of dungeon {dungeon.Id} let the character in.", GameText.Of(DungeonTexts.Text("FailNoKeeper")));

        return false;
    }

    private void Publish(string doing)
    {
        Volatile.Write(ref _report, new ActivityReport
        {
            Doing = GameText.Of(DungeonTexts.Text(doing)),
            Counts =
            [
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountEnteredOne"), DungeonTexts.Text("CountEnteredMany"), _entered)),
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountRoomOne"), DungeonTexts.Text("CountRoomMany"), _rooms)),
                new ActivityCount(GameText.Counted(DungeonTexts.Text("CountLostOne"), DungeonTexts.Text("CountLostMany"), _lost)),
            ],
        });
    }
}
