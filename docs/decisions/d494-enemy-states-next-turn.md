# Fights, a state set on an enemy worth its next turn alone (D494)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Buffs and states
weighed over the rounds they last are
[D209](d209-d252-step-9.md#d209-buffs-and-states-worth-what-they-do-over-the-rounds-they-last); a state the fighter already held,
[D420](d420-step-9-the-pandawas-drink.md#d420-a-state-the-fighter-already-held-is-no-gain-when-a-cast-puts-it-back-the-pandawa-no-longer-swaps-sobre-and-saoul-instead-of-striking).

## D494: A state set on an enemy adds to the side's blows on the next turn alone, so that a Huppermage strikes in its element

*2026-11-22, the owner's report from game: "L'huppermage veut trop changer d'élément avec Cycle Élémentaire alors qu'il n'y
en a pas besoin et du coup il tape souvent air alors que son élément principal c'est terre". The fix is the judge's,
general, no breed named. Refines D209 and D420; builds D192, D201, D244, D493.*

- **Found in the pack of 3.7.14.18** (breed 17): each elemental spell of the Huppermage puts the elemental state of its
  element (290 Fire, 291 Water, 292 Earth, 293 Air) on the enemy it strikes for the whole fight, and once that enemy holds
  two of them, the combination spell (13671) casts the pair's spell (13702 to 13707), which gives the caster 50 power for 3
  rounds, an effect on the enemy, and takes the four states off (32415). Cycle Élémentaire (13722, 1 AP) turns the enemy's
  state into the next element and gives the caster its AP back.
- **Measured** with `scripts/fights.cs --turns` on 10 of the owner's game captures of 22 November, frozen, and a throwaway
  probe that judged each action of the Huppermage's plans: `FightJudge.Lasting` credited each elemental state on the enemy
  35 to 175 points, a buff of the whole fight whose worth is the more the Huppermage's spells deal an enemy in it, up to
  the rounds the fight is foreseen to last. So a blow of 20 in air plus a Cycle beat a blow of 67 in earth: on `main`, the
  Huppermage's 70 turns planned 31 Lame Astrale (air) and 78 Cycles for 24 Météore (earth), and 54 of its blows in another
  element were ones an earth spell would have beaten, its damage foreseen 4,775 where the turns played dealt 6,247.
- **Made**: in `FightJudge.Lasting`, a state the side playing set on an enemy counts nothing of what it adds to the side's
  blows over the rounds after the next turn; what it adds to the next turn is the approach's, which reads the enemy's
  states already. What it takes off the side's blows, such as a state keeping the enemy from taking damage, and what it
  takes off the enemy's blows keep their rounds. The effects reading a state on their target may take it off, as the
  combination does, which the judge cannot tell. Three tests (`EnemyStateTacticsTests`, on spells built as the pack's): a
  character strikes harder rather than marking an enemy for blows to come, the judge credits a state on an enemy nothing
  beyond the fight as it stands, and a state that spares an enemy the side's blows still costs its rounds, the last the
  review's case; each red before its change. Not tested: a state taking off the enemy's own blows, whose rounds are kept.
- **Replayed** on the same captures, against `main`, each on its own tree: the Huppermage's damage foreseen 4,775 to 7,029,
  kills 10 to 13, kills over two turns 49 to 53, the life the reply takes 1,279 to 1,225; Lame Astrale 31 to 6, Météore 24
  to 50, Cycle Élémentaire 78 to 42, Runification 36 to 18. No other breed's figures moved (the Sacrieur, the Pandawa and
  the Forgelance), searches 175 ms halfway against 172.
- **Not measured in game**: a Huppermage of earth striking earth. Left: Cycle Élémentaire still planned 42 times in 70
  turns, its AP given back making it free whenever the combination's 50 power is worth a point; whether the server gives
  that AP back each time is read in the pack, not measured.
- **Rejected**: naming the elemental states or the Huppermage (D192, D244). Leaving out an enemy's state only when one of
  the caster's spells takes it off: the more exact reading, which walks the spells each effect casts, for the same result
  on the captures. Bounding every buff on an enemy to one round: a deboost lasting several rounds is worth each of them.
