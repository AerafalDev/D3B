# Fights, the Pandawa's carrying (D474)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The carry as the
bot's fight state keeps it is [D193](d176-d208-step-9.md#d193-what-the-server-announces-of-a-fight-named-and-kept-mapping-format-10),
the carrier's casts [D191](d176-d208-step-9.md#d191-9c-measured-again-on-38-fights-the-owner-played-for-it).

## D474: The Pandawa's carrying in the AI, a carry and its throw within one turn, as the references model it

*2026-11-20, step 9, row 9f, the owner's ask after a live game and three choices after the lead's study of the pack of
3.7.14.18, the night's capture and the references. Builds D3, D191, D193, D201, D244, D420, D468.*

- **Asked**: the fight AI understands the Pandawa's Karcham (spell 12787), carrying an ally or an enemy then throwing it
  to place it, and the spells that depend on carrying.
- **Found** in the pack: Karcham costs 1 AP at a range of 1 on a taken cell, 4 casts a turn and 1 a target; its criterion
  `HS=3|(HS=3531&HS!534&HS!661&HS!8)` casts it sober and not carried, or carrying. One level holds both halves: a carry
  (action 50, mask `g,A,*e3`), which raises Karcham's own range by 3 to 5 (`281`), and a throw (action 51, mask
  `g,A,*E3`), so that cast again it throws 4 to 6 cells away. Five throw spells ask the carrier's state: 12788, 12793,
  12808, 12811 and 12823, 2 AP, once a turn, 1 to 4 or 5 cells on a free cell; each throws (51, `a,A`), then heals the
  thrown if an ally (`g,K`) or strikes it if an enemy (`A,K`), and the same on a zone around the cell it lands on, with a
  push or a pull. The carrier is in state 3, *Porteur*, which prevents casts but the levels asking it (D191); the carried
  in 8, *Porté*, and 3579, which halves the damage it takes. No action drops a carried fighter.
- **Found** in the night's capture (20261120-003259, structure only): the Pandawa carried an ally once (level 40662), the
  server setting 3 on it and 8 and 3579 on the carried with the carry, not by the level's state effect; it walked, the
  walk accepted; then it stayed idle its next two turns, no throw sent: the AI imagined neither the carry nor the throw,
  and planned nothing it could cast in state 3. No capture of Dofus 3 holds a throw; those of 3.7.12.12 held 4 (D191).
- **Found** in the references (ideas, D3): the carried stands on its carrier's cell and moves with it, a throw needs a
  free walkable cell, a death ends the carry, a blow on the carrier's cell hits the carrier, the mask `K` names the
  carried; Bubble also ends a carry when the carrier is pushed or the carried walks or is teleported. No reference's AI
  carries.
- **Chosen by the owner**, three decisions, each the lead's recommendation:
  1. **A carry and its throw within one turn.** The search imagines carrying then throwing in the same turn, then what
     follows; it never ends a turn carrying, and a turn that begins carrying (by hand, or the server's) throws first.
     What a carried fighter does in its own turn, and whether a carried enemy walks free, are not measured.
  2. **The judge alone chooses**, allies and enemies alike, no weight of its own and no rule naming the breed (D192,
     D244): an ally thrown away from the enemies lowers their threat on it, an enemy thrown beside the allies or into a
     zone opens the next blows, the throw spells' heals and damage count as any other.
  3. **The references' model where nothing is measured**: `K` names the thrown, counted whole; the carried follows its
     carrier; a throw or a death ends the carry; a blow on the carrier's cell hits the carrier alone. The power boosts of
     Porteur's state spell (effect 138) are left out until a capture tells when they apply.
- **Made**: `Carrying` (pure, in `D3B.Engine.Fights`): effects 50 and 51 named (`EffectKind.Carry`, `Throw`), a carry taking
  the fighter it surely reaches off its cell, its carrier's (`Combatant.CarrierId`, which `FightCombatants` now gives from the
  bot's state), the carrier in state 3 and the carried in 8 and 3579, a monster carried only if the pack lets it be.
  `CastRules.Cast`: a level that throws, cast by a carrier, lands on a free cell, never a taken one, its range raised by the
  level's own range modifiers of its spell unless the caster holds one, as D191 measured the server's buffs on 3.7.12.12;
  so Karcham cast again throws 4 to 6 cells away. `Imagination.Cast` lands the carried on the cell before the throw's
  effects, still carried so that `K` reaches it, then ends the carry; `TargetMask` answers `K` as the fighter the caster
  carries. `FightTactics`: a sequence that carries is ranked in the beam but is no end, a turn begun carrying ends so only
  when no other end exists, and a throw is tried on the free cells of its range, half the nearest to the enemies and half
  the farthest, walks before it reaching as the throw's range does. Tests (`CarryTacticsTests`, Karcham and a throw as the
  pack writes them, on the fight built by the test): the carry's cell and states; the throw's cells and range; the throw's
  landing and end; a thrown enemy struck and a thrown ally healed; a turn begun carrying throws; an enemy beside the carrier
  carried and thrown to be struck; no carry ended on; an ally about to fall beside an enemy carried and thrown out of its
  reach; a range modifier the server gave not raised again; a monster the pack does not let be carried left on its cell.
- **Measured** with `scripts/fights.cs --turns` on the 92 game connections of the machine, 1,754 searches, before and after:
  the Pandawa's 115 turns plan 2,566 damage instead of 2,534, 51 kills instead of 48, 67 heals instead of 3 (the throws'
  on an ally thrown), the reply taking 644 instead of 704, 5,511 over two turns instead of 5,391; Karcham planned 27 times
  instead of 7, the throw 12793 6 times, Bombance 22 instead of 13 (sober to carry, D420's remaining swap), the casts
  foreseen to do nothing 3 instead of 8. Every other breed's section is the same line for line. The searches 107 ms halfway
  and 584 ms at most, within 800 (98 and 689 before).
- **Not modelled**: the carried's own turn, a carrier pushed or teleported, a carried fighter walking off, the carrier's
  death mid-turn freeing the carried, the Porteur's power boosts, the casts of 25507 and 25508 in states 4029 and 4030,
  and whether a throw's zone effect of mask `g` or `A` reaches the thrown at its centre beside the `K` one, which D3B's
  zones count twice.
- **Not measured in game**: all of it. To capture: a Pandawa carrying then throwing an ally and an enemy, by Karcham and by
  a throw spell, with the carried ally's own capture; `scripts/fights.cs` then compares the cell, the states and the
  throws' heals and damage.
- **Rejected**:
  - Keeping a carry from one turn to the next, the carried on its carrier's cell, its damage halved, to shelter an ally
    through the reply: it leans on the carried's turn and an enemy's escape, neither measured.
  - Capturing a Pandawa carrying and throwing an ally and an enemy before any code: the bot's carrier would stay idle meanwhile.
  - A worth of the judge for a fighter placed, sheltered or thrown into a zone: a weight to tune, as D468 refused one
    for an open portal.
  - Carrying allies only: a rule of the spell's own.
  - Porteur's power boosts on the throws, and `K` counted half as an unknown mask is: the first a reading not measured,
    the second leaving the throw spells' heals and damage half their worth for a mask the references agree on.
