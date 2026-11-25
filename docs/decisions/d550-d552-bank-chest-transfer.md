# The transfer between the bank and the guild's chest (D550 to D552)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the guild's chest of
[D147 and D153](d126-d175-step-6.md#d153-upkeep-and-the-guilds-chest-measured-mapping-format-7), banking (D80) and the recovery
ladder of [D518](d518-d519-never-idle.md).

## D550: A transfer activity moves objects between the bank and the guild's chest, either way, chosen by category, type and item, in rounds its pods allow, then says what it moved

*2026-11-25, a session of its own started by the lead session for the owner's ask: « transférer des ressources / obj de la banque au
coffre de guilde et du coffre de guilde à la banque, qu'on choisisse quoi, quelle catégorie, avec des bonnes options etc, comme une
activité ». The ask fixed the direction, the choice by category, type or item with exclusions, the quantities and the refusals; the
rest was **taken by the session**, every choice local and reversible, so no question went to the owner. Builds on D80, D147, D153,
D405, D436 and D518.*

- **What was made**: the module `Transfers` registers the activity `transfer` (`TransferActivity`, `TransferSettings`), listed in
  `DefaultModules` after the zaaps' discovery. A round goes to the source, the nearest banker (D79) or guild's chest (D153), takes out
  what the settings choose and the free pods hold, leaves, goes to the destination, puts in what it took, leaves; then the next round,
  until the source holds nothing more to move. It ends saying how many objects of how many items it moved in how many rounds, each item
  counted with its icon on the card, and the items given up as warnings.
- **What moves**: everything the source holds, the owner's second ask during the session (« en dehors des catégories ou obj seul
  faudrait qu'on puisse tout transférer même si ça nécessite plusieurs allers-retours »), or the items whose category, type or own
  id is chosen; in both, neither its type nor itself left out. A profile choosing nothing is refused by a rule, so that no empty
  choice is read as everything. **How many**: everything, at most a
  number of each item over the transfer, or all but a number of each item, which stay in the source.
- **Only what the transfer took moves.** What the character carried before stays in its bags, even an item the transfer moves.
  Before its takes, a round notes how much of each stack in the bags is the character's own; a put takes only what a stack holds
  above it, whether the inventory joined the take to the character's stack or gave it one of its own, as it does an object with its
  own effects (the review's finding: putting any stack of the item could give the guild the player's own ring). A step cut between
  a move and its answer finds the truth in the bags. After a reconnection, whose stacks have new ids, what the transfer carried
  stays in the bags, and the card says so.
- **A refusal of the destination** (a tab's type, a bound object, a full chest, the bank's silence) gives the item up for the run,
  then, as the profile says: put back in the source at the next visit, the default, so that the bags do not fill up round after round;
  kept in the bags; or the transfer stops, saying which item. A rank without the right to take out or put in, a source or destination
  out of reach once the recovery ladder is climbed (D518), or a character whose own load leaves no room ends the transfer at once,
  saying why: no restart would change them (D519 not asked for).
- **A dry run** opens the source, says what would move, item by item, and moves nothing. **Zaaps** follow the general banking rule
  unless the profile takes none (D405, D436), a zaap taken when it saves 5 map changes (D167), as the zaaps' discovery does.
- **Rejected.**
  - A kamas option: the guild's chest holds none (the client's guide, text 1164108; no capture shows any).
  - Choosing the chest's tab: changing tabs is not measured, the owner's guild having one. The transfer works on the tab the chest
    opens on, and the question stays open until a capture of a tab changed.
  - Moving what the character carried before too: it would bank a player's own loot without asking; the harvest's unloading does that.
  - Ending at the first refusal by default: one item a tab refuses would stop a long transfer of everything else.
  - The run's restart after a failure (D519): every failure left is one a new start would meet again.

## D551: Taking out of a storage is measured: a stack at a time by a negative move, the storage's stacks kept in the state, the refusals told by the client's information texts

*2026-11-25, the same session, measured on the owner's 74 game captures of 20 to 25 November, client 3.7.14.18, and the installed
client's tables. Refines D80 and D153.*

- **Measured** (client-game.md, "The game, transferred"): in 551 openings of the bank and 4 of the chest, the client took out by
  `ExchangeObjectMoveRequest` with a negative quantity, 101 times in the bank and 5 in the chest, each answered 28 ms later by the
  inventory's stack, the storage's stack changed or removed, and the weight; and by two requests the readable protocol lacks, a list
  of stacks and all, which the server cuts short to the pods with text 266. A take the pods do not hold is refused with text 285.
- **What was made**: `OpenStorage.Stacks` keeps the storage's stacks from its opening, each change and each removal, all events the
  readable protocol already reads; `StorageMoves.TakeFromStorageAsync` and `PutInStorageAsync` move one stack, or part of it, and say
  how it ended (`StorageMoveOutcome`); `GuildChest.GoToGuildChestAsync`, now with the zaap rule, and `OpenGuildChestAsync` are public,
  the opening refused at once by a text of the error kind. The transfer weighs each take with the pack's weights, so that the server
  never has to refuse one.
- **The refusals**, by the client's information messages of the error kind: 356 a bound object (D153), 654, 655 and 656 a rank without
  the right to open, put in or take out, 659, 666 and 711 no guild, one younger than 30 days, no access, 661 a type a tab refuses, 285
  and 266 the pods. What the server answers a full chest is not measured: a stack it leaves unanswered is refused.
- **Rejected.**
  - The client's list and "all" requests: a pairing of two more messages, mapping format 26 and its release, to save 28 ms a stack.
  - Reading the storage's content again at each move: the server sends each change, which the state now follows.

## D552: The types of items are a setting's kind of their own, and the categories are named by D3B after the inventory's tabs

*2026-11-25, the same session, for D550. Refines D98 and D148.*

- **Measured** (client.md, "Items"): a type's `categoryId` is the inventory's tab its items sort under: 0 equipment, 1 consumables,
  2 resources, 3 quests' objects, 4 hidden objects such as buffs, 5 cosmetics. The client names neither the categories nor the super
  types; the pack keeps 229 named types, each with its category.
- **What was made**: `SettingKind.ItemType` and `ActivitySettingsBuilder.ItemTypes`, offering the types that hold an item, by their
  names in the pack, picked in the studio as items are, with no icon; `ItemCategory` in `D3B.Data.Items` names the five categories that
  go in a storage, which the transfer's texts name in both languages.
- **Rejected.**
  - The super types as the categories: unnamed in the client, 25 of them, and the inventory's tabs are what a player knows.
  - The types as a choice of an enumeration: 229 values that change with the client's builds.
