# Fights, the placement weighed by the turn order (D478)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The placement
judged as a turn's end is [D202](d176-d208-step-9.md); the group's fights, [D440](d440-step-10-the-groups-fights.md).

## D478: A placement weighs only the threat of the enemies playing before the character's first turn

*2026-11-20, step 10, the owner's report from a group game: "the AI does not place the characters according to their turn
order"; the owner chose this among three options. Refines D202, D454.*

- **Found**: `FightTactics.Place` judged each free cell as a turn's end, every enemy's threat there against the damage
  the character may deal from there, the turn order left out; the engine did not know it.
- **Measured** on the owner's game of 2026-11-20, 07:46 UTC, four connections (`scripts/capture.cs --messages`,
  structure only): the server sends the turn order (`FightTurnListEvent`) with the placement's start and again as each
  follower joins, before any character of the bot chooses its cell. `FightState.TurnOrder` holds it then.
- **Made**: `Place` takes the server's turn order; the threat is only that of the enemies the order lists before the
  character's first turn, and of those it does not list, the others finding it gone from its cell. A character playing
  first goes where it strikes, one playing after the enemies stays out of their reach. Every enemy's threat, as before,
  when the order does not list the character. `PlaceAsync` passes `FightState.TurnOrder`.
- **Measured after** with `scripts/fights.cs --turns`, which now replays every placement by the character's place in the
  order: on the 24 placements of the 07:46 game, all in a group, the cell changes once; no enemy could reach a placement
  cell before the characters' first turns (threat 0, no life lost before a first turn), so these fights do not show
  what the owner saw. Fights whose placements are within the enemies' first reach will.
- **Rejected**: a fixed rule, the characters sorted by the turn order and the cells nearest the enemies given to the
  first, blind to range and breed (a healer or an archer sent ahead). The group placed together, each follower knowing
  where its allies stand and when they play, left for later: larger, and it meets the race of two followers choosing the
  same cell (D450).
