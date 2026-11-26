# Profiles, never idle until the server's kick: the behavior speaks for its activity, a quest's objects left out of deposits (D567, D568)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The restart it makes
work for profiles is [D519](d518-d519-never-idle.md#d519-a-run-that-fails-or-makes-no-progress-for-ten-minutes-puts-the-character-back-in-order-and-restarts-its-activity-after-30-s-doubling-to-8-min-six-times-an-hour-at-most-a-failure-no-restart-would-change-or-a-stop-still-ends-it);
the guild's chest, [D147](d126-d175-step-6.md); the behavior that runs a profile, [D297](d293-d358-step-7.md).

## D567: A profile's behavior speaks for the activity its step plays in every way an activity can say something, its restart after a failure included, and the run watches its progress from the start

*2026-11-26, a session of its own, the owner's report (« J'ai été déco sur tout les persos […] je pensais on avait réglé le
problème […] corrige le une fois pour toute »); the diagnosis and the fix the lead's. Refines D519 and D297. Builds D529, D533,
D565.*

- **Measured** on the owner's four journals and captures of 25 November (`…B6VNLRAQ4` to `…AQ7`, `scripts/capture.cs
  --messages`, types and times only): four harvesters of four accounts, each a studio profile, failed their unloading, the
  journal saying `Failed`, `StillFull = True`, `GaveUp = False`, and no `ActivityRestartedEntry`; then no request of D3B's until
  the server closed each connection 29 min 50 s later.
- **Why**: a profile runs as `BehaviorActivity` (D297), the run's activity, which passed on its step's activity's report,
  fullness, unloading and fights, but not `RestartsAfterFailure`, `Restart`, `WaitingUntil`, `Keeps`, `OnGroupUnloaded`,
  `MountRaces`, `CapturesSouls`, `DungeonInside` nor `BanksTheGroup`. Every profile answered no to the restart; and the run
  started its progress watch only for an activity that restarted as it began, which a behavior has none of before its first
  step. D519 was tested on `HarvestActivity` run alone, never through a profile, which is how the app runs every one.
- **Made**: `BehaviorActivity` passes on every member to the activity its step plays; `Restart` begins that activity anew,
  the behavior keeping its state and its place in a list. `ActivityRun` watches every run's progress from its start, an activity
  that would not restart excused for as long as it says so. `BehaviorActivityRestartTests` plays a profile whose activity fails,
  and one that makes no progress, both restarted; and checks by reflection that the behavior overrides every virtual member of
  `BotActivity`, so that one added later cannot be lost for profiles again. The same loss also left out, for profiles, the soul
  stones (D565), the mounts' capture (D533) and the dungeon's keys kept (D529), which this mends without testing them in game.
- **Two players' reports the same day** come of it: a profile's master in a dungeon whose follower stayed at its door, finding
  no way to it (D528 reads `DungeonInside` of the master's run), and a profile capturing Volkornes that never cast its net (D533
  reads `MountRaces`). `AProfilesRunSaysWhatItsActivitySaysOfItself` reads each member through a profile's run.
- **Rejected.**
  - Restarting the whole behavior, its first state again: a list of steps would lose its place (D421), which D519 kept.
  - Passing on only the restart: the next member added to `BotActivity` would be lost the same way, as six were.

## D568: A deposit leaves out a quest's objects, which neither the bank nor the guild's chest takes, and what weighs nothing, and a harvest unloaded into the chest goes on when the bank then takes none of the rest while its bags are no longer full

*2026-11-26, same session, the lead's; what weighs nothing left out the owner's ask (« et ceux de poids nul faudrait les exclure
aussi »). Refines D147, D452, D518 and D519. Builds D80, D457.*

- **Measured** on the same four captures (a throwaway decoder of the scratchpad printing items, types and texts by id): the
  guild's chest took every stack but ten items of the quest's category (3), refusing each with text 356; the miner's weight went
  from 7,035 to 135 of 7,775 pods. The bank then answered nothing to a list of those ten alone, which D3B takes for refused, seven
  times up the ladder (D518); the harvest ended there, its bags at 2 %. Before the chest, the bank took the rest beside them,
  and they went unseen.
- **Made**: `StoredItems.TakesInStorage` says no for an item of the quest's category, an item the tables lack still asked;
  `StoredItems.IsDeposited` also says no for an item of no weight, which leaves the pods as they are, as BillBot left it, though
  the chest took some on the captures. The harvest's, the hunt's, the dungeons' and a group master's deposits leave such stacks
  out. The harvest's unloading is done when the bank took what
  it may, or took none of what is left while the character is no longer full, as the owner's BillBot judged its unloading by the
  pods; the ladder no longer climbs for it.
- **Not made**: measured on the same captures, the client's own two requests to leave, 5 and 40 ms after an exchange ended,
  close the banker's dialog D3B opens 1 ms after it (the `NoAnswer` every other rung in the journals). A failed rung the ladder
  tries again, so no kick comes of it; a follow-up.
- **Rejected.**
  - Leaving out a stack once the bank refused it, as the group's bank does (D457): the first unloading would still fail on it.
  - Every unloading judged by the pods alone: an unloading always starts full, so only the chest's leaves room for it.
  - Leaving out the objects bound to the account or the character: the captures hold no refusal of one, and D3B reads no
    object's effects; the chest refusing one sends it to the bank (D147), the pods judging the rest. A follow-up, measured first.
