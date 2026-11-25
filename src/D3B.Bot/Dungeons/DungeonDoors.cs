using D3B.Bot.Characters;
using D3B.Bot.Npcs;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Dungeons;

namespace D3B.Bot.Dungeons;

/// <summary>A dungeon's keeper and its exit's NPC talked to, by a character alone or each member of a group by itself (D522, D523, D526, D528).</summary>
/// <remarks>
/// The server places the NPCs with the map, and no table says which is the keeper or the exit's: each NPC shown but a banker
/// is asked in turn, each reply chosen among those its question offers by the ids the pack names from their French words.
/// Measured on the owner's captures (client-game.md): the first room comes 28 to 41 ms after the keeper's confirmation, the
/// exit map after the exit's reply, and in a party each character talked to both itself.
/// </remarks>
internal static class DungeonDoors
{
    /// <summary>Gets whether a character may enter a dungeon: its level, then the keyring when the dungeon is on it, else every object it requires.</summary>
    /// <param name="state">The character.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Whether the keeper would let it in; a level the server did not say yet is let pass.</returns>
    public static bool MayEnter(CharacterState state, DungeonCatalog dungeons, Dungeon dungeon)
    {
        return (state.Level is not { } level || level >= dungeon.MinLevel) && DungeonActivity.CarriesKey(state, dungeons, dungeon);
    }

    /// <summary>Tells whether a map is one of a dungeon's, inside it: a room, or a map of its sub-areas but its entrance and exit, such as the one its boss's win leads to.</summary>
    /// <param name="state">The character, whose map and sub-area are read.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Whether the character is inside the dungeon.</returns>
    public static bool IsInside(CharacterState state, DungeonCatalog dungeons, Dungeon dungeon)
    {
        if (state.MapId is not { } map)
            return false;

        if (dungeons.RoomsOf(dungeon).Contains(map))
            return true;

        return IsFinal(state, dungeons, dungeon);
    }

    /// <summary>Tells whether the character is on a map of a dungeon's sub-areas that is no room, entrance or exit: the map its boss's win leads to.</summary>
    /// <param name="state">The character, whose map and sub-area are read.</param>
    /// <param name="dungeons">The dungeons.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsFinal(CharacterState state, DungeonCatalog dungeons, Dungeon dungeon)
    {
        return state.MapId is { } map
            && state.SubAreaId is { } subArea
            && map != dungeon.EntranceMapId
            && map != dungeon.ExitMapId
            && dungeons.SubAreasOf(dungeon).Contains(subArea)
            && !dungeons.RoomsOf(dungeon).Contains(map);
    }

    /// <summary>Talks to the NPCs of a dungeon's entrance until one lets the character in: the keyring's reply first when it carries it, then the key's, then the confirmation (D523).</summary>
    /// <param name="context">The character's behavior context, on the entrance's map.</param>
    /// <param name="tables">The installed build's tables: the dungeons, and the bankers no keeper is.</param>
    /// <param name="dungeon">The dungeon.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>How it went; <see cref="DoorOutcome.Through"/> once in a room of the dungeon.</returns>
    public static async Task<DoorOutcome> EnterAsync(BotContext context, GameTables tables, Dungeon dungeon, CancellationToken cancellationToken)
    {
        var dungeons = tables.Dungeons;
        var keyring = dungeon.IsOnKeyring && DungeonActivity.Carried(context.State, DungeonCatalog.KeyringItemId) > 0;
        var key = DungeonActivity.CarriesObjects(context.State, dungeons.ObjectsOf(dungeon));

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

        foreach (var npcId in NpcsOf(context.State, tables))
        {
            var outcome = await context.TalkAsync(npcId, Choose, cancellationToken).ConfigureAwait(false);

            if (context.State.MapId != dungeon.EntranceMapId)
                return context.State.MapId is { } arrived && dungeons.RoomsOf(dungeon).Contains(arrived) ? DoorOutcome.Through : DoorOutcome.Elsewhere;

            // An NPC who did not answer may still be the keeper: the others are asked, and the door may be tried again.
            unanswered |= Unanswered(outcome);
        }

        return unanswered ? DoorOutcome.Unanswered : DoorOutcome.Refused;
    }

    /// <summary>Talks to the NPCs of the character's map until one lets it out of the dungeon: the reply the pack's exits name, then a keeper's confirmation (D526).</summary>
    /// <param name="context">The character's behavior context, inside the dungeon.</param>
    /// <param name="tables">The installed build's tables: the dungeons, and the bankers no exit's NPC is.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>How it went; <see cref="DoorOutcome.Through"/> once the map changed.</returns>
    public static async Task<DoorOutcome> ExitAsync(BotContext context, GameTables tables, CancellationToken cancellationToken)
    {
        var dungeons = tables.Dungeons;
        var mapId = context.State.MapId;

        int? Choose(NpcDialog dialog)
        {
            var replies = dialog.Replies;

            if (replies.FirstOrDefault(dungeons.IsExitReply) is > 0 and var exit)
                return exit;

            return replies.FirstOrDefault(dungeons.IsConfirmation) is > 0 and var confirmed ? confirmed : null;
        }

        var unanswered = false;

        foreach (var npcId in NpcsOf(context.State, tables))
        {
            var outcome = await context.TalkAsync(npcId, Choose, cancellationToken).ConfigureAwait(false);

            if (context.State.MapId != mapId)
                return DoorOutcome.Through;

            unanswered |= Unanswered(outcome);
        }

        return unanswered ? DoorOutcome.Unanswered : DoorOutcome.Refused;
    }

    private static List<int> NpcsOf(CharacterState state, GameTables tables)
    {
        return [.. state.Npcs.Values.Select(npc => npc.NpcId).Where(npc => !tables.Banks.TryGetBanker(npc, out _)).Distinct()];
    }

    private static bool Unanswered(NpcTalkOutcome outcome)
    {
        return outcome is NpcTalkOutcome.NoAnswer or NpcTalkOutcome.Busy or NpcTalkOutcome.NotPlaced;
    }
}
