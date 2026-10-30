using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Data.Breeds;
using D3B.Data.Spells;

namespace D3B.Bot.Fights;

/// <summary>The spells' levels a character may cast: those the server listed, and those its level unlocks since (D199).</summary>
/// <remarks>
/// Measured on the 52 game connections captured: the server lists the character's spells once, as it enters the game, each
/// with its grade; a level gained later unlocks its breed's next spells and raises grades, which the server announces in no
/// message the readable protocol names. Of the 231 casts the characters chose, 213 were of a spell the list held at the
/// grade cast, and 18 of a spell a level gained during the capture unlocked, the base of its pair at the grade its least
/// level allows: 231 of 231 as this rule has it. At each character's first fight, the rule added no spell to the list.
/// The list holds spell 0 too, a weapon's blow, whose costs and effects are the weapon's: it is left out until the AI knows
/// the character's weapon.
/// </remarks>
public static class CharacterSpells
{
    private const int WeaponBlow = 0;

    /// <summary>Gets the spells' levels a character may cast.</summary>
    /// <param name="listed">The spells the server listed, each with its grade, by spell.</param>
    /// <param name="breedId">The character's breed, when known.</param>
    /// <param name="level">The character's level now.</param>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="breeds">The pack's breeds.</param>
    /// <returns>One level of each spell, the highest the character casts.</returns>
    public static ImmutableArray<SpellLevel> Of(IReadOnlyDictionary<int, int> listed, int? breedId, int level, SpellCatalog spells, BreedCatalog breeds)
    {
        ArgumentNullException.ThrowIfNull(listed);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(breeds);

        var grades = new SortedDictionary<int, int>();

        foreach (var (spellId, grade) in listed)
        {
            if (spellId is not WeaponBlow)
                grades[spellId] = Math.Max(grade, GradeAt(spells, spellId, level));
        }

        if (breedId is { } breed && breeds.TryGetBreed(breed, out var found))
        {
            foreach (var spellId in breeds.SpellsOf(found))
            {
                // A pair's spells share a place: the one the list holds, else the pair's first.
                if (spells.TryGetVariantOf(spellId, out var variant) && (variant.SpellId != spellId || listed.ContainsKey(variant.VariantSpellId)))
                    continue;

                if (!grades.ContainsKey(spellId) && GradeAt(spells, spellId, level) is var grade and > 0)
                    grades[spellId] = grade;
            }
        }

        var levels = ImmutableArray.CreateBuilder<SpellLevel>(grades.Count);

        foreach (var (spellId, grade) in grades)
        {
            if (spells.TryGetLevel(spellId, grade, out var spellLevel))
                levels.Add(spellLevel);
        }

        return levels.DrainToImmutable();
    }

    /// <summary>Gets the spells' levels the character may cast in its fight.</summary>
    /// <param name="character">The character's state.</param>
    /// <param name="fight">Its fight.</param>
    /// <param name="self">Its own fighter.</param>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="breeds">The pack's breeds.</param>
    /// <returns>The fight's context's spells when the server listed them; else a transformed character's form's; else its own.</returns>
    /// <remarks>Measured live (D203): transformed into a bouftou by an alteration, the character's own spells went unanswered.</remarks>
    public static ImmutableArray<SpellLevel> InFight(CharacterState character, FightState fight, Fighter self, SpellCatalog spells, BreedCatalog breeds)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(self);

        if (!fight.Spells.IsEmpty)
            return Of(fight.Spells, null, 0, spells, breeds);

        if (!character.TransformedSpells.IsEmpty)
            return Of(character.TransformedSpells, null, 0, spells, breeds);

        return Of(character.Spells, self.BreedId, self.Level ?? character.Level ?? 1, spells, breeds);
    }

    private static int GradeAt(SpellCatalog spells, int spellId, int level)
    {
        if (!spells.TryGetSpell(spellId, out var spell))
            return 0;

        var grade = 0;

        foreach (var levelId in spells.LevelIdsOf(spell))
        {
            if (spells.TryGetLevel(levelId, out var found) && found.MinPlayerLevel <= level)
                grade = Math.Max(grade, found.Grade);
        }

        return grade;
    }
}
