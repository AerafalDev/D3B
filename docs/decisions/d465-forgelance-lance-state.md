# Fights, the Forgelance's lance in hand (D465)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The lances searched
first are [D462](d462-forgelance-lances.md).

## D465: A thrown lance is out of the Forgelance's hand until its next turn, a measured rule the pack does not hold

*2026-11-19, the owner's question after D462 (whether the lance left on the map explains the Forgelance's empty turns), the
lead's measure on the replay, the owner's choice of a measured rule over leaving the refusals to D407. Refines D462. Builds
D224, D244, D255, D270, D407.*

- **The empty turns**: the 7 Forgelance turns D462 left empty, and the 21 empty before it, are all of rounds 1 to 3 with
  the enemy 6 to 12 cells away, no lance on the map and no spell its states forbid: the judge waits when nothing can be
  struck this turn, the lances reaching next turn from where it stands. The Pandawa's 17 empty turns are the same, enemies
  6 to 20 cells away; one only had two spells its states forbade.
- **Measured** on the 83 game captures of 18 and 19 November (client 3.7.14.18): at each throw the server ended states
  3360 and 3590 on the Forgelance and gave it 3361; with the lance out the players cast none of the spells asking 3360
  (the throws ask 3360 or 3589); 304 of its 308 turns began with 3360 and 3590 again and no lance on the map. The pack
  holds none of it: no effect of any spell takes 3360, the lance's (monster 7139) starting spell takes 3590 and gives 3361
  to itself, client-only, and effect 3792 of the throws holds no state. The AI kept 3360 after a throw, so that 33 of the
  308 plans threw a lance and cast after it a spell asking it, which the server refuses; 20 threw the same lance twice.
- **Made**: `ForgelanceLance`, the ids in one place with the measure: a cast that brings monster 7139 takes 3360 and 3590
  from its caster and gives 3361; `Imagination.TurnStarted` gives 3360 and 3590 back and takes 3361 and the lance off the
  map as its thrower's turn begins, and the judge's next turn, which weighs the approach, does the same. Eight tests: the
  thrower no longer holds it, a second throw refused by its states, the lance back as the thrower's next turn begins, left
  as an enemy's begins, each Forgelance's own lance alone back, a throw with 3589 alone leaving the states, the judge's
  next turn throwing again, one throw a turn in a plan.
- **Replayed** with `scripts/fights.cs --turns`, before and after, on the same captures: only the Forgelance moves. Its
  221 turns plan 5,676 damage instead of 5,885, 162 kills instead of 171, and the reply takes 1,643 instead of 1,546;
  the first lance spell is planned 67 times instead of 96. Against the turns before D462 (4,439, 120 and 1,907), the
  lances searched first are still worth over a quarter more damage: of D462's gain, 209 were second throws the server
  refuses. The totals of every breed change by the Forgelance's figures alone.
- **Rejected**: a rule read from the pack, as the rest of the AI is (D244): the swap is the server's own and nothing in the
  pack says it; giving a summon's client-only state to its summoner would touch the states of many other monsters'
  starting spells. Leaving it to D407, the server refusing the second cast and the AI planning again: a round trip a turn
  and a plan that counted on a blow it cannot strike.
- **Not measured in game**: a Forgelance throwing once and then casting what it may with the lance out. The state 3589,
  which the throws also accept, was held in one turn of the captures, by whatever gives it; the AI keeps it as the server
  sends it. The ids are those of 3.7.14.18: a client that changes them leaves the throws as they were before this entry.
