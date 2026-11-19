# Fights, the group's reply and its target (D466)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The reply is
[D201](d176-d208-step-9.md#d201-the-ai-an-imagined-fight-a-judge-a-beam-search-and-the-enemies-reply) and
[D211](d209-d252-step-9.md#d211-the-enemies-reply-beyond-the-greedy-measured-no-gain); the AI in a group,
[D434](d429-d435-step-10-groups.md#d434-the-ai-unchanged-in-a-group-then-measured),
[D454](d454-group-fights-bolder.md) and [D460](d460-group-fights-strike-now.md).

## D466: An enemy of the reply holds every character of a group as dear as the fighter playing, as real monsters do

*2026-11-19, step 10, the first part of 10d, the owner's ask after the lead's study of the AI breed by breed (its T2). Refines
D201, D211. Builds D244, D434, D463.*

- **Found**: in `FightTactics.Replies` each imagined enemy strikes the target that lowers `FightJudge.AlliesWorth` most,
  and that worth counted the fighter playing's life `TacticWeights.Survival` times (2, 3 when prudent) against an ally's
  once. Its own life dearer is the fighter playing's view, not the monsters': with it, the imagined enemies turned on the
  character far more than the real ones did. The judge's threat already chooses its target by damage alone.
- **Made**: `AlliesWorth`, which only the reply uses, weighs every character of the side (`Combatant.IsCharacter`, a breed
  and no summoner) as the fighter playing; a summon and a companion keep their weights, so that alone, with no other
  character on its side, nothing changes. The judge's own worth of a fight keeps the character dearer. Tested: an enemy
  between the character at 50 life and an ally of its group at 40, a blow of 20, strikes the ally, with the default, the
  group's and the prudent weights (it struck the character before).
- **Measured** with `scripts/fights.cs --turns` on the 83 game captures of 18 and 19 November (client 3.7.14.18, main
  d95882f2 then this change), 1,181 turns: the reply's blows in a group, foreseen against dealt between turns, on the
  character 10,574 for 2,360 (x4.5) to 5,237 (x2.2), on its other allies 22,942 to 28,739 for 13,395 (x1.7 to x2.1); alone,
  10 turns, unchanged (247 for 271). The life the plans leave the reply to take falls from 9,504 to 3,765, the damage
  foreseen this turn rises from 21,171 to 21,363, the kills foreseen stay 526, the damage over two turns 47,896 to 48,111;
  plans 1,234 with casts, 171 walks alone and 214 empty (1,233, 176 and 210). By breed, the character's x-ratio falls in
  every one (Féca 6.2 to 2.0, Enutrof 6.7 to 2.6, Iop 4.5 to 1.5, Sacrieur 3.2 to 1.1, Pandawa 5.6 to 2.9, Zobal 4.4 to 3.0,
  Eliotrope 2.4 to 1.3, Huppermage 4.5 to 1.5, Forgelance 6.6 to 4.3); the empty plans move by one or two but for the
  Eliotrope (53 to 57, its walks alone 44 to 41) and its damage foreseen rises (4,789 to 4,836). The figures on the character
  and on its allies are the study's own variant's, made on a copy.
- **Left**: the reply foresees twice what the team takes in all (34,000 for 17,100), the turns it foresees nothing on the
  character that lost life rise from 183 to 224. The study's two other causes stay, for 10d's next parts: an ally of the
  group has no spells for the AI, and the allies never play in the reply, so that one that kills an enemy before it plays
  is not seen. The group's threat (`TacticWeights.Group`, 0.75) is set once they are made (D434).
- **Not measured in game**: the characters of a group a little less on the defensive, which the owner's next group run shows.
- **Rejected.**
  - The fighter playing's life counted once in the reply, a group or not: alone its summons and its companion would be
    struck more against what the replay of the solo fights measured right (D211, x0.9).
  - A weight by breed, or the group's weights changed instead: one AI for every breed through the effects (D244), and the
    cause is the enemies' choice, not the character's.
  - The allies' spells and their turns in the reply in the same change: each of the study's causes measured apart.
