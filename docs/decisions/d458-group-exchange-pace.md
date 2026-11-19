# Groups, the exchange's pace and the pods seen first (D458)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's bank is
in [D442](d442-step-10-the-groups-bank.md), [D452](d452-group-bank-master-empties.md) and
[D457](d457-groups-invite-and-bank-refusal.md).

## D458: A follower says it is ready 3.5 s after the exchange's last change, and an activity starting plays nothing before its run looks at the pods

*2026-11-19, the owner's test in game of main 31a47c6a (D457's invitation seen working: the master invited at its first
step): the first exchange cancelled, one through, the next cancelled, each very slow to conclude, "quand un perso valide
faut juste attendre 3/5 s pour que l'autre puisse valider", and the master travelled to its zone before it saw its full
mule; the lead's fix, local and reversible, the owner's ask. Refines D442, D452. Builds D79, D297.*

- **Measured** on the four game connections of that run (`scripts/capture.cs --messages`, type URLs and times only):
  - Each follower moved its stacks one by one, each after the client's display time: 67 stacks in 39 s.
  - A readiness sent 0.5 s after the exchange's last change was never answered, twice; the master gave up each exchange
    30 s later. Every readiness sent 3.5 to 4 s after the last change was answered at once, and the master's, 60 ms after
    its follower's, was taken: the delay runs from the last change.
  - At its first step, the master's profile chose its state and its activity's step travelled six maps to its zone; only
    the next step saw the mule full. The pods a profile's activity watches are known to the run once that activity plays
    (`BehaviorActivity.Watches`), after the run looked at them.
- **Made**:
  - `FollowActivity`: a follower says it is ready once 3.5 s passed with no change told in the exchange, then again each
    second it is not answered, three times at most. Its stacks still go one by one, the client's display time between
    them, as measured (D442): 67 of them in 39 s, the exchange concluding.
  - `StateVisit`: an activity's node that becomes the one playing plays nothing at that step, so that the run looks at
    its pods, and its group's, before the activity's first step.
- **Not measured**: the owner looks at it in game, every exchange concluding.
- **Rejected.**
  - The stacks given whole in one `ExchangeTransferFromInventoryRequest`, as the bank takes them: still not measured in an
    exchange, as D442 rejected it, and the step of the readiness was measured only after moves one by one, which a transfer
    may make another count; the review found it. The faster way waits on a capture of the client moving many stacks.
  - The moves one by one with no display time: a request every 30 ms, which no client sends.
  - The master waiting longer for the readiness: the server never answers the one it refused.
