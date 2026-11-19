# Fights, a blow now worth more than a blow later (D460)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The AI in a group is
in [D434](d429-d435-step-10-groups.md#d434-the-ai-unchanged-in-a-group-then-measured) and
[D454](d454-group-fights-bolder.md).

## D460: The AI weighs next turn's damage under 1, in a group too, so that it strikes now what it could strike later

*2026-11-19, the owner's report after testing main a752a945 in game with a master hunting and three followers ("plein de
fois où y'a des persos qui peuvent taper mais qui font rien, ou qui use un sort pas de leur élément qui sert à rien"), the
lead's study of the run's captures, the owner's choice of the lead's recommendation. Refines D454. Builds D203, D210, D212,
D390, D407.*

- **Measured** on the run's four captures (23 fights, 87 turns of the AI, `scripts/fights.cs --live` and `--turns`): 15 turns
  with no cast nor walk, 6 of the master's (a Forgelance) and 9 of a Huppermage's, each with a monster in reach, often one of
  15 to 56 life it would have killed. Replayed from each turn's start, D454's `TacticWeights.Group` (threat 0.75, approach
  1.1) plans nothing on the same 15 turns and no other; the defaults plan a walk and a blow on all 15.
- **Why**: the judge bounds next turn's damage by what killing the enemy now would take off its cost (D203), then weighs it
  by the approach. Under 1 a blow now is worth more than the same blow next turn; at 1 they are worth the same and ending
  the turn keeps its tie (D407); above 1 a monster the character can kill now and next turn alike is better
  left whole. Replayed on the 64 turns that end before the fight does, approach 1.1 alone halved the damage planned (1,246
  instead of 2,345) and the kills (26 instead of 63), and the reply took half again more life (959 instead of 650); 1 left 6
  turns empty, 0.99 none; the threat at 0.75 alone changed nothing in these fights.
- **The spells of another element**: of the blows planned, those of another element while one of the character's own was
  castable from the same cell and foreseen dealing more a point were 8 with D454's weights, 3 with the defaults; the casts
  of a damage spell only to swap or push an ally, 6 played, 3 planned with the defaults. Most of the rest are spells of
  the reach the character needs (the Forgelance's fire spells reach 3 cells, its water and air ones 6) or a second element
  for a state, chosen by the damage foreseen through the effects (D244).
- **Made**: `TacticWeights.Group` keeps the threat at 0.75 and weighs the approach as the defaults do, 0.9;
  `TacticWeights.Approach` refuses 1 or more, a negative weight and NaN, the reason in its documentation;
  `scripts/fights.cs --weights` says so instead of throwing. Tested: a character with one blow kills now a monster it
  could kill next turn too, with the defaults', the group's and the prudent weights; an approach of 1, 1.1, -0.1 or NaN
  is refused.
- **Not measured in game**: the group's fights with these weights; the owner's next test is to show the characters hitting
  when they can. 10d still sets the group's threat on its measure (D434).
- **Rejected.**
  - D454 undone whole: the threat at 0.75 costs nothing here and keeps the group bolder against monsters that hit.
  - A rule by element: one AI through the effects (D244); the blows of another element are what the damage foreseen chose.
  - Approach clamped silently in the judge: a weight that cannot work is refused where it is set, not changed out of sight.
