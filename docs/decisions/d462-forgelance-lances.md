# Fights, the Forgelance's lances (D462)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The summons as a last
resort are [D408](d407-d408-step-9-the-players-reports.md#d408-a-summon-is-a-last-resort-but-for-the-breeds-made-of-their-summons).

## D462: The Forgelance's summons are not a last resort, its lances striking as they are thrown

*2026-11-19, the owner's choice after the lead's study of the AI breed by breed on the captures of 18 and 19 November (a
Forgelance being the owner's master). Refines D408. Builds D201, D270, D407.*

- **Found**: D408 leaves out of the first search every spell one of whose effects summons, but for the Osamodas, the
  Roublard and the Steamer. In the pack of 3.7.14.18 that is far more than summons: eight of the Forgelance's spells strike and
  leave its lance on the map, so that a Forgelance threw its lance only when its plan did nothing else, a walk being enough
  to make it do something.
- **Measured** with `scripts/fights.cs --turns` on the 83 game captures of 18 and 19 November (client 3.7.14.18), before and
  after, 1,181 turns replayed: the damage foreseen this turn 19,725 to 21,171, the kills 475 to 526, the life the reply takes
  9,865 to 9,504, the damage over two turns 47,550 to 47,896. The study's replay broken down by breed, on a copy, gave the
  Forgelance's 221 turns exactly that gap: damage 4,439 to 5,885 (a third more), kills 120 to 171, the reply 1,907 to 1,546,
  its first lance spell planned 96 times instead of 2; every other figure of the standard replay is the study's variant's,
  so that no other breed moves. The Forgelance's plans: 9 fewer walks alone, 7 more that do nothing, which the judge
  prefers once its lances are weighed; the searches' halfway 73 to 99 ms, within the budget of 800.
- **Made**: the Forgelance (breed 20) joins `FightTactics`' list of the breeds whose summons are not a last resort. Its
  lances are searched from the first search, as an Osamodas' beasts are; every other breed's summons stay a last resort.
  Two tests: a Forgelance throws a lance that strikes more than its blow, a Sram keeps its blow over the same lance.
- **Rejected**: a criterion by effect, a spell left out only when its summon is its only effect of worth (no damage, steal,
  heal or shield), which the study recommended: the owner wants the list, a rule one reads by breed, to grow instead. D408
  undone for every breed: replayed on the study's copy, it cost the Sacrieur a third of its damage foreseen this turn
  (1,302 to 875) and half its kills (22 to 10), its summon cast 49 times instead of 7, the Enutrof's bags and the Féca's
  barricades coming in too.
- **Not measured in game**: a Forgelance throwing its lance through the bot. The other breeds whose spells strike and summon
  (four of the Féca's, one of the Eniripsa's, one of the Pandawa's) keep them as a last resort.
