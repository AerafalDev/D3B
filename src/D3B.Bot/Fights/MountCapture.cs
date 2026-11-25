using System.Collections.Frozen;
using System.Collections.Immutable;
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

    /// <summary>The state of a mount tamed, "Monture capturée".</summary>
    public const int Tamed = 7109;

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
}
