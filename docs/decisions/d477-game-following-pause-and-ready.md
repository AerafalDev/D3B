# Groups, the game's following not paused and its readiness left to the server (D477)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The game's own
following is [D476](d476-groups-game-following.md); the player's hand, [D66](d064-d090-step-4.md).

## D477: A follower the game moves is not paused by its client's walks, and leaves its readiness to the server

*2026-11-20, step 10, the owner's report from the first game of D476: "some characters pause", and "no need to set ready
on each character when the party option is on, ready on the main is enough". Refines D476, D66, D434.*

- **Measured** on the owner's game of 2026-11-20, 07:12 UTC, four connections through the app with D476
  (`scripts/capture.cs --messages`, structure only):
  - **The game follows map changes**: the three followers' clients sent 10 to 11 walks and 3 to 4 map changes each by
    themselves, after their master's; D3B sent one walk of its own. Each was on its master's map within a second.
  - **Paused**: the bots took those walks for the player's hand (D66) and paused two of the three followers; the master
    then waited for them, "Paused".
  - **Ready**: D3B said each follower ready, 0.8 to 1.1 s before its master; the game of 05:53 UTC had measured the server
    setting the followers ready in the millisecond their master was (D476), so that those readinesses were needless.
- **Made**:
  - A character that follows its party's leader by the game's option (`CharacterParty.FollowsLeader`), out of a fight:
    its client's walks and map changes are not the player's (`PlayerActions`). Its other actions still are.
  - In a group the game follows, a follower in its master's fight places itself once and says nothing more
    (`IFightTeam.LeavesReadyToTheGame`, `IsPlaced`); its master waits for it placed, not ready, then says it is ready,
    and the server sets the followers ready with it. Its being placed wakes the master's wait (`GroupDirectory.Touch`).
  - A follower that joined its master's fight by request, its options off, says it is ready itself in that fight: the
    server set it ready only for options on (the fresh review's finding).
- **The cost**: the player's own walk of a follower that follows by the game pauses nothing, and the follower, its
  following lost meanwhile, asks again within 10 s (D476). To take such a follower in hand, the player unticks its
  following in the party's window first: its walks then pause the bot, as any other's do.
- **Rejected**:
  - Telling the client's walks by their delay after the leader's place, 4 to 6 ms: a map change follows the client's walk
    by up to 200 ms, and a window would fail under load.
  - Keeping the followers' readiness: needless, the owner's ask.
