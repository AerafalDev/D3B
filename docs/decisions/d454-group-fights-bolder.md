# Fights, a little bolder in a group (D454)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The AI in a group is
in [D434](d429-d435-step-10-groups.md#d434-the-ai-unchanged-in-a-group-then-measured).

## D454: Beside allies of its group, the AI weighs the enemies' threat less and its next blows more

*2026-11-19, the owner's ask from game ("faudrait aussi qu'en combat de groupe les personnages soient un poil moins sur
la défensive"), no detail chosen among the lead's four; the figures the lead's reckoning, local and reversible, ahead of
10d's measure. Refines D434. Builds D198, D203, D390.*

- **Seen**: the app's log of the owner's group fights judged none of them risky, so the defensiveness came from the
  AI's own weights, not its prudent ones (D198).
- **Made** (`TacticWeights.Group`, `FightPlay.WeightsOf`): in a fight where another living character, not a summon,
  fights on the character's team, the AI weighs the life the enemies may take next at 0.75 instead of 1, and the damage
  it may deal next turn at 1.1 instead of 0.9: the enemies' blows spread over several allies. A fight judged too risky is
  still played prudently; a character alone plays as before.
- **Not measured**: the figures are a reckoning; 10d measures the group's fights on captures and sets them (D434), the
  owner judging them in game until then.
- **Rejected.**
  - Waiting for 10d's measure: the owner asked for it now.
  - The prudent weights left out in a group: a fight the AI would lose is no less lost beside allies.
