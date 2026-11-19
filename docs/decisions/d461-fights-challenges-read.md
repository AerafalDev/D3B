# Fights, the challenges read (D461)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The challenges'
bonus is in [D438](d438-fights-challenges-bonus.md).

## D461: The bot reads its fight's challenges, their targets, their results and the proposals, mapping format 22

*2026-11-19, outside the roadmap's steps, the owner's ask (« que l'IA fasse les challs qui sont jugés faisables ou
faciles »), the first of three sessions the lead proposed and the owner started: read the challenges, then play the easy
ones, then the feasible ones. Builds D178, D438.*

- **Measured** on 83 game connections of 3.7.14.18 through the app, 2026-11-18 and 19, 2,980 fights, by structure with
  `scripts/capture.cs` and each value compared on the machine by a tool of the session, never written down
  ([client-game.md](../client-game.md#the-game-the-challenges)): one challenge in every fight, named by an add event just
  before the fight's start and a list just after it; 430 changes of a challenge's targets as turns began; one result a
  fight, before its end, 1,329 won and 1,647 lost. A target is a fighter of the fight with its cell, and for 145 changes the
  one fighter allowed to strike it. In 5 placements the server proposed two challenges, 4 times at the owner's request,
  and the client chose one, which the add event then named. The server's numbers are the client's challenges' table's, 842 challenges in
  `ChallengesDataRoot`.
- **Made.** `ChallengeListEvent`, `ChallengeAddEvent`, `ChallengeTargetsEvent`, `ChallengeResultEvent`,
  `ChallengeProposalRequest`, `ChallengeProposalEvent`, `ChallengeSelectionRequest`, with `ChallengeInformation`,
  `ChallengeTarget` and the enum `ChallengeState`, in `fights.proto`; 7 pairs pinned for 3.7.14.18; mapping format 22 (264
  messages, 570 fields, 37 enums and 244 values, no failure, the same bytes twice, the same pairs without the captures).
  `FightState.Challenges` keeps each challenge with its bonus, its targets as the server last named them (a target with no
  fighter left out) and its result; the AI's log names each challenge once and its result as the fight ends. The bot
  sends none of the requests yet.
- **Left for the next sessions**: the challenges' table in the pack, with its texts and its criteria, for the studio to
  name a challenge and the AI to know what each asks; the AI keeping the easy ones, then the feasible ones; choosing among
  proposals.
- **Not measured**: the challenge state's two other values; the proposals' third field; whether a fight ever holds more
  than one challenge, as the client's mode and the references allow; a group's challenges.
- **Needs**: the mapping of format 22 published with the release `client-3.7.14.18` before an app that reads it, on the
  owner's go (D24).
- **Rejected.**
  - The challenges' table in the pack in this session: a pack format and a release for names the log does not need yet,
    its spells already logged by number; the next session needs it for the criteria, and takes both together.
  - The mode and number events (`ktn`, `kub`, `kto`): always the same value in every fight measured, nothing to read yet.
  - Keeping a target with no fighter: it names only a negative cell, nothing to strike.
