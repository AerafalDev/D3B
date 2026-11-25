using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Data.Monsters;

namespace D3B.Bot.Fights;

/// <summary>The wild mounts a character captures, by the taming spell its capture net gives (D533).</summary>
/// <remarks>
/// Measured on the client 3.7.14.18 (D533): a capture net worn in the fight consumable slot gives a taming spell, 32260 for the
/// universal net, 32270 to 32278 for the species' reinforced and multiplying ones; cast once a fight on a wild mount, it puts
/// the mount in state 7109 "Monture capturée", captured as the fight ends. The wild mounts are the monsters of races 21, 155
/// and 183.
/// </remarks>
public static class MountCapture
{
    /// <summary>The race of the wild Dragodindes.</summary>
    public const int Dragodindes = 21;

    /// <summary>The race of the wild Muldos.</summary>
    public const int Muldos = 155;

    /// <summary>The race of the wild Volkornes.</summary>
    public const int Volkornes = 183;

    /// <summary>The profession whose level the species' nets need, Éleveur.</summary>
    public const int Breeder = 79;

    /// <summary>The state of a mount tamed, "Monture capturée".</summary>
    public const int Tamed = 7109;

    /// <summary>Gets the capture nets, items of type 99, by item: the race each tames, none for the universal net, and the level of Éleveur its use needs.</summary>
    /// <remarks>Measured in the client 3.7.14.18 (D533): <c>PJ&gt;79,99</c> on the multiplying nets, 149 on the reinforced, 199 on both.</remarks>
    public static FrozenDictionary<int, (int? Race, int BreederLevel)> Nets { get; } = new Dictionary<int, (int? Race, int BreederLevel)>
    {
        [32_521] = (null, 0),
        [32_522] = (Dragodindes, 150),
        [32_523] = (Volkornes, 150),
        [32_524] = (Muldos, 150),
        [32_525] = (Dragodindes, 100),
        [32_526] = (Volkornes, 100),
        [32_527] = (Muldos, 100),
        [32_528] = (Dragodindes, 200),
        [32_529] = (Volkornes, 200),
        [32_530] = (Muldos, 200),
    }.ToFrozenDictionary();

    /// <summary>Gets the taming spells the capture nets give.</summary>
    public static FrozenSet<int> TamingSpells { get; } = new[] { 32260, 32270, 32271, 32272, 32273, 32274, 32275, 32276, 32277, 32278 }.ToFrozenSet();

    /// <summary>Gets the wild mounts of a fight the character may still tame: its enemies alive of the races captured, none tamed yet.</summary>
    /// <param name="fight">The fight, as the bot knows it.</param>
    /// <param name="monsters">The pack's monsters, whose race each has.</param>
    /// <param name="races">The races captured.</param>
    /// <returns>The mounts, by fighter.</returns>
    public static ImmutableArray<long> Targets(FightState fight, MonsterCatalog monsters, IReadOnlyList<int> races)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(races);

        if (races.Count is 0)
            return [];

        return [.. fight.Fighters.Values
            .Where(fighter => fighter.IsAlive && fighter.Team != fight.Team && fighter.MonsterId is { } id
                && monsters.TryGetMonster(id, out var monster) && races.Contains(monster.Race) && !fighter.States.Contains(Tamed))
            .Select(fighter => fighter.Id)];
    }
    /// <summary>Gets the best net carried, or worn in the fight consumable slot, for a group's mounts: the one the breeder's level allows with the highest level needed, the universal last.</summary>
    /// <param name="inventory">The character's stacks.</param>
    /// <param name="breeder">The character's level of Éleveur, 0 without the profession.</param>
    /// <param name="races">The races of the group's mounts the profile captures.</param>
    /// <returns>The stack of the net, the one worn on a tie; null when none fits.</returns>
    public static InventoryItem? NetFor(IEnumerable<InventoryItem> inventory, int breeder, IReadOnlyCollection<int> races)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(races);

        return inventory
            .Where(stack => stack.Quantity > 0 && stack.Position is InventoryItem.NotWorn or InventoryItem.FightConsumable && Nets.TryGetValue(stack.ItemId, out var net)
                && breeder >= net.BreederLevel && (net.Race is not { } race ? races.Count > 0 : races.Contains(race)))
            .OrderByDescending(stack => Nets[stack.ItemId].BreederLevel)
            .ThenByDescending(stack => stack.Position is InventoryItem.FightConsumable)
            .FirstOrDefault();
    }

    /// <summary>Gets the races a net worn tames, of those the profile captures: its species alone, or all for the universal net or none known.</summary>
    /// <param name="net">The item worn in the fight consumable slot, or null.</param>
    /// <param name="races">The races the profile captures.</param>
    /// <returns>The races.</returns>
    public static IReadOnlyList<int> RacesFor(int? net, IReadOnlyList<int> races)
    {
        ArgumentNullException.ThrowIfNull(races);

        // Measured in the client (D533): a species' net's spell is cast only while the fight holds a monster of its species.
        return net is { } item && Nets.TryGetValue(item, out var worn) && worn.Race is { } race ? [.. races.Where(captured => captured == race)] : races;
    }

    /// <summary>Gets the races of a group's monsters the profile captures.</summary>
    /// <param name="group">The group on the map.</param>
    /// <param name="monsters">The pack's monsters, whose race each has.</param>
    /// <param name="races">The races the profile captures.</param>
    /// <returns>The races, each once.</returns>
    public static IReadOnlyList<int> RacesIn(MapMonsterGroup group, MonsterCatalog monsters, IReadOnlyList<int> races)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(races);

        return [.. group.Monsters
            .Select(member => monsters.TryGetMonster(member.MonsterId, out var monster) ? monster.Race : -1)
            .Where(races.Contains)
            .Distinct()];
    }
}
