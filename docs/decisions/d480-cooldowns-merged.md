# Fights, the server's cooldowns merged (D480)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The server's
cooldowns are read since [D193](d176-d208-step-9.md); the Zobal's masks are [D224](d209-d252-step-9.md).

## D480: The cooldowns the server gives are merged spell by spell, not replaced

*2026-11-21, step 10, the owner's ask to audit the captures of 20 and 21 November ("pas mal d'incohérences"), the lead's
triage, this session's measure. Refines D193; builds D224.*

- **Measured** on the 130 game connections of 2026-11-20 and 21 (`scripts/fights.cs --live --details`): of the 12,791
  casts the AI asked, the server refused 184 with text 175, 166 of them a Zobal's Intrépide mask (spell 13386) and 1 its
  Pleutre mask (13387), D3B's rules allowing each. Putting on a mask, the server sends `RefreshCooldownEvent` twice, each
  naming one spell alone, the old mask's then its variant's (13386 then 13409, waiting a round); at a turn's end it names
  every spell it counts down, those back at 0 too. The bot replaced a fighter's cooldowns with each event's, so that the
  second event erased the first, and the AI cast the old mask again in the same turn: 133 refusals in the first round.
- **Made**: each event's cooldowns are set into the fighter's, spell by spell, the spells it leaves out kept. Replayed on
  the same captures, the 167 refusals are on a cooldown the bot now holds, and none of the 12,605 casts the server
  announced is. Test: `FightEventsTests.ACooldownTheServerGivesAloneKeepsTheSpellsItLeavesOut`.
- **The rest of the audit**, measured the same way, nothing changed for it:
  - 17 refusals of the Pandawa's Karcham (12787), on allies and enemies its rules allow and on whom it was cast 300
    times more: cause not found.
  - The fight's options: texts 95 and 40 are of the message kind, the server saying the fight closed and the spectators
    refused (D216), not refusals.
  - 238 of the AI's turns with no cast nor walk: 179 with the nearest enemy 5 cells away or more and no spell landing
    from the character's cell, the turn ended by D3B with MP left; 5 with the enemy nearer and no spell landing; 45 with
    a spell landing that the AI did not cast; 8 of a follower whose turn the server ended within 0.1 s without a turn
    start; 1 on no cell. On the 107 connections after D472, 224, 167 of them far and with no spell landing. The judge's
    choice of passing rather than nearing, left for a session of its own.
  - 3 attacks unanswered: the monster group walked as D3B attacked it, the bot walking again and moving on.
  - The longest wait between a fight and the next attack, 1,153 s, was the player's hand: the client asked 102 moves in
    exchanges, 14 exchanges and 14 map changes meanwhile. The waits of 4 to 5 minutes are the group's bank (D442, D458).
  - `scripts/fights.cs --live --details` now counts each cast refused by its spell and by what D3B's rules said of it,
    each idle turn by what it held as it began, and lists the attacks unanswered and the waits of over a minute.
- **Rejected**:
  - Keeping a snapshot per event and decreasing it at each turn: the server already names every spell it counts down,
    and no merged cooldown stood on a cast it announced.
  - Leaving the spell refused out of the turn by `MostRefusedATurn`: the bot would still try it on every cell it walks to.
