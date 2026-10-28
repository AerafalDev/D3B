using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Characters;

/// <summary>What a bot knows of its character, at one moment: an immutable snapshot, which only the bot's own loop replaces (D9).</summary>
/// <remarks>
/// <para>
/// The game server tells it all: the character choice names the character, then the server sends its inventory, weight
/// and professions, the map it is on and what stands there, and every change after. <see cref="CharacterEvents"/> turns
/// each message into the next snapshot; logic code reads snapshots, never the messages.
/// </para>
/// <para>
/// What the server has not said yet is <see langword="null"/> or empty: the map and cell until the map is named and its
/// information sent, the inventory until its content is sent.
/// </para>
/// </remarks>
public sealed record CharacterState
{
    /// <summary>Gets who the bot plays.</summary>
    public required CharacterKey Key { get; init; }

    /// <summary>Gets the character's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the character's level, when the character choice gave it.</summary>
    public int? Level { get; init; }

    /// <summary>Gets the map the character is on, once the server named it.</summary>
    public long? MapId { get; init; }

    /// <summary>Gets the sub-area of the map, once the map's information was sent.</summary>
    public int? SubAreaId { get; init; }

    /// <summary>Gets the cell the character stands on, or the one its walk started from while it walks.</summary>
    /// <remarks>Known once the map's information placed the character, then after each walk, refusal or stop.</remarks>
    public int? Cell { get; init; }

    /// <summary>Gets the path the character walks, every cell from the first, as the server showed it; empty when it stands still.</summary>
    public ImmutableArray<int> Path { get; init; } = [];

    /// <summary>Gets whether the character walks: the server showed it a path, and has not answered the client's confirmation yet.</summary>
    public bool IsWalking => !Path.IsDefaultOrEmpty;

    /// <summary>Gets the stacks the character carries, by their own id.</summary>
    public ImmutableSortedDictionary<int, InventoryItem> Inventory { get; init; } = ImmutableSortedDictionary<int, InventoryItem>.Empty;

    /// <summary>Gets the kamas the character carries.</summary>
    public long Kamas { get; init; }

    /// <summary>Gets the weight the character carries, in pods.</summary>
    public int Weight { get; init; }

    /// <summary>Gets the most the character can carry, in pods.</summary>
    public int MaxWeight { get; init; }

    /// <summary>Gets the character's professions, by id.</summary>
    public ImmutableSortedDictionary<int, CharacterJob> Jobs { get; init; } = ImmutableSortedDictionary<int, CharacterJob>.Empty;

    /// <summary>Gets the state of a character the client just chose, as the server's confirmation describes it.</summary>
    /// <param name="key">Who the bot plays.</param>
    /// <param name="character">The character, as the server's confirmation gives it.</param>
    /// <returns>The state, nothing known yet but the character's name and level.</returns>
    public static CharacterState Chosen(CharacterKey key, Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var basics = character.Basics;

        return new CharacterState
        {
            Key = key,
            Name = basics?.Name ?? "",
            Level = basics is { HasLevel: true } ? basics.Level : null,
        };
    }
}
