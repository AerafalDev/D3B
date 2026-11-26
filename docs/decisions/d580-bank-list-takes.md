# The bank's takes by list, mapping format 27 (D580)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The session D577 asked for,
after the owner's report of 26 November (D575 to D579).

## D580: The bank's list take, its take of everything and their answers are read, mapping format 27 published first, and the transfer takes out of the bank by a list

*2026-11-26, the bank's list takes session, the owner's choice « Poser et retirer en liste » (D577). Builds D24, D80, D551, D577.*

- **Measured** on the owner's 103 game connections of 20 to 26 November, client 3.7.14.18, by a probe of the session's
  `artifacts/studies/` (git-ignored) printing only numbers ([client-game.md](../client-game.md#the-game-transferred)): the client
  takes a list out of the bank 70 times, by a packed list of the storage's ids, and everything 27 times, by a request whose one
  field, an enum of seven values, no capture sets. The server answers 28 ms later with the storage's stacks removed, the
  inventory's added and the weight. A stack taken whole keeps its id in the bags and never joins a stack of the same item there,
  as the bags held one 9 times. When the pods hold only part, it takes stacks whole, then of one more what fits, which stays in
  the bank with its new quantity and comes in as a new stack, then says so with text 266.
- **The pace.** Text 268 refused 39 of the player's bank requests, all sent 0.18 to 0.25 s after the last; none sent 0.25 s or
  more after it, and D3B's moves wait 0.5 s (`Delays.Display`). Text 267 refused 4 lists the inventory could take none of.
- **Made.** `ExchangeTransferToInventoryRequest`, `ExchangeTransferAllToInventoryRequest`, `StorageObjectsRemovedEvent` and
  `ObjectsAddedEvent` in the readable protocol, pinned on 3.7.14.18, `MappingFile.FormatVersion` 27; the state removes and adds
  the stacks they name; `StorageMoves.TakeFromBankAsync` sends a list of the open bank's stacks and says whether the pods cut it
  short. `mapping-27.binpb` goes to the release `client-3.7.14.18` before any app that reads it (D24).
- **The transfer.** From the bank, a round takes by one list the stacks it moves whole, weighed so that the pods hold them, then a
  stack at a time what it takes in part; a list the bank refuses goes a stack at a time. In the guild's chest every move stays a
  stack at a time, as the client's.
- **Rejected.**
  - The take of everything for a transfer of everything: it would take what the run gave up, and what the pods cut would be the
    server's choice, not the round's.
  - Leaving the take of everything out of the readable protocol: the captures' listings name it, at no cost to the bot.
