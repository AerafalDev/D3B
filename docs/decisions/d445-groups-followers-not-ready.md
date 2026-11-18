# Groups, the followers paused in a fight (D445)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's fights
are in [D440](d440-step-10-the-groups-fights.md).

## D445: A readiness taken back while the character is not ready is the client's own, not the player's

*2026-11-18, the owner's test in game of main 7350ece5, the lead's fix, local and reversible. Refines D66, D202, D440.*

- **Measured** on the owner's test (four game connections of 3.7.14.18 through the app from 22:32, a master and three
  followers; the app's log; `scripts/capture.cs --messages`, type URLs and times only): from the second fight on, each
  follower's client sent by itself a request saying it is not ready, an empty `FightReadyRequest`, about 0.5 s after the
  bot's request to join its master's fight and before the server's answer, the character not in the fight yet. The bot
  took it for the player's hand (D202), logged "the player sent FightReadyRequest, the bot pauses", and the follower
  stayed unready until the owner resumed it by hand, three followers each fight.
- **Made** (`PlayerActions`): a readiness is the player's when it says ready, or takes it back from a character that is
  ready; taken back from one that is not, it changes nothing, and is the client's own.
- **Not measured**: why the client sends it from the second fight only, and whether its readiness option, which the
  follower turns off (D437), is the cause.
- **Rejected.**
  - Every readiness of a follower ignored: the player taking a follower's fight in hand would no longer pause it.
  - Resuming a follower by itself after its fight: the player's real hand would be overridden too.
