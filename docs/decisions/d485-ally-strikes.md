# Fights, the blows on the character's own allies (D485)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The judge is
[D201](d176-d208-step-9.md#d201-the-ai-an-imagined-fight-a-judge-a-beam-search-and-the-enemies-reply), its comfortable
life [D407](d407-d408-step-9-the-players-reports.md), a summon's life [D270](d253-d292-step-9.md#d270-summons-in-the-ai-brought-judged-and-replying);
the pushes [D214](d209-d252-step-9.md) and [D289](d253-d292-step-9.md); the AI in a group [D454](d454-group-fights-bolder.md),
[D460](d460-group-fights-strike-now.md) and [D467](d467-group-reply-target.md).

## D485: A blow on an ally costs what it takes, and a push a fighter stops strikes that fighter too

*2026-11-21, step 10, 10d, the owner's report from the group games of 20 and 21 November: "un perso frappe certaines fois
son allié"; settled by the lead session, reversible by a weight. Refines D407, D289. Builds D463.*

- **Measured first.** `scripts/fights.cs --live` now counts the life the character's own casts took from its team, by
  breed and spell, and `--turns`, for each cast played and planned, what the engine foresaw of it against what the server
  announced, on the whole team and on its characters alone. On the 20 captures of the owner's three group runs (20
  November 12:10 UTC, 21 November 00:39 and 08:03; 4,240 AI fights, client 3.7.14.18), the characters' casts took 13,619
  life from the other characters and 4,870 from summons: an Enutrof's 13335, a cross of 3 of mask `h,m,d,H,M,D`, 6,162
  from characters in 192 blows; a Pandawa's 12782, its cross about the target of mask `g,A,*E3531` once drunk, 3,450 in
  87; then pushes into an ally (action 80), an Iop's 13108 and 13116 618, a Zobal's 13403 728, an Eliotrope's 14575 620,
  a Forgelance's 23741, 23754 and 23824 1,091.
- **Two causes.** The zones were foreseen right (00:39 run: 13335 2,255 foreseen for 2,324 announced, 12782 3,148 for
  3,049): the judge took them knowingly. Above `TacticWeights.Comfort` (60%) of its most life an ally's point is worth a
  quarter of the curve's (D407), 0.16 at its most, where a point dealt an enemy is worth 0.6 to 1, so a zone of 30 on an
  enemy and its ally beat a blow of 20 on the enemy alone. The pushes were not foreseen at all (13108: 0 for 228): the
  engine hurt the target a push stopped, never the fighter that stopped it. On the 213 captures of 3.7.14.18 that fighter
  took half the push damage the formula gives against itself, its own reduction counted, 932 times in 1,012 on the
  caster's other side and 692 in 756 on its own (`CastBench`, "push into a fighter").
- **Made.** `Imagination.Cast` keeps in `ImaginedFight.StruckBySide` the life, shields counted, each fighter lost to the
  casts of the other fighters of its side; `FightJudge` takes it off the side's worth once more, at
  `TacticWeights.AlliesStruck` (1 in every weight) times the fighter's weight, so that a point taken from an ally costs at
  least what a point dealt the hardest-hitting enemy is worth; a summon's life, worth nothing of itself (D270), costs
  nothing more, the caster's own blows on itself neither. The reply's blows on the side keep their weight, the threat and
  D454's boldness unchanged. `Imagination.Slide` strikes the fighter right behind a target a push stopped with half the
  formula against it. Tested, with the default, the group's and the prudent weights: a zone on an enemy and its ally at full
  life loses to a blow on the enemy alone (it won before), as at 40% life (lost already, the curve whole there); the zone
  wins with no ally in it; a push stopped by an ally that resists pushes strikes it with half its own damage. Not tested:
  the summons and the caster left out of the cost.
- **Replayed** with `scripts/fights.cs --turns` on the same 20 captures, 6,074 turns (main 3528f830 then this change,
  the bench the same): the life the plans foresee taking from the other characters falls from 12,035 to 2,581, where the
  turns played took 13,631 as the server announced, now foreseen 13,376 (11,918 before); on the whole team, summons
  counted, from 19,944 to 9,810, the casts that strike it from 508 to 261. The Enutrof's from 7,095 to 701, the
  Pandawa's from 3,724 to 799; the four breeds that push from 1,216 to 1,081, their pushes into an ally seen now (the
  Iop's turns played 215 foreseen to 644 for 666, the Eliotrope's 323 to 648 for 601). The damage foreseen falls from
  289,948 to 285,597 (1.5%), the kills from 1,693 to 1,678, the damage over two turns 1.4%; the Enutrof's damage 3.7%,
  its kills 228 to 214, the Pandawa's damage 4.2%, its kills 319 to 326; the other breeds within 1.2%, the Forgelance's
  kills 179 to 172. The plans keep their kinds (6,011 with casts, 446 walks alone, 233 empty; 6,012, 443, 235). The
  life the reply takes rises 4.8% on the plans and 4.1% on the turns played, the reply's pushes now striking the fighter
  behind: its foresight of what the enemies took from the character rises from 27,326 to 30,393 for 54,359 lost, on the
  00:39 run. The Sacrieur's plans leave the reply 4,881 to 5,962 (its turns played 5,158 to 5,406), the collision's doing,
  not the weight's: its pushes into the enemies worth more, its damage 0.4% up.
- **Collision alone**, `--weights AlliesStruck=0`, on the 00:39 run: the plans' blows on the team rise from 7,972 to 9,725,
  155 casts to 239; the two go together.
- **Not measured in game**: the characters of a group no longer striking one another, which the owner's next group run
  shows; the push into an ally foreseen, never seen by the AI in game.
- **Left**: the Forgelance's lances are summons, struck freely as D270 wants; its 23736, 23801 and 23824 are foreseen
  striking its team where the server announced little (the lance's zones, another session's). A push into an ally of the
  caster's side came at a quarter 360 times, not explained.
- **Rejected.**
  - The comfortable life for the character alone, its allies' life on the whole curve: a point on a full ally would cost
    0.5 instead of 0.16, still under an enemy's, and the enemies' reply would weigh three times more on every ally above
    60%, undoing D454's boldness in a group.
  - The ally's loss counted from the turn's start in the judge: the reply's blows would count again, as the threat does.
  - A rule in the search, no cast whose zone holds an ally: blind to a kill worth the blow, and to the pushes.
  - The pushes left for a session of their own: without them a quarter of the life taken from the allies stays
    unforeseen, and the collision alone, the judge unchanged, had the AI plan more blows on its allies (00:39 run, 155
    casts to 239), the damage it adds on the enemies worth more than what the allies lose.
