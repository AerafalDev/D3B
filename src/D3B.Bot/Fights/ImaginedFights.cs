using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Data;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;

namespace D3B.Bot.Fights;

/// <summary>The character's fight as the AI imagines it from what the server said: the fighters, and what the AI knows of them (D201).</summary>
/// <remarks>
/// The fighters come from the fight's state (<see cref="FightCombatants"/>). The character's spells come from the server's
/// list and its level, its worn sword's blow among them (<see cref="CharacterSpells"/>), a monster's and a summon's from the pack at its grade; the
/// character's cooldowns from the server (D193). Every fighter but the character holds the AP and MP it starts its next
/// turn with: a monster's grade's, else what the server last gave it; the character holds what it has left, and starts its
/// turns with what it had as its last began.
/// </remarks>
public static class ImaginedFights
{
    /// <summary>Gets the character's fight as the AI imagines it.</summary>
    /// <param name="character">The character's state, in a fight.</param>
    /// <param name="map">The fight's map.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="blows">The pack's spells and the weapons' blows, which the AI's levels are of.</param>
    /// <param name="turnPoints">The AP and MP the character started its last turn with, when it played one.</param>
    /// <returns>The fight and what the AI knows of it; null out of a fight, or when the server has not shown the character.</returns>
    public static (ImaginedFight Fight, FightKnowledge Knowledge)? Of(CharacterState character, FightMap map, GameTables tables, WeaponBlows blows, (int ActionPoints, int MovementPoints)? turnPoints)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(blows);

        if (character.Fight is not { } fight || !fight.Fighters.TryGetValue(character.Key.CharacterId, out var self))
            return null;

        var spells = ImmutableDictionary.CreateBuilder<long, ImmutableArray<SpellLevel>>();
        var points = ImmutableDictionary.CreateBuilder<long, (int ActionPoints, int MovementPoints)>();
        var fighters = ImmutableArray.CreateBuilder<Combatant>(fight.Fighters.Count);

        foreach (var fighter in fight.Fighters.Values)
        {
            var combatant = FightCombatants.Of(fighter, tables.Monsters);

            if (fighter.Id == self.Id)
            {
                spells[fighter.Id] = CharacterSpells.InFight(character, fight, fighter, blows, tables.Breeds);
                points[fighter.Id] = turnPoints ?? (fighter.ActionPoints, fighter.MovementPoints);
                fighters.Add(combatant);

                continue;
            }

            if (fighter.MonsterId is { } monsterId && fighter.Grade is { } grade && tables.Monsters.TryGetMonster(monsterId, out var monster)
                && tables.Monsters.TryGetGrade(monsterId, grade, out var found))
            {
                var levels = ImmutableArray.CreateBuilder<SpellLevel>();

                foreach (var spell in tables.Monsters.SpellsOf(monster))
                {
                    if (tables.Monsters.SpellGradeOf(spell, grade) is var spellGrade and > 0 && tables.Spells.TryGetLevel(spell.SpellId, spellGrade, out var level))
                        levels.Add(level);
                }

                spells[fighter.Id] = levels.DrainToImmutable();
                combatant = combatant with { ActionPoints = found.Characteristics.ActionPoints, MovementPoints = found.Characteristics.MovementPoints };
            }

            points[fighter.Id] = (combatant.ActionPoints, combatant.MovementPoints);
            fighters.Add(combatant);
        }

        var imagined = new ImaginedFight
        {
            Map = map,
            Fighters = fighters.DrainToImmutable(),
            ActorId = self.Id,
            Round = fight.Round,
            History = self.Casts,
        };
        var knowledge = new FightKnowledge
        {
            Spells = spells.ToImmutable(),
            TurnPoints = points.ToImmutable(),
            Cooldowns = self.Cooldowns.ToImmutableDictionary(),
        };

        return (imagined, knowledge);
    }
}
