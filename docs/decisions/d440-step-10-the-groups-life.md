# Step 10, 10c2: the group's life before an attack (D440)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The step's design
is in [D429 to D435](d429-d435-step-10-groups.md), the group's fights in [D439](d439-step-10-the-groups-fights.md).

## D440: 10c2, the master attacks once each follower's life is over its profile's share, each follower healing by itself

*2026-11-18, step 10, session 10c; the follower eating what only heals, else regenerating, the owner's choice among three,
as the lead recommended it; the rest the lead's, local and reversible, within D433. Builds D148, D153, D325, D433, D437.*

- **The share** (`GroupLife.ShareOf`): the life the master's own profile heals under, the life its activity watches
  (`BotActivity.Watches`, D325), so that a hunt, a harvest whose AI fights and a list's step give it alike. A profile that
  heals not gives none, and the master waits for no follower's life, as it heals not its own.
- **The master's gate** (`GroupGate`, D437): before any action it holds, once every follower is with it, it waits for each
  follower whose life is under the share, its card saying "who regains its life" (`MemberAbsence.Healing`), as an activity
  heals before its step. A life rises with no message, by the bot's reckoning (D153), so the gate looks again as the first
  follower's points are due.
- **The follower heals by itself** (`FollowActivity`), with its master: once under the share, it eats until its life is
  whole the food it carries that only heals, smallest first, as a profile choosing no food does (D148); with none, its life
  regenerates up to the share (9g5), its master moving ending that wait as its other waits. The owner's choice: no setting
  more in "Follow".
- **Not measured**: tested on the fake game only, with the rest of step 10 measured in game at the very end (D439).
- **Rejected.**
  - The follower regenerating only: slower, and the food it carries left unused.
  - "Follow" with its own heal and food settings: a setting more than D433 wanted.
  - A fixed share for the group, or the follower's own: the master's profile already says when its characters fight.
  - The life waited for before the attack's request alone: both attacks send it once the master stands on the group's
    cell, which the group leaves within seconds (9g), so the attack would go unanswered after the wait, the review found.
