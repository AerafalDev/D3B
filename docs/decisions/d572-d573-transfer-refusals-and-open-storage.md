# The transfer's lasting refusals, and a run begun in a storage left open (D572, D573)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the transfer between
the bank and the guild's chest ([D550 to D552](d550-d552-bank-chest-transfer.md), [D563 and D564](d561-release-1-0-0.md)) and the
busy wait bounded (D484).

## D572: An item the destination refuses for good, an object bound to the account or a type the chest's tab does not take, stays left out of the character's next transfer runs that way

*2026-11-26, a session of its own started by the lead session for the owner's report « j'ai fait bank into guild sur les mules et
guild into bank sur le main, ça a fait nimp »; the diagnosis the session's, the fix the owner's choice (« B then A »). Refines D550
and D563.*

- **Measured** on the owner's four game connections of 2026-11-26 12:52 UTC (`…E6KT3` to `…KT6`), replayed through
  `CharacterEvents.Apply` by a tool never committed that printed item ids, quantities, text ids and weights only, and the bots'
  journals: three mules from the bank to the chest and the main the other way, on the same chest at once. Every move of D3B's was
  answered but four: a mule's bound object, refused by the chest with text 356, once a run, four runs in a row. Each time it went
  out of the bank, was refused and went back, two openings of the bank for nothing; the item given up lived in the run only.
- **Made**: `TransferBags` keeps, for each character and each way, the items its destination refused with text 356
  (`NotExchangeable`) or 661 (`TypeRefused`). A run begins with them given up, so that it neither takes them nor puts them back,
  and its card still warns of each. They are kept by the item's id in the client's tables, which a new game connection does not
  change, until the host stops. The other refusals, the pods, the bank's silence, another text, stay the run's own.
- **Rejected.**
  - Keeping every refusal: a silence or a full chest may not come again, and the next run would never try the item.
  - Keeping them by stack: a stack's id changes with each connection, and the object goes back to the source as another stack.
  - Keeping them in D3B's database: a tab's types may change between two days, and the host's restart is the owner's way to try
    again.

## D573: A run begun while the character is in a storage, a dialog or an exchange leaves it before its first step, rather than wait the busy wait's minute

*2026-11-26, the same session as D572, the owner's choice. Refines D484.*

- **Measured** on the same connections: the owner stopped three mules' runs from the app with the bank open, each started again
  a second later, and once more on one of them. Four times the new run waited 61 to 65 s in the bank, D3B sending nothing, until
  its busy wait's minute (`BusyWait.Bound`) put the character in order and left it. The journals hold no restart: the stops were
  the owner's.
- **Made**: `ActivityRun`, before a run's first step, leaves through `Recovery.SettleAsync` a storage, a dialog or an exchange the
  character is in, once the bot may act. A run is started by the player, so no step of it opened what it finds open. Every
  activity gets it, profiles included; the busy wait stays for what a step meets later.
- **Rejected.**
  - Leaving the storage when a run is stopped: a stop is the player's, who may be taking the character over in that very bank.
  - Waiting a shorter while at the first step: the character would still wait, for a dialog no step will close.
  - The transfer alone: a harvest or a hunt stopped at the banker leaves the same bank open for the next run.
