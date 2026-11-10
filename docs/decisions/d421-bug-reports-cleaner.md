# Bug reports, the cleaner (D421)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D421: A report's capture is cleaned by value: the ticket and the client's opaque strings by shape, the account's characters' names wherever they occur

*2026-11-10, step BR2, the owner chose by value for the names (session BR1's question), the rest is the lead's and reversible. Refines D410, builds D409, D63.*

D410 said the cleaner overwrites the ticket and "the fields that name the account (the character list and selection)", leaving the measure to
BR1. BR1 measured it on 60 captures ([client-game.md](../client-game.md#the-game-what-names-the-account)): the ticket is once, but the chosen
character's name is 170 times in 8 messages, and the client sends two opaque strings that look like secrets.

- **Names, by value.** The cleaner reads the connection through the build's translation, collects the name of every character of
  `CharacterListEvent` and `CharacterSelectionEvent`, and overwrites each string field of either side equal to one of them, then each remaining
  occurrence of a name of 6 bytes or more in the raw bytes (a shorter one could meet other data). Fields are renumbered at each build, so no field
  list would hold; a value does.
- **Secrets, by shape.** In a frame the client or D3B sent, a string field of 32 characters or more made of digits and `a` to `f` is overwritten:
  the ticket, `kod` and `kso` of 3.7.13.17, and what a next build adds of the same kind. Every other occurrence of the same value, either side, goes too.
- **Same length.** Each is overwritten by a filler of its own byte length (a name by a distinct ASCII name, a secret by zeros), so no length and
  no frame changes and a replay reads the copy as it read the original. The player's file is never touched.
- **It fails closed.** A capture whose list cannot be read, no list or a build with no mapping, is not cleaned: the cleaner says so and the
  report cannot carry that capture, instead of carrying names. The result says how many names, secrets and fields were overwritten, so the
  dialog can say what was removed.
- **What stays**, and the dialog says by structure: the other players' names the server sent (the game's data), the numbers a frame holds
  (the character's id), timestamps, and GUIDs whose meaning is not known (`lve` is stable per character). The tests look for the ticket's and
  the names' bytes in every output; a real capture is never a fixture (D24, D63).
- **Rejected.**
  - The list's and selection's fields only: it leaves the name 170 times.
  - A field list pinned per build: the fields are renumbered at each build and a new build leaks until measured.
  - Hiding every name: the other players' names are the server's data, cannot be told from monsters' or NPCs' by structure, and a replay of a
    fight reads them.
  - Detecting the opaque strings by their message: its code changes with the build.
