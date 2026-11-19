# Fights, the easy challenges (D465)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The challenges read
are in [D461](d461-fights-challenges-read.md), their bonus in [D438](d438-fights-challenges-bonus.md).

## D465: The AI plays for the easy challenges, those of its own turns, while a turn costs no more than their bonus pays, pack format 13

*2026-11-19, outside the roadmap's steps, the owner's ask (« que l'IA fasse les challs qui sont jugés faisables ou faciles »),
the second of the three sessions D461 began; the list, the default and the price are the owner's choices. Builds D198, D201,
D438, D446, D461.*

- **Measured** on 3.7.14.18 ([client.md](../client.md#challenges)): the client's table holds 842 challenges in 7 categories,
  the fights' own in category 1 and a dungeon boss's version of some in 9 with the same completion criterion, so the AI reads
  a challenge's criterion, not its number. In the owner's 2,976 captured fights, 34 challenges; the 12 whose criterion asks
  only of each turn were 1,458 fights, 49%, won without being played for from 6% (Zombie) to 93% (Prudent).
- **The pack, format 13.** `ChallengeCatalog` (`challenges/catalog`, `GameTables.Challenges`): each challenge's texts,
  category, icon, monster and the challenges it excludes, a slice of one array, with its completion and activation criteria
  as the client writes them; `ChallengesExtractor` reads it, its names and descriptions among the texts the pack keeps. 80
  exclusions name a challenge the table lacks, kept as written.
- **The owner's list**, the easy ones: Zombie, Statue, Nomade, Versatile, Collant, Misanthrope, Prudent, Raser les murs, De
  la même lignée, La diagonale du plein, En ligne de mire, Pas vu pas pris, matched by their exact criterion
  (`TurnChallenge.Of`): what each asks of the MP used, of each spell cast once a turn, or of the cell the turn ends on, beside,
  away from, in line, in diagonal, in sight of an ally, an enemy or an obstacle.
- **The search.** `FightTactics.Plan` takes the challenge: it searches the best plan as always, and when that plan breaks
  the challenge it searches again, its casts and walks those the challenge allows and its finalists only the ends that keep
  it. The owner's ask (« faut faire le pour et le contre, avec un score ») sets the price: the kept plan wins while what it
  loses on the best, both judged once the enemies replied, stays within half of b / (1 + b) of what the best gains on ending
  the turn now, b the challenge's bonus: a fight slower by that share still earns as much a minute, and half of it since a
  turn's challenge is lost at the first turn that breaks it. 15% of the turn at a bonus of 45%, 28% at 130%. The price is
  paid once a turn: once a plan of the turn keeps the challenge, the turn's next searches keep it at any price
  (`TurnChallenge.KeptThisTurn`), the review's finding, since the turn's gain falls as it is played and the walk that ends
  it would have left a challenge the turn already paid for. Statue's turn ends on the cell it began on, its MP whole, so
  that a jump or a teleport breaks it.
- **Replayed** on the owner's 83 captures (`scripts/fights.cs --turns`, the fight's risk aside): of 417 turns played with an
  easy challenge, the best plan keeps it in 264, the search keeping it in 83 more, and 70 leave it as too costly, Statue 14
  of 22, Zombie 10 of 21 and En ligne de mire 19 of 41 most; Versatile and Prudent are left once in 133 turns. A plan given
  a challenge takes 90 ms halfway and 786 ms at most, against 82 and 711 for the plan alone, the second search only when
  the best breaks the challenge.
- **In the fight.** A hunt's and a harvest's profile have « Challenges », None or Easy, Easy by default, a profile stored
  before included; a follower plays for its master's profile's (D446), a list for its step's. A fight the AI judged too
  risky plays for none (D198): a challenge never loses a fight. A turn whose plan leaves the challenge leaves it for the
  fight, logged once. The character's summons the client controls play for it too.
- **Not measured**: a fight in game playing for each challenge; Nomade's partial escape, a walk off an enemy that costs more
  than its cells, not foreseen; the MP a spell gives or takes in the turn, which the AI counts as used or not; whether the
  server counts summons among the allies of Collant and its kin, which the AI does; whether
  a challenge kept wins as often in game as the replay keeps it. The pack of format 13 ships with the release `client-3.7.14.18` on the owner's go (D24), before an app that reads it.
- **Rejected.**
  - Hardi and Barbare among the easy ones, the owner's choice: the first walks a ranged class into the enemies, the second
    asks of the kills, not of the turn.
  - Keeping the challenge whatever it costs: a Statue for a melee class would pass its turns, the owner's « si le combat
    dure 5 min de + c'est pas la peine ».
  - The whole balance b / (1 + b) as the price: a costly turn is paid for nothing when a later turn breaks the challenge.
  - One search with a penalty on the plans that break the challenge: the best plan would no longer be searched whole, and
    the price could not be weighed against it.
  - Matching the challenges by their number: the dungeon bosses' versions carry the same criterion under other numbers.
