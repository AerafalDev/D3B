# Step 10, 10c1: the group's fights (D439)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The step's design
is in [D429 to D435](d429-d435-step-10-groups.md), the chain in [D437](d437-step-10-the-chain.md).

## D439: 10c1, a follower joins its master's fight by request, the master ready once its followers are placed

*2026-11-18, step 10, session 10c; 10c in three stacked pull requests the owner's choice among three, as the lead
recommended it; the measure in game at the very end the owner's choice; the rest the lead's, local and reversible, within
D431 and D434. Builds D216, D431, D432, D434, D435, D437.*

- **10c in three pull requests**, one behavior each, each branch on the one before: the group's fights (this one), the
  life before an attack, then the group's bank (D433).
- **Measured in game at the very end**, the owner's choice of 2026-11-18: 10b, the challenges' bonus in a group and 10c
  are looked at together once 10c is made; until then each is tested on the fake game only.
- **A follower joins** (`FollowActivity`): told that its master is in a fight on its own map (`CharacterState.PartyFight`,
  D435), while its master's own state says the fight is in its placement, it asks to join it by the fight's id and its
  master's (`FightJoinRequest`), once a fight and a connection, so that a join the server leaves unanswered is not sent
  again. The AI then places it and says it is ready, as it plays every fight of a follower (D198).
- **Each bot's part in its group's fights** (`IFightTeam`, `FightRole`, `GroupFight`, `BotActor.Team`), asked by the AI
  during the placement: alone, it closes its fight then refuses spectators, as before (D216); a master refuses spectators
  only, its fight left to the party the server locks it to (D435); a follower sets none of its team's options, which are
  its master's.
- **The master ready once its followers are placed** (D434): after its own placement, it waits for each follower
  connected, not paused and on the fight's map that is out of the fight or in its placement not yet ready, then says it is
  ready. The fight starting ends the wait, as 45 s do, the AI's wait for a placement.
- **What 10a left, not needed**: the end of a member's fight, which no message measured says, is read from the master's own
  state, its bot in the same host; the fight shown on a map, which a follower arriving late would read, is not: the gate
  keeps every follower on its master's map before an attack (D437), and a follower connected again during the placement
  joins it from the party's event.
- **After a lost fight**, the chain gathers by 10b's rules: a follower brought back elsewhere goes to its master's map,
  and the master waits for it after its next map change (D432, D437); tested on the fake game.
- **Not measured**: whether the server takes the join of a follower whose master refused spectators first, whether it
  answers a follower's toggle of its team's options at all, and how long a placement lasts with followers joining.
- **Rejected.**
  - The master ready as soon as it is placed: the server starts a fight once every fighter in it is ready, a follower not
    yet joined not among them, and the fight would start without it.
  - A follower setting its team's options as the master does: a non-leader's toggle is not measured, and its closing the
    fight would keep the followers after it out.
  - Reading the fights shown on a map for a follower arriving late: a mapping for a case the gate already keeps from
    happening.
