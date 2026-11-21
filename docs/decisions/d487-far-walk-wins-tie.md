# Fights, a walk nearer far enemies after the reply (D487)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The approach within
reach is [D472](d470-fights-phantom-points-and-nearing.md#d472-within-its-spells-range-and-mp-out-of-their-sight-each-cell-nearer-an-enemy-still-counts-the-characters-walk-instead-of-passing-their-turns);
the next turn searched after the reply, [D210](d209-d252-step-9.md#d210-positions-over-two-turns-the-next-turn-searched).

## D487: A walk nearer the enemies that the judge weighs better before the reply wins a tie within a point after it

*2026-11-21, step 10, the audit of the captures of 20 and 21 November (D480: 238 idle turns of the AI's, 179 of them far
from the enemies with no spell landing and MP left), the lead's task. Refines D472, D210.*

- **Measured** with `scripts/fights.cs --turns` on the 130 game captures of 20 and 21 November, 6,401 turns: 243 plans of
  nothing, 182 of them with MP left, the nearest enemy 5 cells away or more and no spell landing from the character's
  cell. Of these, 75 had a walk the judge weighed better before the reply that did no better after it, 42 a walk better
  after the reply the search never weighed there (only the 3 best walks before the reply are), 55 no walk better than
  standing even before the reply (in 32, the walk nearer as good, a spell landing next turn from either cell), 10 no
  cell to walk to.
- **The cause.** After the reply, the approach counts only where next turn's search gains nothing (D210); in 57 of the 75
  it gained as much from either cell, a heal or an enemy reached next turn from both, so D472's slope was left out and the
  walk tied ending the turn now, which wins a tie.
- **Made.** When ending the turn now is the best plan, a plan of a single walk ending nearer the nearest enemy, weighed
  better than standing before the reply, wins if it is within a point (`Tie`) of standing after it, the best of them, and is
  never swapped for a plan of as few actions that ending the turn now beat (the review's finding, made after the replay
  below, which it can only change where such a plan took the walk's place). A character on no cell, carried, measures nothing. Tests (`FightTacticsTests`): a hurt character with 1 AP, its enemy 12
  cells away, healing next turn from either cell, walks nearer; a character on no cell ends its turn. `--turns` counts each
  plan of nothing with MP by the buckets of D480's live idle turns, and the far ones by these stages.
- **Replayed** on the same captures, main's counts before the fix in brackets: plans of nothing 190 (243), walks only 539
  (481), casts 6,403 (6,408); the far plans of nothing with no spell landing 132 (182), those of a walk better before the
  reply only 34 (75), missed by the search 33 (42). By breed, plans of nothing: Forgelance 59 (67), Pandawa 34 (57), Zobal
  23 (30), Enutrof 19 (28), Eliotrope 28 (29), Sacrieur 14 (18). The damage foreseen 299,753 (299,792), over two turns
  712,631 (712,651), the kills 1,877 (1,876), the life the reply takes 59,917 (59,869), the Pandawa's alone changing
  (8,987 against 8,939); damage against the turn played more 920, less 737, as before.
- **Left.** The 42, now 33, that a walk outside the 3 best before the reply would win; the 55 where no walk beats standing
  before the reply. Of the 45 live idle turns with a spell landing, the replay keeps 43 plans of nothing with a spell
  landing (46 before): the Forgelance's thrown lance (D465), which the bucket's landing test does not read, may explain
  some; the blows on allies (D485) make the AI cast less, not more, and were not measured apart.
- **Rejected.**
  - The approach added to next turn's searched gain after the reply: it would reweigh every turn whose next turn gains,
    where only the ties between ending now and walking were at fault.
  - More walks weighed after the reply (`RootEnds` above 3): it would take the 33 too, but each costs a reply and a next
    turn searched in every search; left for a session that measures its time.
  - Ending a tie on the walk nearest the enemies whatever the judge weighed before the reply (D472's rejection): only a walk
    the approach weighed better wins here, never one that changes nothing the judge sees.
