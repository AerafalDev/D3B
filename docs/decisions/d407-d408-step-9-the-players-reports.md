# Step 9, the players' reports (D407 to D408)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D407: The fight AI after the players' reports: a refused cast is weighed anew, a life steal is no longer cast for a heal that is not needed

*2026-11-09, step 9, the owner relaying five reports of players, in French. Refines D203 (the casts the server refuses), D212 and D390
(the judge's weights); builds on D192, D201, D209, D244 and D270. The owner chose the judge's change among three (below).*

- **Invisible monsters (Prespic): a bug, fixed.** `FightPlay` kept every spell the server refused out of the whole fight, with an
  error text as with no answer; D203 had measured only the second (a transformed character's own spells, answered by nothing). A
  monster the server does not let the character target refused one cast, the spell left the fight, and the AI passed its turns
  once the monsters were seen again, the three turns reported. Now a cast refused with a text is not weighed again that turn on
  that cell, as before, and the next turn weighs it anew; one left unanswered stays out of the fight. The test sends the refusal
  and the next turn: it failed on the second cast. **Not measured**: no capture holds an invisible fighter (`FightVisibility` is
  2 on every fighter captured), so what the server sends as a monster hides and shows again is unknown: the state keeps its last
  cell until the server shows it again, and `Combatant.IsVisible`, which `NeedVisibleEntity` reads, is never unset. It wants a capture.
- **Life steals (reports 2 and 4): reproduced, and the judge's change.** A steal heals half of what it deals (`Imagination`), and the
  judge counts the character's life as `Survival` (2) times a square root of its share of its most: a point healed is worth 1
  at full life, more below, where a point of damage on an enemy is worth 0.6 to 1. On a test of two spells of 3 AP, a steal of 8
  healing 4 and a blow, the blow won only from 14 damage at 97% of the character's life, 16 at 90%, 18 at 50% and 20 at 30%:
  the steal beat a blow dealing three quarters more with 3 life lacking. Against a monster a blow killed in one cast and two steals
  killed too, the steals won by the 3 life they healed. Made: above 60% of its most life a point of the character's life is worth
  a quarter (`TacticWeights.Comfort` 0.6, `ComfortWorth` 0.25; below, the curve is whole), and between plans that leave the same
  enemies alive and are worth the same within a point of life, the one with the fewest actions wins (`FightTactics.Plan`, the plan
  that ends the turn keeping its tie). The same test: the blow wins from 10 damage at 97%, 11 at 90% and 80%, the steal still
  wins up to 18 at 50% life, and the single blow is cast against the monster it kills. The fear of damage falls with the
  curve while the character's life is above 60%, as the owner means by an AI too safe.
- **Rejected for the life steals.** The owner's rule of a steal played only when its damage is within a quarter of the best spell's:
  a rule more in the search, blind to the character's life (it would refuse a steal at 20% life). `Survival` 2 to 1.5: a single
  weight, but it moves the AI's whole defence and leaves the kill in one cast to a tie.
- **Dopou (report 3): the judge is right by its numbers, not reproduced as a bug.** A push against a wall deals
  `(level / 2 + 32 + bonus - reduction) * cells / 4` for the cells it could not move the target (measured on 11 pushes, D214; the
  pushes the captures hold are of 1 to 4 cells). The pack's *Panache DoPou* (12094) pushes 10 cells around its caster for 1 AP, so
  a target against a wall takes 205 at level 100, and the AI casts it twice then. The tests set a push against a wall and a
  blow: the AI casts what deals more. If the player sees the water spell hit harder, the push's damage is overestimated for long
  pushes, or the target's push resistance is unknown to the AI: no capture holds either. It wants a capture of a long push.
- **Summons (report 5): no change raised their worth but D390, which did little.** The only change of the fight AI since 2026-11-06
  is D390 (`Approach` 0.8 to 0.9), which weighs a summon's first blow 12% more and the rest of its worth, being alive for 1.5 of
  its turn's damage (D270), not at all. A sweep of the blow a character gives up for two summons, 20 to 100 damage, chose the
  same plans at 0.8 and 0.9. A summon is worth more than a blow per AP by D270's measure (71 planned where the players brought 42), and the engine counts the summoner's slots (`CastRules`), so it summons again as soon as one dies, which
  reads as insisting. Neither *Chafer* nor *Arakne* is a summon of a breed in the pack: they are those of monsters (*Invocation de
  Chaferfu*, *d'Arakne*), so the report's character plays a form or an item of which no capture exists. Nothing changed here; the owner then asked for summons as a last resort (D408).
- **Replayed** on the 5,056 turns of the 152 captures (`scripts/fights.cs --turns`, which takes `--weights` now, the judge's weights
  tried from the command line). With the curve and the tie, the numbers before in brackets: the AI's plan dealt more damage
  than the turn played in 1,998 turns (1,355; 1,655 with `Survival` 1.5 alone), as much in 2,202 (2,837); kills as many 4,845
  (4,854), more 174 (165), fewer 37 (37); the reply took more of the life than after the turn played in 695 turns (379; 494
  with `Survival` 1.5). The casts planned: damage 7,015 (6,682), shields 1,096 (1,460), heals 174 (208), boosts 376 (479), moves 310 (357),
  summons 1,004 (988); the plans that do nothing 36 (32). The curve alone gave the same within 2%: the tie moves a few plans.
  So the AI attacks more and protects itself less, as the owner asks, for a reply that costs it life in one turn in seven
  instead of one in thirteen.
- **Not measured in game**: the players' fights with the new curve; the owner is to say whether the AI plays less safely and
  whether the life steals are rarer.

## D408: A summon is a last resort, but for the breeds made of their summons

*2026-11-09, step 9, the owner's ask after the players' reports, "for a fast farm". Refines D270 and D407.*

- **Asked.** The AI summoned whatever its breed, the summon being worth more than a blow per AP (D270, D407). The owner wants a
  fast farm: only a breed whose summons are its kit, an Osamodas for one, summons as it likes; any other summons only when it
  can do nothing else that turn.
- **Made.** `FightTactics.Plan` searches the turn without the fighter playing's summoning spells first, unless its breed is one of
  those made of summons; the plan it finds is played when it does something, and only an empty plan lets the search run again
  with the summons. The breeds, the lead's reckoning to be corrected by the owner: the Osamodas (2), the Roublard (13), whose bombs
  are its whole kit (D277), and the Steamer (15), whose turrets are. The Sadida's tree, the Féca's walls, the Enutrof's bag and the
  rest are last resorts. A summon the fighter playing's *summon* brings is not touched: only the character's turn is searched so.
  Three tests: a Sram with a blow and a summon strikes, one with a summon alone summons, an Osamodas with both summons.
- **Rejected.** A weight on the summons' worth, by breed: a number to tune where the owner asks a rule. Summons after the blows
  with the AP left: a farm that waits for its summons' turns.
- **Not measured in game**: whether an Osamodas still farms as fast, and which other breeds the owner counts as summoners.
