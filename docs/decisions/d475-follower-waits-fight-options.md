# Groups, a follower waits for its master's fight's options (D475)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The fight's lock
kept from one fight to the next is [D471](d471-groups-party-kept-and-lock.md).

## D475: A follower joins its master's fight once the server listed it open

*2026-11-20, step 10, the release of the app's 0.9.5 stopped by its tests, the owner's go to fix it first. Refines D471.*

- **Found**: the release workflow of `v0.9.5` failed on
  `GroupFightTests.AMasterOpensTheFightTheServerKeptClosedAndItsFollowerJoinsItOnceOpen`, which failed every time run
  alone and passed with its class. The follower sent its join while its master's fight was still closed: it read the
  fight open as soon as the master's placement began, before the server had said anything of its options.
- **Measured** on the 92 game connections of 3.7.14.18 on the machine (`scripts/capture.cs --messages`, structure only):
  each of the 3,109 placements that began lists the team's four options, after the placement's cells and before the
  turns' order, in the millisecond of the fight's join. Between the two, a fight kept closed from the last looks open; a
  join sent then is refused (D471), and the follower never sends another for that fight.
- **Made**: `FightState.IsOpen` is true once the server listed the option that closes the fight, and listed it off. A
  follower joins only then, and its master's state seen changing from not open to open wakes it. The tests' placement
  lists the four options after its cells, as the server does.
- **Rejected**: waiting for the turns' order, which follows the options but says nothing of them; sending the join again
  after a refusal, which the server answers with an event not mapped (D471), so that a refusal could not be told apart
  from a slow answer.
