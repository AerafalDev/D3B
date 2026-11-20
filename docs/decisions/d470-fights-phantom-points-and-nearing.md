# Fights, points below 0 and the approach within reach (D470, D471)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The imagined fight is
[D201](d176-d208-step-9.md#d201-the-ai-an-imagined-fight-a-judge-a-beam-search-and-the-enemies-reply); the Pandawa's drink,
[D420](d420-step-9-the-pandawas-drink.md); the AI in a group, [D460](d460-group-fights-strike-now.md).

## D470: A malus of AP or MP takes the fighter's points below 0, as the server keeps them: the Pandawa no longer swaps its states twice for a point it never had

*2026-11-20, step 10, the owner's report from a group run ("le panda utilise Bombance plusieurs fois de suite pour rien"), the
lead's measure on the run's captures. Refines D201, D420.*

- **Measured** on the run's captures (client 3.7.14.18, the first to hold a Pandawa casting Bombance, spell 12804): with no MP
  left, the Pandawa cast Bombance three times in a row, the monsters 12 cells away. Sober, a cast casts *Saoul*'s spell
  (24036), whose malus of 1 MP (effect 169, 2 rounds) the server announced as the fighter's MP going from 0 to -1; drunk, a
  cast dispels it (24037), the MP back to 0.
- **The cause.** `ImaginedFighters.Changed` kept AP and MP at 0 at least, while the malus' end gave back the whole point: two
  casts of Bombance (1 AP, the second giving its AP back) were imagined to leave 1 MP, and the search planned them, then a
  walk and *Schnaps*. The bot plays the first action and plans again from the server's state, which held no such MP, and
  found the same plan: cast after cast. D420's fix, a state held as the search began is no gain, was not in question: the
  swap judged no better than standing (1,207.2 both); the walk it bought was.
- **Made.** A change of AP or MP in the imagination takes the fighter below 0 as the server does. Tests
  (`StateToggleTacticsTests`, a pack-like swap whose drunk state takes 1 MP): the malus takes a caster with no MP to -1 and
  its end to 0; replanning after each cast with no MP left, an enemy a cell beyond the blow's range, no swap is cast.
- **Rejected.** Recording the malus as only what it took from the points: the server's own count is the negative one, and
  the bot reads the server's -1 into its fights as it is.

## D471: Within its spells' range and MP, out of their sight, each cell nearer an enemy still counts: the characters walk instead of passing their turns

*2026-11-20, step 10, the owner's report from the same run ("des persos passent tour alors qu'ils peuvent avancer vers les
monstres"), the lead's measure on the run's captures. Refines D201's approach; builds D210, D460.*

- **Measured** on the run's captures, every turn replayed whose plan was empty with MP to walk: 9, of the Forgelance,
  the Pandawa and the Sacrieur, each beside 3 to 5 allies with the nearest enemy 7 to 13 cells away. Once the enemies
  replied, each walk weighed exactly what ending the turn now did, and ending the turn now wins a tie. Where no spell lands
  from any cell the fighter could stand on next turn, the approach is the next turn's damage times `JudgeTurns.Nearing`,
  half at most, decreasing by 15% a cell beyond the spells' range and the MP: within them, behind walls or out of sight,
  every cell was worth that half, and walking nearer nothing.
- **Made.** Within the range and the MP, each cell nearer adds its share of a twentieth: `0.5 + 0.05 x (reach + 1 -
  distance) / (reach + 1)`, half at the edge, 0.55 on the enemy's cell, a slope whatever the reach; beyond them, nothing
  changes. Tests: the share falls with each cell from the enemy for reaches of 4, 9 and 13, is half just beyond the reach
  and at most 0.55 (`JudgeTurnsTests`); an enemy 8 cells away walled in by pillars, in the range and MP of a spell that
  needs its sight, is walked towards (`FightTacticsTests`). With a first slope of a hundredth a cell, the run's empty plans
  with MP to walk fell from 9 to 4; the 4 left weighed no walk better even before the reply, the enemy walking to the
  fighter in the reply, or a spell landing next turn from wherever it stood. The review found that slope flat again 10 cells
  within a long reach, hence the share of the reach.
- **Known.** The half already weighs every damaging spell where a landing weighs only those that land, so that a cell from
  which nothing lands may outweigh one from which only a weak spell does; the twentieth adds a little to that, measured on
  the replay below.
- **Replayed** with D470 on the 92 game captures of this machine, 1,282 turns (`scripts/fights.cs --turns`, main's
  counts in brackets): plans of casts 1,355 (1,357), of walks only 242 (184), of nothing 157 (213); the Forgelance's empty
  plans 12 (32), the Pandawa's 10 (18). The damage foreseen 24,219 (24,246) and over two turns 54,628 (54,720), the kills 592
  (590), the life the reply takes 4,732 (4,733); damage more than the turn played in 245 turns (243), less in 127 (125).
  Bombance planned 7 times (13), the Pandawa's swaps between two casts 4 (11).
- **Not measured in game.** The owner is to say whether the characters still pass their turns, and the Pandawa still casts
  Bombance for nothing.
- **Rejected.**
  - Ending a tie on the walk nearest the enemies: it sends walks that change nothing the judge sees, which ending the turn now
    winning a tie avoids; the gap was the approach's, not the tie's.
  - The reply's blows foreseen 2 to 4 times those dealt in a group, a lead of the first study: the walks weighed as standing
    did before and after the reply alike, so it was not the cause here; 10d measures it.
  - A slope of a hundredth a cell up to 0.55: flat again 10 cells within a long reach, as the review measured.
  - A larger share within the reach: it would weigh a cell from which nothing lands more against one from which a weak
    spell lands.
