# Groups, the challenges' bonus of the followers (D446)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The bonus is in
[D438](d438-fights-challenges-bonus.md).

## D446: A follower chooses the challenges' bonus its master's profile chooses

*2026-11-18, the owner's test in game of main 7350ece5; the master's choice for the followers the owner's choice between
two, as the lead recommended it. Supersedes D438's follower sending none. Builds D438, D440.*

- **Reported** by the owner: the bonus set to the drops gave the experience in game, though the app's log said the master
  chose the drops and the server answered, five fights in a row.
- **Measured** on the owner's test (four game connections of 3.7.14.18 through the app from 22:32, a master and three
  followers; `scripts/capture.cs --messages`, type URLs, sizes and times only): in each fight the master's client sent its
  own choice with no value set, the experience, as the placement started, and the server answered it; D3B's request then
  carried a value, the drops (1, as D438 measured the owner's click), and the server answered it with that value. Each
  follower's client sent its own choice too, the experience, as it joined, and the server answered each follower; the
  master was told nothing of theirs. So the bonus is each character's own, and the enum's values are not swapped: the
  followers fought for the experience, their clients' default.
- **Made**: a follower's challenges' bonus is its master's profile's (`FollowActivity.ChallengeReward`), the client's
  while the master runs no profile; the AI sends it as for any profile, once the server answered the follower's client
  (D438).
- **Not measured**: which reward the fight's end gave each character with its own bonus; the owner looks at it again in
  game.
- **Rejected.**
  - A bonus of its own in "Follow": a setting more, where the owner wants the whole group on one bonus.
