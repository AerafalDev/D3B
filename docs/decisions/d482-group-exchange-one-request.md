# Groups, the exchange's stacks in one request (D482)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's bank
is [D442](d442-step-10-the-groups-bank.md), its readiness's pace [D458](d458-group-exchange-pace.md) and its rounds
[D459](d459-group-bank-rounds-and-food.md).

## D482: A follower moves its whole stacks into the exchange in one request, a part of a stack by a move

*2026-11-21, the lead's task after the study of cheap improvements (its item 11), on captures the owner made for it.
Supersedes D442 and D458 in part: their rejection of the transfer, for want of a capture of the client moving many stacks.*

- **Measured** on the owner's captures of 2026-11-21 (18:28, 18:32 and 18:43 local time, the owner moving stacks by hand in
  exchanges between two of their characters; `scripts/capture.cs --messages --frames --schema --mapping`, structure only):
  inside an exchange with a player the client moved many stacks with `ExchangeTransferFromInventoryRequest`, the request
  the bot sends the bank, of the same shape: one packed list of ids. The server answered each of its 31 with one
  `ExchangeObjectsAddedEvent`, 28 to 169 ms after, listing up to 33 stacks; a single stack went either that way or by
  `ExchangeObjectMoveRequest`. The bot's state needs nothing more: it counts those events, the step its readiness names
  (D442), and one request makes one. That the server takes that step after a transfer of many, D458's doubt, is the
  lead's reading, not measured: the captures' values are not read.
- **Before**: one `ExchangeObjectMoveRequest` a stack, the client's display time between them: 54 s for 86 stacks (D458).
- **Made**: the follower sends the stacks it gives whole in one `ExchangeTransferFromInventoryRequest`, then each stack it
  gives in part, as much as its master's pods hold, by an `ExchangeObjectMoveRequest`, the only request that names a
  quantity, the display time between requests. What it gives is unchanged (`GroupGifts`: not what it wears, never its
  kamas, its food as D459 says, not the stacks an exchange left), and its readiness still waits 3.5 s after the last change
  (D458). The master's reckoning of what the exchange left (D459) reads it from the follower's inventory, as before.
- **Not measured, and guarded**: whether the server, in an exchange with a player, takes part of a transfer as the bank
  does, or refuses it whole for one stack it may not take, such as one bound to the account (D153). A transfer of several
  stacks answered by no change within the answer's wait goes again a stack a move, the review's finding: else such a
  stack would keep the follower's whole load out of every exchange of the unloading.
- **Left**: an empty request the build does not pair came before 16 more of those events, each 28 to 41 ms after it; what it
  moves, perhaps all the inventory, was not measured, and the bot does not need it.
- **To see in game**: a mule emptied in about a second of moves, then the 3.5 s of D458, and the exchange concluding; a
  mule carrying an object bound to the account, to see which way the server answers.
- **Rejected.**
  - Keeping a move a stack, with a shorter wait: each move still takes a round trip and the display time, and the client
    itself sends one request.
  - The empty request not paired: not measured, and it may move what D459 keeps, such as the food.
  - Every stack by the transfer, a part of one included: the request names no quantity, and would give more than the
    master's pods hold.
