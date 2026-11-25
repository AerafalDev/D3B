using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Npcs;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Dungeons;

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
/// <para>In a room of the dungeon the activity ends: the rooms' chain comes next (D520).</para>
/// </remarks>
public sealed class DungeonActivity : BotActivity
{
    private const int MostFailedJourneys = 3;
    private const int MostUnanswered = 3;

    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);

    private readonly DungeonSettings _settings;
    private readonly GameTables _tables;
    private readonly BusyWait _busy = new();
    private ActivityReport? _report;
    private int _failedJourneys;
    private int _unanswered;
    private int _entered;
    private readonly Dictionary<int, ActivityException> _shut = [];
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
            if (dungeons.TryGetDungeon(listed, out var inside) && dungeons.RoomsOf(inside).Contains(mapId))
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

    private (int Index, Dungeon Dungeon) NextEnterable(CharacterState state)
    {
        var listed = _settings.Dungeons;

        if (listed.Count is 0)
            throw Impossible("The profile lists no dungeon.", GameText.Of(DungeonTexts.Text("FailNoDungeon")));

        ActivityException? refused = null;

        for (var turn = 0; turn < listed.Count; turn++)
        {
            var index = (_next + turn) % listed.Count;

            if (Refusal(state, listed[index], out var dungeon) is { } why)
                refused ??= why;
            else
                return (index, dungeon);
        }

        // One dungeon says what keeps it shut; several, that none opens.
        if (listed.Count is 1)
            throw refused!;

        throw Impossible("No dungeon of the profile can be entered: the character lacks the level or the key of each.", GameText.Of(DungeonTexts.Text("FailNoneEnterable")));
    }

    private ActivityException? Refusal(CharacterState state, int id, out Dungeon dungeon)
    {
        var dungeons = _tables.Dungeons;

        if (!dungeons.TryGetDungeon(id, out dungeon))
            return Impossible($"Dungeon {id} is not in the client's table.", GameText.Of(DungeonTexts.Text("FailUnknownDungeon")));

        if (_shut.TryGetValue(id, out var shut))
            return shut;

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
            case TravelOutcome.NoPath:
                throw Impossible($"No way leads to the door of dungeon {dungeon.Id}, map {dungeon.EntranceMapId}.", GameText.Of(DungeonTexts.Text("FailNoPath")));
            default:
                if (++_failedJourneys >= MostFailedJourneys)
                    throw Failure($"The journey to the door of dungeon {dungeon.Id} failed {MostFailedJourneys} times in a row.", "FailJourney");

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
                Publish("DoingRooms");

                return true;
            }

            // An NPC who did not answer may still be the keeper: the step is tried again, a few times only.
            if (outcome is NpcTalkOutcome.NoAnswer or NpcTalkOutcome.Busy or NpcTalkOutcome.NotPlaced)
            {
                if (++_unanswered >= MostUnanswered)
                    throw Failure($"No NPC at the door of dungeon {dungeon.Id} answered, {MostUnanswered} times in a row.", "FailNoKeeper");

                return false;
            }
        }

        var shut = Impossible($"No NPC at the door of dungeon {dungeon.Id} let the character in.", GameText.Of(DungeonTexts.Text("FailNoKeeper")));

        // A keeper may turn away one dungeon of several, its keyring's charge spent: the others are still entered (D524).
        _shut[dungeon.Id] = shut;

        return false;
    }

    private void Publish(string doing)
    {
        Volatile.Write(ref _report, new ActivityReport
        {
            Doing = GameText.Of(DungeonTexts.Text(doing)),
            Counts = [new ActivityCount(GameText.Counted(DungeonTexts.Text("CountEnteredOne"), DungeonTexts.Text("CountEnteredMany"), _entered))],
        });
    }
}
