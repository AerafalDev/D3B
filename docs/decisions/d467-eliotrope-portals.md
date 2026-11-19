# Fights, an Eliotrope's portals (D467)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The marks on the
cells are [D280](d253-d292-step-9.md#d280-glyphs-walls-and-the-marks-as-a-turn-begins-and-ends), the masks' states in the
search [D255](d253-d292-step-9.md#d255-a-change-of-the-fighters-states-kept-in-the-turns-search).

## D467: An Eliotrope's portals in the AI, cast through by every character of the bot

*2026-11-19, the owner's ask and choice (A of three) after the lead's study of the portals in BubbleEmu, the pack of
3.7.14.18 and the captures. Builds D3, D201, D244, D255, D280.*

- **Asked**: the fight AI handles the Eliotrope's portals, as BubbleEmu does it (read for ideas, D3); and "the allies may
  strike through the portals too when it is worth it, but never as an obligation".
- **Found**: the 137 game connections on the machine (83 of 3.7.14.18 of 2026-11-18 and 19, 54 of 3.7.13.17, one schema)
  hold 24 portals set and no cast through one: the 4 that client-game.md counted on 3.7.12.12 were in captures no longer
  kept. Everything below is the references' model, Dofus 2's, not yet measured on Dofus 3. On the Eliotrope's 262 turns
  replayed, its other spells were foreseen right, but Portail was planned 7 times for nothing, and 93 effects of its spells
  ask a cast through a portal (`R`) or through none (`r`), which the mask answered unknown, each counted half: a push of
  mask `r`, Affront's, never moved anyone.
- **Made**: `Portals` (pure, in `D3B.Engine.Fights`): a mark of effect 1181 on one cell, the whole fight, four a team, a
  fifth taking the oldest's place; open while no fighter stands on it and its team has another open; a cast on an open
  portal goes along its chain, each portal to the nearest of its team's left, and lands as far from the last one as the
  caster stands from the first, its effects raised by 2% a cell between the portals (the effect's first dice). Any fighter
  goes through. A portal the server set, which names no end, stays the whole fight as the search's do. `CastRules` checks the
  portal from the caster, then the cell it lands on from the exit, and refuses a level needing a cell with no portal on the
  portal cast on as on the cell it would land on, so that Portail, which may go through portals, sets none through them; `Imagination` casts there, its zone and pushes from the exit; `TargetMask` answers
  `R` and `r` on every cast. `FightTactics` tries every level on the portals' cells, a level that sets a portal on the cells
  that line a way up with an enemy, and keeps beside the best sequences the best of other sets of the team's portals, those
  whose ways land on an enemy first, as D255 keeps a mask. The bot sends the portal's cell, as the client does.
- **Read**: the cast's portals, `FightCastSpell.portal_ids`, the third field of the cast's spell in the client's schema,
  mapping format 23; `scripts/fights.cs` says, for each cast through portals, the cell and the exit foreseen against those
  the server named, and imagines it on the portal cast on. No capture holds one yet.
- **Measured** with `scripts/fights.cs --turns` on the 83 captures of 3.7.14.18, 1,181 turns, before and after: the
  Eliotrope's 262 turns plan 5,509 damage instead of 4,789, 144 kills instead of 127, the reply taking 1,185 instead of
  1,281, 11,883 over two turns instead of 10,088, and 39 plans doing nothing instead of 53; Portail planned 28 times,
  Affront 122 instead of 25, its push now imagined. Every other breed's section is the same line for line. The bench: the
  life a cast took imagined 206,959 against 205,854 before, for 207,016 announced; 777 pushes on the cell imagined against
  609; Commotion's pull of mask `r`, now imagined, stops beside the cell cast on where the server takes the target onto it,
  712 times (a pull towards a zone's middle, left as it was). The searches 94 to 113 ms halfway and 589 ms at most over two runs, within 800 (93 and 577 before).
- **Not modelled**: a walk onto a portal (1182) and the portals it closes until the next turn, the closing effect 1183, a tie
  between two portals as near (by an angle in the references, by the older here), the enemies' casts through portals in the
  reply, Coalition's spell triggered by a portal (`PT`). The bonus is the client's text, 2% a cell; the references add the
  dice's 2 once more, which a capture will tell.
- **Rejected**:
  - B, capturing an Eliotrope's portals by hand before any code: the owner chose A, the capture measuring it after.
  - C, casting through the portals already set and setting none: the bot's Eliotrope would set none.
  - Keeping in the beam the best sequence of every exact set of portals: 56 sets after a second portal, the frontier past
    the search's budget.
  - A worth of the judge for an open way: a weight to tune, where the beam's extras and the next turn's search find the casts.
- **Not measured in game**: all of it. To capture: an Eliotrope casting through two, then three portals at several
  distances, an ally casting through them, and an enemy if one does; the mapping of format 23 ships with the release
  `client-3.7.14.18` on the owner's go, before an app that reads it.
