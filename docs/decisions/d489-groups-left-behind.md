# Groups, a follower left behind (D489)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The master's gate
is [D437](d437-step-10-the-chain.md); the follower the server holds, [D464](d464-groups-held-follower.md); the wait for a
busy character, [D484](d484-busy-wait-bounded.md).

## D489: A master goes on without a follower missing five minutes, but fighting or healing, and waits for it again once it is back

*2026-11-21, the lead's task after D484, which left the groups' stall of 2026-11-19 unlooked at. The owner chose the
answer and the bound among the options the session put. Supersedes D437's wait "as long as it takes" and the option D464
rejected. Builds D87, D432, D447, D464, D484.*

- **Read in the code**: the master's gate (`GroupGate.GatherAsync`) waited with no delay for every follower missing,
  disconnected, paused, fighting, on its way, out of its party or healing. D464 mended the measured cause on the
  follower's side (a step aside after three failed changes), but a follower whose step aside does not help loops on, and
  its master sends nothing until the server closes every connection of the group 30 minutes on.
- **Measured** on the owner's 216 game captures of 2026-11-18 to 21 (D484): the only master's stalls are the two groups
  of 2026-11-19 (05:56 and 18:47 UTC), both D464's held follower, 14 connections closed for inactivity; none after D464.
  The fight's placement and the group's bank were already bounded (D448, D442); the gate alone was not.
- **Made**, the owner's choice: a follower missing for five minutes of one wait (`GroupGate.LeaveBehindAfter`),
  disconnected, paused, on its way, stuck or out of the party, is left behind. The master acts without it, writes "Goes on
  without …" in its journal and warns in the host's log; the follower still comes after it, invited again if out of the
  party, and the master waits for it again once it finds it with it, at any request, or on another map than the one it was
  left on: on its way again, such as a map behind its master walking on (the review's finding). A follower left behind
  takes the group to no bank and gives nothing there (`GroupBank`). A follower fighting or healing is waited for without
  bound, as a fight and a heal end by themselves; a fight starts its count again. Tested on the fake game only.
- **Not measured**: the owner looks at it in game only when it happens: the journal's "Goes on without", the group going
  on, the follower back later. A follower left behind walks alone, so it may be attacked and fight alone; it is not
  joined to its master's fights until it is back.
- **Rejected.**
  - Failing the master's activity at the bound, saying why on its card (D484's way): only the diagnosis is gained, the
    connections still idle until the server closes them.
  - Keeping the endless wait, D464's status quo: any cause the follower's fixes miss stops the whole group for good.
  - Two minutes: a client connecting again or a slow way may go past it. Ten minutes: patient, but nothing more measured.
