using System.Collections.Frozen;
using D3B.Bot.Characters;
using D3B.Data.Monsters;

namespace D3B.Bot.Fights;

/// <summary>The archmonsters' souls a character captures, by the soul stone it wears and the spell it casts (D565).</summary>
/// <remarks>
/// Measured in the client 3.7.14.18 (D533): spell 413 "Capture d'âmes", 4 AP on the caster itself, every 3 rounds, castable
/// only while a soul stone is worn, puts the caster in a state that captures the souls of the creatures vanquished as the
/// fight ends; since 3.5, only archmonsters' and bosses' souls. The stone worn must capture the group's highest level.
/// </remarks>
public static class SoulCapture
{
    /// <summary>The spell that captures souls, "Capture d'âmes".</summary>
    public const int SpellId = 413;

    /// <summary>Gets the crafted soul stones, items of type 83, by item: the highest level each captures.</summary>
    /// <remarks>Measured in the client 3.7.14.18 (D533): effect 705 of each, the largest capturing every level.</remarks>
    public static FrozenDictionary<int, int> Stones { get; } = new Dictionary<int, int>
    {
        [9_686] = 50,
        [9_687] = 100,
        [9_688] = 150,
        [9_689] = 190,
        [9_690] = int.MaxValue,
    }.ToFrozenDictionary();

    /// <summary>Gets the smallest stone carried, or worn in the fight consumable slot, that captures a level.</summary>
    /// <param name="inventory">The character's stacks.</param>
    /// <param name="level">The group's highest level.</param>
    /// <returns>The stack of the stone, the one worn on a tie; null when none captures the level.</returns>
    public static InventoryItem? StoneFor(IEnumerable<InventoryItem> inventory, int level)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        // The owner's choice (D565): the smallest that suffices, so that the large stones are kept for the high levels.
        return inventory
            .Where(stack => stack.Quantity > 0 && stack.Position is InventoryItem.NotWorn or InventoryItem.FightConsumable && Stones.TryGetValue(stack.ItemId, out var most) && most >= level)
            .OrderBy(stack => Stones[stack.ItemId])
            .ThenByDescending(stack => stack.Position is InventoryItem.FightConsumable)
            .FirstOrDefault();
    }

    /// <summary>Gets the level a group's soul capture needs: its highest monster's, when it holds an archmonster.</summary>
    /// <param name="group">The group on the map.</param>
    /// <param name="monsters">The pack's monsters, whose flags each has.</param>
    /// <returns>The level, or null when the group holds no archmonster.</returns>
    public static int? LevelToCapture(MapMonsterGroup group, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(monsters);

        return group.Monsters.Any(member => IsArchmonster(member.MonsterId, monsters)) ? group.Monsters.Max(member => member.Level) : null;
    }

    /// <summary>Gets whether an archmonster is among a fight's enemies, alive or dead: the souls of those vanquished are captured as it ends.</summary>
    /// <param name="fight">The fight, as the bot knows it.</param>
    /// <param name="monsters">The pack's monsters, whose flags each has.</param>
    /// <returns>Whether one is.</returns>
    public static bool HoldsArchmonster(FightState fight, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(monsters);

        return fight.Fighters.Values.Any(fighter => fighter.Team != fight.Team && fighter.MonsterId is { } id && IsArchmonster(id, monsters));
    }

    private static bool IsArchmonster(int monsterId, MonsterCatalog monsters)
    {
        return monsters.TryGetMonster(monsterId, out var monster) && monster.Properties.HasFlag(MonsterProperties.IsMiniBoss);
    }
}
