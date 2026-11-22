# Fights, the Forgelance's lance left on the map (D498)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The lances searched
first are [D462](d462-forgelance-lances.md), the lance out of hand
[D465](d465-forgelance-lance-state.md#d465-a-thrown-lance-is-out-of-the-forgelances-hand-until-its-next-turn-a-measured-rule-the-pack-does-not-hold),
the spells cast on it [D489](d489-forgelance-lance-reach.md#d489-the-lance-gives-each-spell-it-names-its-reach-and-a-summon-on-a-free-cell-asks-its-casters-conditions).

## D498: The Forgelance's lance stays on the map until it dies or a cast kills it, then is back in its hand: the Forgelance no longer waits for a lance that never comes back

*2026-11-22, the owner's report from game: the Forgelance throws its lance too often, then strikes earth around it while it is a
fire character, and passes turns without walking to the monsters. Supersedes D465's return as the thrower's turn begins.
Builds D244, D462, D489.*

- **Measured** with a throwaway copy of `scripts/fights.cs` under `artifacts/`, on every game capture of the machine (18
  to 22 November, client 3.7.14.18): of the 221 Forgelance turns that ended with its lance on the map, 199 began the next
  with the lance still there and the Forgelance holding 3361, not 3360; the 22 others followed the lance's death in the
  enemies' turns. A cast that kills the lance, a spell whose effect casts 30712 on it with the lance out (23824 and 23726 in
  the captures), left the Forgelance holding 3360 and 3590 at once; and of the 17 throws a Forgelance holding 3589 made with its lance down, no cast killing
  it before in the turn, in three captures, all 17 ended the turn with one lance, the new one: a second throw takes the
  first off. D465's measure, 304 of 308 turns beginning with the lance
  in hand, counted every turn, most of which had not ended with the lance down: it never said the lance comes back.
- **The fault.** `Imagination.TurnStarted` and the judge's next turn gave the lance back as its thrower's turn began. The
  judge saw a throw cost nothing next turn, the spells asking the lance in hand (23754, its fire cone, first) castable again; and
  with the lance down, a recall worth nothing, since it came back anyway. On the 13 captures of the owner's group of 22
  November, the turns D3B began with the lance down: 36 ended with no cast nor walk, 15 with a walk alone; the plans of
  those 108 turns struck nothing in 41.
- **Made.** `ForgelanceLance.Recovered`, after each cast, walk and mark struck: a Forgelance whose lance no longer lives
  has it back in hand (3361 ended, 3360 and 3590 given, the reach of 24591 taken back), and a throw that brings a lance
  takes its thrower's older one off. The turn's start and the judge's next turn leave the lance where it lies. Tests
  (`LanceTacticsTests`): the lance stays as its thrower's turn begins, comes back as an enemy kills it and as a cast kills
  it, the judge finds no throw next turn while it lies, a second throw leaves one lance, and a Forgelance whose lance lies
  far from the enemies recalls it; red before, green after.
- **Replayed** with `scripts/fights.cs --turns` on the same 13 captures, against `main` at ae5b308b, by breed. The
  Forgelance's 227 turns: plans of nothing 11 to 3, of walks alone 14 to 4, damage foreseen this turn 31,015 to 33,099,
  kills 125 to 145, the life the reply takes 10,275 to 10,990; the fire cone 23754 planned 120 to 157 times,
  Effondrement on the lance 16 to 7, the weapon's blow 41 to 12; of the probe's planned blows, those in earth that strike
  26 to 21, their damage 692 to 402, the fire ones' 29,860 to 31,993. Over two turns its plans deal 77,234 to 68,900 and
  the turns played 68,586 to 57,253: the next turn no longer gets a lance back it does not get, and the plans gain on the
  turns played more than before (13% to 20%). The turns the probe judged better played than planned, 50 to 39. The
  Sacrieur's 276 turns, the Ouginak's 303 and the Huppermage's 192 within the noise of searches bounded in time (0.2% of
  their damage).
- **Found, left to the owner.** Épilogue with the turn's last AP throws the lance on a free cell, striking nothing: planned
  70 times in the Forgelance's 227 turns on `main`, 104 after. The lance shields the Forgelance: it takes a cell the
  enemies walk around and, the captures say, the enemies' blows took 4,526 life from it in the group's 174 turns of one
  capture, 5,572 from the Forgelance; the reply the search plays never strikes it (a summon that deals nothing is worth
  nothing to an enemy's choice), but its cell keeps the enemies off, 10 to 83 points of life the Forgelance keeps in the
  replies the probe weighed with and without it. This change makes it more frequent; whether it is one throw too many is
  the owner's to say in game.
- **Not tested**: the recall after a walk or a mark struck (a trap or a glyph killing the lance), only after a cast.
- **Not measured in game**: the Forgelance recalling its lance, then striking with it in hand.
- **Rejected.**
  - A rule by spell or by element (D192, D244): the earth blows fell once the judge saw the lance stay, with no rule.
  - D465 kept and the recall left to the server: a turn that counted on the lance back and a recall the judge saw no use for.
