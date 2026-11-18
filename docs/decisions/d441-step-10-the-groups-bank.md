# Step 10, 10c3: the group's bank (D441)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The step's design
is in [D429 to D435](d429-d435-step-10-groups.md), the group's life in [D440](d440-step-10-the-groups-life.md).

## D441: 10c3, a full member takes the group to the bank, each follower gives the master what it carries by an exchange, mapping format 20

*2026-11-18, step 10, session 10c; the master's own deposit by its profile's rule in one opening of the bank the owner's
choice among three, as the lead recommended it; the step's reading and the rest the lead's, local and reversible, within
D433. Builds D80, D325, D405, D433, D435, D437, D439.*

- **Measured** on 10a's two captures of the exchange (`scripts/capture.cs --messages --frames`, type URLs, sizes and
  times only, never a value): the follower asked, the leader was told 27 ms later and accepted, the exchange opened for
  both; the follower moved three things in, twice by `ExchangeObjectMoveRequest` and once by
  `ExchangeTransferFromInventoryRequest`, and each move was told to both 28 to 29 ms later by one event listing the stacks
  moved, unmapped until now; both then said they were ready with step 3. At the conclusion, in one millisecond: the
  follower's stacks removed or lessened and the leader's added, each told neither is ready any more, the weights, then the
  exchange left.
- **Mapped**: that event, `ExchangeObjectsAddedEvent`, with no field read, pinned for 3.7.14.18; **mapping format 20**,
  254 messages paired, no failure, the same bytes twice, the same pairs without the captures (only the evidence of three
  differs). The lead's reading, not measured: the step is how many of these events the exchange had, 3 after 3 moves, so
  each side counts its own (`PlayerExchange.Changes`) rather than its requests, a move the server refuses changing nothing.
- **The exchange in the state** (`CharacterState.Exchange`, `PlayerExchange`): asked, open, its changes, each side's
  readiness, until it is left; open, it is a dialog, so the bot is busy and its recovery leaves it.
- **When** (`GroupBank`, `IGroupBank`, `BotActor.Bank`): before each step of a master whose activity banks, the run asks
  whether a follower is full at the share of pods the master's profile banks at, the pods its activity watches (D325), or
  the master by its own activity; then the group's bank takes the unloading instead of the activity's.
- **How**: the master goes to the nearest banker, by a zaap only as the general banking settings allow
  (`BotActivity.BanksByTeleport`, D405, D436), again while busy, its gate gathering the group map after map (D437); in the
  chain's order, each follower asks it for an exchange (`ExchangePlayerRequest`), once a round; the master accepts after
  the client's display time, the follower moves in one by one (`ExchangeObjectMoveRequest`) each stack it does not wear,
  never its kamas nor the food that only heals, which it eats before its master acts (D440, the lead's choice), as much as
  the master's pods still hold (`GroupGifts`), then says it is ready naming the changes, and the master the same. The
  master waits 30 s for the asking, then 30 s for each move or the readiness, so that a follower carrying many stacks is
  not cut short. When the master's pods hold none of a follower's next stack it deposits first; a follower whose round
  gave nothing, such as objects the server exchanges not, is left, as one that never asks. Then the master deposits, in
  one opening of the bank, what it was given and what its own profile banks (`BotActivity.Deposits`, the owner's choice),
  never its kamas: the gifts by the master's stack they went into and how many, so that a gift joining a stack of the
  master's own, which its profile keeps, goes in for its quantity alone, by the move the guild's chest measured taking part
  of a stack (D153), the lead's reading for the bank.
- **Not measured**: tested on the fake game only, with the rest of step 10 measured in game at the very end (D439):
  whether the server checks the step, what it answers an exchange of more than the master's pods, a part of a stack moved
  into the bank, and the client's windows of the exchange on both screens. A master harvesting to its guild's chest takes
  its group to the bank all the same; the group's banking is not counted on the activity's card, which keeps its last
  text meanwhile, the review's finding left for 10d.
- **Needs**: the mapping of format 20 published with the release `client-3.7.14.18` on the owner's go, before an app that
  reads it (D24).
- **Rejected.**
  - The master depositing all it does not wear: a profile banking its loot alone would empty its consumables.
  - The gifts remembered by item: a gift merged into the master's own stack of that item would take the whole stack to
    the bank, the review found.
  - One wait of 30 s for the whole exchange: a follower with many stacks, a move each display time, would be cut short.
  - Two openings of the bank, the followers' gifts then the master's own unloading: the bank paid twice.
  - The step counted from the follower's requests: a move the server refuses would be counted.
  - `ExchangeTransferFromInventoryRequest` listing every stack at once: one stack a request measured in an exchange
    between characters, and a part of a stack needs the move's quantity.
