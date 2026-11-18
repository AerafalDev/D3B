# Fights, the challenges' bonus (D438)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D438: A profile chooses the challenges' bonus of its fights, the drops by default, mapping format 19

*2026-11-18, outside the roadmap's steps, the owner's ask ("le master pour les fights mette les challenges en drop", an
option of the profile for every fight); the values, the default and the place chosen by the owner as the lead recommended
them; measured on the owner's capture of that day. Builds D203, D216, D244, D431.*

- **The ask.** In every fight the AI plays, the challenges won give the drops rather than the experience; in a group, the
  master's choice is the one that counts.
- **Measured** on one game connection of 3.7.14.18 through the app, 3 min 39 s, three fights alone, described with
  `scripts/capture.cs` and each value compared on the machine by a tool of the session, never written down
  ([client-game.md](../client-game.md#the-game-the-challenges-bonus)): the client sends the choice by itself 7 to 103 ms
  after each placement starts, with the one its player made last, even in a fight the player leaves it alone, and again at
  each of the player's clicks; the server answers each with an event of the choice 27 to 29 ms later. The owner's click on
  the drops sent 1, on the experience 0, as the references name them; the build's enum has these two values alone.
- **Made.** `ChallengeBonusChoiceRequest` and `ChallengeBonusChoiceSelectedEvent` in `fights.proto`, with the enum
  `ChallengeBonus`; 2 pairs pinned for 3.7.14.18; mapping format 19 (253 messages, 554 fields, 36 enums and 241 values, no
  failure, the same bytes twice, the same pairs without the captures). `FightState.ChallengeBonus` keeps the server's last
  word.
- **The option**, « Bonus des challenges » (`ChallengeReward`): Drops, Experience, or As the client sets it, which sends
  nothing; Drops by default, profiles stored before included. In the hunt's profile it shows just before "After a lost
  fight"; in the harvest's under "The AI fights", while that is on; not under "Advanced". A list speaks for the step it
  plays (`BehaviorActivity`); out of an activity's step, with the host's `Fights:Alone` alone, and in a harvest whose AI
  does not fight, whose option is hidden, the client's choice stays (`FightsItsFights` gates it).
  A "Follow" profile (D430) sends none: the master's counts.
- **When**: during the placement, after the fight is closed (D216) and before the character is placed, the bot waits for
  the server to answer the client's own choice, at most its answer delay, then sends the profile's only when it differs,
  and waits for its answer once; unanswered, the placement goes on.
- **Not measured**: no capture holds a group, so whether the server answers or refuses a member who does not lead its team,
  and whether it tells the followers of the leader's choice, is left to the measure in game (the references' server takes
  the team leader's alone).
- **Needs**: the mapping of format 19 published with the release `client-3.7.14.18` before an app that reads it, on the
  owner's go (D24).
- **Rejected.**
  - Two values, Drops and Experience: no way to leave the choice to the client or the player.
  - One box, "Drops": the experience could not be forced, and the client keeps the player's last choice from one fight to
    the next.
  - The experience or the client's choice by default: not what the owner asked for.
  - The option under "Advanced", or in a section of its own: found later, or a category for one setting.
  - A general setting of the app rather than the profile's: the owner asked for the profile.
