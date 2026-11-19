# Groups, a follower the server holds (D464)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). A walk the server
holds is in [D85](d064-d090-step-4.md#d85-a-walk-the-client-does-not-follow-leaves-the-character-where-the-server-has-it),
[D89](d064-d090-step-4.md#d89-recovery-the-clients-new-connection-and-a-walk-the-server-holds) and
[D456](d456-groups-one-cell-each.md); the exit taken with no walk in [D451](d451-groups-direct-exit.md).

## D464: A refusal out of a fight says the server may hold a walk, and a follower whose change keeps failing from one cell steps aside

*2026-11-19, the owner's report after the group runs of the day, one master and seven followers ("y'a des persos qui restent
bloqués sur une map très longtemps", "j'ai toujours un perso bloqué au bout d'un moment et hop afk fenêtre qui se ferme",
"faut corriger ça une fois pour toutes"); the root cause measured on the run's captures before any fix; the fix the
lead's, local and reversible. Refines D456. Builds D85, D89, D451.*

- **Measured** on the eight game connections of 18:47 to 20:42 and the eight of 06:56 to 07:54 (UTC), the app's installed
  captures (`scripts/capture.cs --messages`, type URLs and times; the cells and refusals decoded in a scratch tool,
  `artifacts/studies/` not written):
  - **The cost**: in both runs the group stopped at once, the master on the next map waiting for one follower, the six
    others idle, and the server closed every connection 30 minutes later for inactivity. The same follower both times,
    after a fight each time.
  - **The trigger**: the follower walked onto its exit, a step the server showed; the client confirmed 23 ms after (at
    night) and 475 ms after (in the morning), too soon for that walk, its confirmation being for its previous one. The
    server answered it, the bot took the walk for arrived and asked for the neighbour from the exit, with no walk (D451).
  - **The loop**: the server refused that change naming the walk's first cell, then each one asked again from there,
    every 8.5 s for 30 minutes (213 refusals in the morning, 207 at night). The refusal set the cell but not the hold (D456), so the
    bot never sent the walk that lets a held character go (D89); and a follower one change away from its master tries
    that change only, however often it fails (D456).
  - **Not the cause**: the pods (915 of 1104 at night), the paths, the master, the 5-minute stop of the night, which was
    the group's bank.
- **Made**:
  - `CharacterEvents`: a refusal out of a fight leaves the walk held as well as setting the cell, so that the next map
    change, door, haven bag or join steps aside first (`ReleaseHeldWalkAsync`), whose walk the server shows and lets the
    hold go. In a fight, the AI's refusals change nothing of it.
  - `MapWalk.StepAsideAsync`: the step to a free neighbour that `ReleaseHeldWalkAsync` makes, now for any caller. It
    leaves out the cell the previous walk ended on, whose one-step walk the client does not follow (9g5), and a cell that
    moves the character to another map (`MapWalk.LeadsAway`, the follower's since D453), unless no other neighbour is left:
    the review's two findings, a refusal now arming a step aside before any action that releases the hold.
  - `FollowActivity`: after three changes failed in a row by a way of one change, the follower steps aside before the
    next, whatever the server answered, so that no cause the state misses keeps a follower on one cell for good.
- **Not measured**: the owner looks at it in game: a group playing for hours, no follower stuck on its exit, no
  disconnection for inactivity. A walk's confirmation sent too soon is still taken for its arrival: the refusal that
  follows is what tells it, and the step aside what mends it.
- **Rejected.**
  - Telling a confirmation for the previous walk by its delay: measured twice only, and the morning's 475 ms is a running
    two-step walk's; a wrong guess would make every walk wait.
  - A journey round when the change keeps failing: D456 measured it going round by nine maps, the way having not failed.
  - A master that stops waiting for a stuck follower after a while: the group would go on without it, the fights locked
    to the party; a follower that never stays stuck is the fix.
  - Leaving every refusal held, in a fight too: the AI's walks are not the roleplay's, and a fight's end resets the map.
