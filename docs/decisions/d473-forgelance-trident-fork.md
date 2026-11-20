# Fights, the Forgelance's Trident and the fork (D473)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The zones and masks
are [D187](d176-d208-step-9.md#d187-zones-and-target-masks), the lance in hand [D465](d465-forgelance-lance-state.md).

## D473: A fork reaches a row past its size, each row a step of its decrease

*2026-11-20, the owner's report of a Forgelance throwing Tridents into the void, the lead's finding of 4 losses of life
the engine did not foresee in that capture, the owner's choice of the fork alone after the measure. Builds D3, D181, D187,
D195, D465.*

- **Reported**: in a fight of 2026-11-20 the Forgelance cast Trident de la Mer (23393) on empty cells, twice from the
  caster's column three cells ahead, and a monster two cells past each lost life; the engine had foreseen no damage, and
  `scripts/fights.cs --details` counted those two and two of Effondrement (23736) as damage not foreseen.
- **Measured** with `scripts/fights.cs` and a copy of its bench on the 92 game captures of 3.7.14.18, every cast replayed in
  each character's capture: Trident's damage, its state 3743 and its pull's states share a fork of 1. Of the enemies within
  4 cells of the cell cast on, along and across, the 314 on the cell, on the first row ahead (the middle and one cell each
  side) and on the second (the middle and two cells each side) lost life, none of the 324 elsewhere. The engine drew the
  first row only: the 21 losses of Trident it did not foresee were all on the second row. Drawn with it, the second row's
  losses came out under the range foreseen one step away, 72 times: the decrease counts the rows, the first one step and
  the second two. The pull Trident casts (24739) comes after its damage, and the order of its effects explains nothing.
- **Effondrement** is no fault of the engine: its damage of mask `A,F50000` strikes monster 50000 only, as the mask reads,
  and the blow comes from the spell 30711 it casts on the cell while the lance is in hand. The AI's imagination foresees
  every one of the 392 losses the bench counted as not foreseen, the bench comparing only the level's own effects.
- **Made**: `SpellZones` draws a fork of size n as the cell and n + 1 rows ahead, row r its middle and the cells r away on
  each side; `SpellEffects` counts a fork's row as its steps away. `scripts/fights.cs` counts a loss the level's effects do
  not foresee and the whole cast imagined does, through a spell it cast, as "dealt, imagined through a spell it cast". Two
  tests: a fork of 1 covers its seven cells, a fork's rows lower its damage a step each.
- **Replayed**, before and after: the bench's losses with the casts they triggered, 413 dealt and not foreseen, are now 0
  (21 within, 392 imagined through a spell cast), the 13 below as before, no Trident under or over its range. With
  `--turns`, 1,282 turns, only the Forgelance moves, every other breed's section the same line for line: its 250 turns plan
  6,201 damage instead of 6,182, 174 kills instead of 172, the reply as before (1,226); its played turns foresee 5,078
  instead of 5,020, against 4,988 dealt; Trident is planned 42 times instead of 32 (53 played). The searches 117 ms halfway
  and 502 at most (103 and 623 before), those keeping a challenge 145 and 862 (126 and 814 before), over the budget of 800
  at most as before.
- **Left for sessions of their own**, the owner's choice: a pull towards a cell stops beside it in `Imagination`, where the
  server takes the target onto it and on past it, Trident's pull 24739 among them (728 pulls went beyond the cell
  imagined, every breed's); and casts the AI plans that strike nothing, 18 of the 42 Tridents planned, half of them doing
  nothing but spend AP, the others pulling an ally.
- **Rejected**: correcting the pull in this session, a change of every breed's pulls that needs its own measure; a fork
  of size n reaching n rows, as the references' damage preview draws it, which the captures refute.
- **Not measured**: a fork of 2 or 3, which monsters' spells and none of the captures' casts hold, and one cast along a
  diagonal, its direction kept as the cast's axis; to see in game, a Forgelance's Trident striking two cells past the cell.
