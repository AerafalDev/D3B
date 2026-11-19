# Groups, the invitation at once and a stack the bank refuses (D457)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The master's gate
is in [D437](d437-step-10-the-chain.md) and [D447](d447-groups-following-in-real-time.md), the group's bank in
[D442](d442-step-10-the-groups-bank.md) and [D452](d452-group-bank-master-empties.md).

## D457: The master invites its followers at its first request, and a stack the bank refuses stops not the group's unloading

*2026-11-19, the owner's test in game of main 9b7561f1 ("c'est bon, ils suivent tous", D456 seen working), then two reports:
the master went several maps before inviting the followers started before it, and at the bank it opened the bank though
empty and never took its full mule's objects, then stopped; the lead's fix, local and reversible, the owner's go. Refines
D447, D452.*

- **Seen** in the app's log of that run:
  - The master travelled six maps to its zone before inviting: since D447 its walks and map changes are not held, and the
    gate invited only as it waited before an action held, here the first door. The followers followed it all the same,
    out of its party, which a fight is locked to (D440).
  - A follower full took the group to the bank (D433), the master carrying 90 pods of 5,117. The master deposited first
    (D452): the bank took none of its one stack, and the group's unloading ended there, before any exchange; its activity
    then found the group still full and failed. The run of 02:12 ended the same way, 72 of 73 stacks put in, then none of 1.
- **Made**:
  - `GroupGate`: any request it does not hold, such as a walk, first has the master leave a party another leads and invite
    its followers out of its party, as the gate did before an action, waiting for nothing; again every 10 s while
    unanswered, not at each walk.
  - `GroupBank`: the stacks the bank leaves after a deposit, such as a quest's objects, are left in the master's bags for
    the rest of that unloading, tried again at the next, lest a bank full one day keep them out for good; a deposit the
    bank took none of no longer ends the unloading; the master opens the bank for itself only
    while it carries a stack the bank has not refused.
- **Not measured**: the owner looks at it in game: the master inviting at its first step, and a full mule emptied by its
  master, then the master's deposit.
- **Rejected.**
  - Waiting for the followers to accept before the first walk: the master would stand still, D447's slowness.
  - Leaving out a quest's objects by their type: the bank's own refusal says it for every kind, measured.
