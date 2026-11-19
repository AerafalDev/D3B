# Groups, one cell each (D456)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Following in real
time is in [D447](d447-groups-following-in-real-time.md) to [D453](d453-groups-door.md); a walk the server holds in
[D85](d064-d090-step-4.md#d85-a-walk-the-client-does-not-follow-leaves-the-character-where-the-server-has-it) and
[D89](d064-d090-step-4.md#d89-recovery-the-clients-new-connection-and-a-walk-the-server-holds).

## D456: No two bots of the host walk to one cell at once, a walk the server holds is let go only by a walk, and a follower never goes round while its master is one map away

*2026-11-19, the owner's report after the test of main 22d0e426 ("les followers mettent encore beaucoup trop de temps à
suivre le maître, et il y en a toujours un qui est plus à la traîne", the Huppermage never entering the bank, which every
character enters alone); the root cause sought before any fix, the owner's ask; the design the lead's recommendation, the
owner's choice ("faut juste que les suiveurs usent pas le même path que le master mais qu'ils le suivent"), the detours'
rule the owner's ask. Refines D85, D89, D447, D451, D453. Builds D74, D437.*

- **Measured** on the owner's runs of the night (one master, three followers, each in its own client; the app's logs of
  00:54, 01:25 and 02:12, and the 23 game connections of 22:33 to 02:12, `scripts/capture.cs --messages`, type URLs, sizes
  and times only; `artifacts/studies/groups-following-lag.md`, local):
  - **The trigger**: walks of a character the server held no walk of, to a cell another bot of its map was already walking
    to: the clients followed 9 of 28; to a free cell, 201 of 207. Since D451 and D453 the followers take their master's way
    out each through `MapChange`, and three of them standing on one cell chose the same nearest exit (252 to 405), the same
    door's cell (33 to 287 at the bank), and the same step aside (287 to 301).
  - **The cost**: after a walk not followed, the next was followed 4 times in 74, refused 53. A stay on a map with such a
    walk lasted 20 s halfway, 187 s at most, against 3.6 s; three changes then failed and the follower's journey went round,
    9 map changes where its master had made 1.
  - **The bank**: the follower that lost the race to both of the door's cells was refused a walk, which the state read as
    the hold over; standing for the bot on the door's cell, it used the door with no walk, refused 25 times
    (`InteractiveUseErrorEvent`), until the owner moved it by hand. Not the character's class: the one that lost the race.
  - **Not the cause**: D77's half second (the clients followed 162 of 170 first walks on a map), nor the bots' pace (a
    follower's request 1.1 to 1.8 s after another character left its map, halfway).
- **Made**:
  - **One cell each** (`MapMates`, one per host, `Bots`): every walk of a bot (`MapWalk`) claims the cell it ends on before
    its request goes, under one lock for the host, and waits while another bot claims it or walks to it, 5 s at most; a
    bot merely standing on the cell holds no one up (walks to such a cell followed 3 times of 4). A map change chooses the
    nearest exit no other bot stands on, walks to or claims, and keeps it claimed until the character left, so that a
    door's cells are taken in turn; a step aside, and a follower's cell beside its master (D448), keep clear of them too.
  - **The hold**: a refusal says where the server has the character, no longer that the walk was let go
    (`CharacterEvents`); a map change or the haven bag whose step aside did not let the walk go is not asked
    (`NotWalked`), so that the next request is a walk from where the server has the character.
  - **No detour** (`FollowActivity`): a follower whose master is one map change away takes that change only, however
    often it fails, saying it is stuck meanwhile; a whole journey only when its master is further.
- **Not measured**: the owner looks at it in game: every follower keeping up, through the bank's door.
- **Rejected.**
  - The followers leaving in a chain, one after the other: no two at one cell, but the slowness the owner refused (D447).
  - The game's own following of the leader: neither mapped nor measured (D431, D435), and it says nothing of doors.
  - Copying the master's cells: four walks to one cell, D448 measured, none followed.
  - A longer delay before each walk: the first walks on a map were followed; the race, not the pace, is the cause.
  - Waiting for a bot standing on the cell: a master would wait on its followers' cells.
