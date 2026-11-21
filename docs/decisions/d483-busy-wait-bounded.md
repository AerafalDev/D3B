# Activities, the wait for a busy character bounded (D483)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The recovery after
an interruption is [D89](d064-d090-step-4.md); the harvest's losses and the inactivity kick, [D481](d481-harvest-losses-go-on.md).

## D483: A character busy a minute at a step's start is put back in order, and still busy a minute later the harvest or the hunt fails saying why

*2026-11-21, the lead's task after D481: a study read a second way to Ankama's inactivity kick in the harvest's step,
which waited for a busy character without end. The owner chose the answer and its reach among the options the session
put, one at a time. Builds D2, D85, D87, D89, D481.*

- **The way, read in the code**: `HarvestActivity.StepAsync` and `FightActivity.StepAsync` began by waiting 10 s for a
  character walking, using an element or in a dialog or exchange (`CharacterState.IsBusy`), or on a map not known yet,
  then stepping again. A state that never clears (a walk shown that D3B did not send and the client never confirms, a
  use whose end never comes, a dialog left open) would loop so for ever, sending nothing, until the server closes the
  connection 30 minutes on (D481).
- **Measured** on the owner's 216 game captures of 2026-11-18 to 21 (`scripts/capture.cs --messages`, directions and
  times only) and the runs' table: 17 connections end with the server closing 1790 to 1799 s after D3B's last request,
  the client still sending its own periodic requests. 3 are the stopped harvests of D481. The other 14 are two groups of
  7 on 2026-11-19 (05:56 and 18:47 UTC): after a fight the master changed map, which clears every busy state, then sent
  nothing for 30 minutes, its run never ending, while one follower asked to change map every 8.5 s, about 210 times,
  each refused: the walk the server holds of D464, decided that day. **No connection shows this wait looping**; the
  harvest's only silence over 45 s on the captures of 11-21 is the player's hand.
- **Made**, the owner's choice: a `BusyWait`, one per activity, counts from the first step that finds the character busy
  or its map unknown. Past a minute (`BusyWait.Bound`), the character is put back in order as after an interruption
  (`Recovery.SettleAsync`: a use let end, a walk settled, a dialog or exchange left, a held walk let go) and the host's log
  warns. Still busy a minute later, the activity fails with what held, which the card and the journal show (D481: the
  connection then idles until the server closes it). A step that finds the character free, or an interruption, whose
  recovery puts it in order anyway, or steps stopped over 20 s, as the app's pause between two steps makes them, starts
  the count again. A fight runs no step and is waited for without bound.
- **Reach**, the owner's choice: the harvest and the hunt, the same defect in both steps.
- **Left**: a master waiting for a follower that cannot arrive, the groups' stall measured above, is not this wait; whether
  it is bounded since D464 was not looked at.
- **Rejected.**
  - Failing at the bound, putting nothing in order: simpler, but stops a harvest that a dialog left would have let go on.
  - Putting in order at each bound and never failing: a use whose end never comes would loop again until the kick.
  - The harvest alone: the hunt keeping the same endless wait, as debt.
