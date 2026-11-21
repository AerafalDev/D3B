# Fights, the Huppermage's runes, the Sram's traps and the Féca's glyphs triggered (D493)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The marks on cells
are [D280](d253-d292-step-9.md#d280-glyphs-walls-and-the-marks-as-a-turn-begins-and-ends), weighed over their rounds in
[D288](d253-d292-step-9.md#d288-walls-and-glyphs-weighed-over-the-rounds-they-strike); the portals,
[D468](d468-eliotrope-portals.md).

## D493: A Huppermage's runes set and triggered, a trap set off by whoever comes onto it, and a Féca's glyphs triggered

*2026-11-21, the owner's ask to handle the Huppermage and its runes, then the Féca's glyphs and the Sram's traps, read on
the Bubble emulator; the lead's plan, the owner's choice of all three. Builds D3, D227, D280, D288, D468, D489.*

- **Found in the pack of 3.7.14.18** (breed 17): each elemental spell of the Huppermage casts, by effect 1160 on the
  fighter it strikes, a rune spell (13685 Air, 13686 Earth, 13687 Fire, 13673 Water) whose own effect 2022 sets a rune of
  2 rounds on its cell, casting 13689, 13690, 13665 or 13688; the elemental spell's own 2022 is for the client alone. A
  rune strikes only as its author triggers it, by effect 2023: Runification casts 13691, which triggers the runes under
  the fighters and on the cell cast on, the caster in the states Rune (296) and its rank (6969 to 6971) that the rune's
  damage asks; Surcharge Runique triggers them over the whole map. The rune spells and the elemental ones also cast the
  elemental states (290 to 293, 32391 to 32394) and their combination (13671, then 13702 to 13707 for a fighter in two),
  which the engine already foresaw through 1160 and the masks' states. The Sram's traps (breed 4) are effect 400 of no
  duration; the Féca's glyphs (breed 1) are 401, 402 and 1091, already imagined, and Transhumance triggers them by 1026,
  its value the spell that set them.
- **Read in the Bubble emulator** for ideas only (D3): a rune taking the place of one on its cell; 2023 of a point
  triggering the caster's runes covering the cell, of a larger zone those whose middle it covers, each casting its spell
  on its middle, then deleted; a trap lasting until a fighter's move, slide or teleport ends on its cells, its spell cast
  by its author on its middle, then deleted, a slide stopping on it; a walk cut on a trap; a glyph triggered casting its
  spell on its middle and staying.
- **Measured** on the owner's 217 game captures of 3.7.14.18, by a throwaway probe that counts the server's marks: the
  bot's Huppermage played 70 turns, casting its elemental spells and never Runification, so that no rune struck. The
  server set 376 runes, each a mark of type 4 by action 2022, the spell that set it the rune spell (13686 Earth 287 times,
  13687 Fire 36, 13673 Water 41, 13685 Air 12), casting the spell the pack names, on one cell, ending two rounds after it
  came; 20 came on a cell a rune stood on, 16 of those with the old one taken back (310) as the new came; 9 others went by
  310 from their author. `ImaginedFights.MarksOf` knows a rune by the spell that set it, as it knows a glyph. The bot's
  Féca played 274 turns and cast none of its glyphs; no Sram of the bot's is captured, and the 28 traps D280 counted were
  monsters'.
- **Made**: `CellMarkKind.Rune` and `Glyphs.Rune` (2022), a trap kept the whole fight; `EffectKind.TriggerRunes` (2023)
  and `TriggerGlyphs` (1026), foreseen once on their caster as their mask's conditions on the caster allow; `Imagination`
  carries the marks through a cast's effects, so that a spell an effect casts sets its marks and a trigger acts in its
  level's order, the states before it held; a trap set off by a fighter a cast or a walk brings onto its cells, again for
  one its spell moves onto another; a slide stopped on a trap's first cell, with no push damage; `FightMoves.Reachable`
  and `Path` stopping a walk on the first trap cell it steps on, for the fighter playing and the enemies of the reply.
  Fifteen tests on spells built as the pack's: a rune set by the spell cast, striking no one until triggered, triggered
  on its cell or over the map, its author's alone, replaced; the elements combined; the server's rune known; a trap
  kept, set off by a walk and by a push that stops on it, a walk stopping on it, none set off by a trap set around a
  fighter; a Féca's glyphs triggered on their cell and kept.
- **Measured with `scripts/fights.cs --turns`** on the 217 captures, against `main` at the session's start, each run on
  its own tree: the Huppermage's 70 turns plan Runification 7 times instead of none, its damage foreseen 2,517 then 2,573,
  its heals 18 then 47, the rune healing its allies, its kills over two turns 125 then 126, the life the reply takes 262
  then 337; runes set by its plans 76, by its turns played 49. The Féca's 274 turns: damage over two turns 20,666 then
  20,710, kills 230 then 231, plans of nothing 36 then 35. No other breed moved; searches 177 ms halfway as before.
- **Left**: a rune or a trap left on the ground weighed for the rounds to come, as D288 weighs a glyph: the judge counts a
  rune only once a plan triggers it in the same turn, and a trap only as the reply walks onto it; the enemies' traps,
  which the server does not show; a glyph-aura's buffs taken back as a fighter leaves it; a rune triggered, a trap and
  Transhumance in game.
- **Rejected**: modelling the rune from the elemental spell's own 2022, which is for the client alone and would set a
  rune the server does not; triggering a trap as a walk passes over it without stopping there, which the references do
  not do.
