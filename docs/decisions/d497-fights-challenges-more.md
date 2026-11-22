# Fights, more challenges (D497)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The easy challenges
are in [D466](d466-fights-challenges-easy.md), the challenges read in [D461](d461-fights-challenges-read.md).

## D497: The AI plays for eleven more challenges, cast by cast, at three quarters of what keeps the reward a minute the same

*2026-11-22, outside the roadmap's steps, the owner's ask (« Faudrait gérer plus de challenges »), the list and the price the
owner's choices after the lead listed the client's 43 fight challenges, the third of the challenges' sessions D461 began.
Builds D461, D466; supersedes D466's price and its rejection of Hardi.*

- **The owner's list**, matched by their exact completion criterion as D466's: Hardi (`TD<2,hc0,e1`), Élémentaire
  (`SC=hc0,e,s`), Sans-cœur (`SO!e0,hc0,h`), Premier (`CK~1`), Dernier (`Ck~1`), Élitiste (`CK~!,1`), Tueur à gages
  (`CC=e0,m1,k,k`), Tueur à vue (`CO=e0,m1,tk,n`), Cruel (`SL<1`), Ordonné (`SL>1`) and Focus (`SF=1`), read on the pack of
  3.7.14.18 with a tool never committed. With D466's twelve, 23 of the 43; Misanthrope was already among them.
- **The rules**, from the client's descriptions and the Bubble emulator (`artifacts/studies/challenges-bubble.md`), which
  implements only Premier, Cruel and Ordonné faithfully: Hardi ends the turn beside an enemy; Élémentaire binds each allied
  fighter to the element of its first blow on an enemy, neutral one of them, a cast in two elements breaking it; Sans-cœur
  heals no ally but the fighter itself; Premier kills no enemy before the target, Dernier the target only once the others are
  dead, a cast killing it with others allowed as the server may kill it first or last, Tueur à gages only the target the server named and none once it is dead until it names the next; Élitiste strikes no
  enemy but the target while it lives, Tueur à vue the target the server named for the fighter, then the enemy nearest it;
  Cruel and Ordonné kill from the lowest or the highest level, a tie free, the enemies' summons left out as Bubble leaves them;
  Focus strikes no other enemy while the one its team struck last lives, a zone striking two at once breaking it.
- **Cast by cast.** `TurnChallenge.Cast` judges each cast the search imagines against the fight before and after it, and
  gives the challenge as it stands then (the element bound, the enemy struck, the next target) or none when the cast broke
  it. The best plan is watched for it and never held to it, so that a plan breaking it on the way is known as such; the plan
  that keeps it is searched as in D466, its casts those that keep it.
- **What the fight said.** `FightState.FirstElements` keeps the element of each fighter's first blow on the other team, a
  push's binding none, and `FightState.StruckEnemyId` the enemy the character's team struck last, both from the server's
  losses of life; `FightChallenges.TurnOf` fills them in with the target the server named, the one it named for the fighter
  for Tueur à vue, and the turn reads them again before each of its plans (`FightPlay.Refreshed`), its price kept, and
  plays for it no more once the server decided it, the review's finding.
- **The price**, the owner's ask (« faudrait monter un poil la valeur »): three quarters of b / (1 + b) instead of half, 23%
  of the turn at a bonus of 45%, 42% at 130%.
- **Replayed** on the owner's 83 captures of 18 and 19 November (`scripts/fights.cs --turns`), the branch against main: the
  best plans, their damage, kills, heals and the reply the same to the unit, the best plan watched and never changed. Of
  the 416 turns D466's twelve were played in, 271 kept by the best plan, 98 by the search and 47 left, against 91 and 54
  at half the price, Zombie left 2 times in 21 against 4; of the 156 turns of the eleven, 112 kept by the best plan, 31 by
  the search and 13 left, Hardi once in its single turn, Sans-cœur in none. A plan given a challenge took 160 ms halfway and
  792 ms at most, against 184 and 1,879 on main in the same hour, the machine shared with other sessions.
- **Not measured**: a fight in game playing for each; whether the server counts a summon's blow, a poison's or a trap's for
  the fighter that set it, which the AI does not foresee; whether a heal at full life breaks Sans-cœur; whether an enemy's
  summon can be Premier's or Élitiste's target, or counts for Cruel and Ordonné; how the server names Tueur à gages' next
  target; a kill on a walk, through a trap, which no challenge of the kills weighs.
- **Rejected.**
  - Économe, Imprévisible, Barbare, Mains propres, Intouchable and the others of the 43, the owner's choice.
  - Hardi left out, D466's choice: the price bounds what it costs a ranged class, the owner now asking it.
  - Judging the kills' order on the turn's end alone: two kills in one turn in the wrong order would look kept.
  - One element for the team, Bubble's reading: the client's text binds each fighter.
