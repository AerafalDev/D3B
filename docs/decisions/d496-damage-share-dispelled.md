# Fights, a share of the damage received dispelled with its spell (D496)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The shares and
reductions of the damage received are
[D491](d491-unhandled-effects.md#d491-the-damage-received-reads-every-resistance-reduction-and-share-and-a-cast-moves-its-caster);
the Pandawa's drink, [D420](d420-step-9-the-pandawas-drink.md#d420-a-state-the-fighter-already-held-is-no-gain-when-a-cast-puts-it-back-the-pandawa-no-longer-swaps-sobre-and-saoul-instead-of-striking)
and [D486](d486-dispel-keeps-undispellable-buffs.md#d486-a-dispel-takes-only-the-buffs-the-server-ends-and-a-removal-naming-a-spell-that-spells-alone-the-pandawa-keeps-sobre-and-casts-in-its-element).

## D496: A share or a reduction of the damage received is held by its spell, so that a dispel takes it back: the Pandawa no longer drinks three times for one

*2026-11-22, the owner's report from game: "le Panda fait encore Bombance pour rien souvent j'ai l'impression". Builds
D227, D420, D486, D491.*

- **Found** with a throwaway probe judging each action of the Pandawa's plans on 10 of the owner's game captures of 22
  November: with AP left at a turn's end, the AI planned Bombance three times, drunk, sober, drunk, where one cast ends the
  turn as drunk. Saoul gives 85% of the damage received for 2 rounds; Sobre dispels Saoul's spell; but `Imagination` added
  a share (D491) to the fighter without holding it as a buff of its spell, so the dispel left it, and the third cast added a
  second: 85% of 85%, a reduction the server never gives, worth 3 to 18 points of the reply. The fighters the bot reads from
  the server had the same gap: their buffs of 1163 and 105 were not held.
- **Made**: a share (1163) and a reduction (105) of the damage received that last are held by their spell, the reduction
  naming its points by its parameter as the server's buff does, and `Imagination`'s undoing of a held buff takes them back;
  a spell cast again past its stacks replaces its oldest share or reduction, as a boost's (D227), the review's case;
  `FightCombatants` holds the server's buffs of 1163 and 105 too. Four tests (`DamageShareDispelTests`): a dispel takes back
  a share its spell gave, a reduction its spell gave, and a share the server gave, and a share cast again past its stacks
  replaces the oldest; red on `main`, green after. The test of `FightCombatants` names the buffs it now holds.
- **Replayed** with `scripts/fights.cs --turns` on the same captures, against D494 alone: the Pandawa's Bombance planned 81
  to 75 times in its 162 turns (the turns played cast it 104), its damage foreseen 20,446 to 20,615, the life the reply
  takes 3,851 to 3,923, the phantom share gone; the Forgelance's damage foreseen 5,285 to 5,327, kills 14 to 15; no other
  breed moved.
- **Not measured in game**: a Pandawa ending a turn drunk by one cast. Left: Bombance with the turn's last AP to end it
  drunk, which Saoul's share of the damage received is worth, the owner's impression to confirm in game.
- **Rejected**: a limit of Bombance's casts a turn, a rule by spell (D192).
