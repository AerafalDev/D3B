# Fights, the search's budget a limit, an unanswered cast and the weapon in a state that prevents fighting (D509 to D511)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The search is
[D201](d176-d208-step-9.md)'s, its threads [D508](d508-fight-searches-off-the-pool.md)'s; the unanswered cast left out of the
fight is D203's; the summons as a last resort, D408's.

## D509: The search's budget bounds the whole search, its passes and its reply, the first cast and two ends always weighed

*2026-11-24, F1 of the next release's study, the owner's scope of the release; the lead's choice. Refines D201, D203, D408.*

- **Found.** The 800 ms of `TurnSearchLimits.Budget` were read only in the depth loop. The 16 finalists' reply and next
  turn after it had no budget, and the summons' last-resort pass (D408) started a budget of its own after the first. Measured
  with D508's harness on the 331 turns of the owner's captures of 24 November from 04:16, one search at a time on 16 cores:
  the reply weighed 60% of a search's time (284 ms halfway against 185 for the depth loop, 629 ms at most), the plans 478 ms
  halfway and 1,297 ms at most, 9 over 800 ms. On 4 cores with 4 to 8 bots, the searches took 2 s halfway and up to 5.5 s.
- **Made.** `SearchClock`, one per search, read on `TurnSearchLimits.Clock` (the system's, a test's ticking one): the casts
  are weighed within the first three quarters of the budget, the ends against the reply within the whole, and the last-resort pass
  shares the clock of the pass before it. Whatever the time, the first depth is searched whole and ending the turn now and
  the best end that acts are weighed against the reply, so that a search the machine starved of time never passes the turn
  for want of it: before, a budget spent left only the walks. With a challenge, the search that keeps it has a budget of
  its own, as before. `TurnPlan.Replied` says how many ends were weighed against the reply. Tests (`FightTacticsTests`): a
  search out of time still strikes the enemy in reach; past its budget, it weighs only ending now and the best end; both
  red before.
- **Replayed** with `scripts/fights.cs --turns` on the 5 captures of 24 November from 04:16 (551 turns, 599 searches), three
  runs at once so that each had the same machine, against `main`: the casts cut at three quarters of the budget, the searches
  846 ms for 90% instead of 1,109, 1,083 ms at most instead of 2,253 (those keeping a challenge 1,734 instead of 2,450), the
  sum judged 0.07% lower, the kills foreseen 233 as before, the Sacrieur's damage foreseen 36,997 to 37,164, the
  Huppermage's 34,128 to 33,677, the Forgelance's 36,449 to 36,075, the Pandawa's 36,165 to 36,570 with D511; cut at half the
  budget, the sum judged 0.44% lower and the kills 219, the Pandawa's damage 34,086. With D508's harness, 4 and 8 bots on 4
  cores, a search lasted at most 871 and 977 ms on its own clock, instead of 3.3 and 9.8 s.
- **Not measured in game**: the plans of a machine slower than the owner's, which the cut at three quarters weakens most.
- **Rejected**: the casts cut at half the budget, 0.4% less judged for no time saved; a budget for the reply apart from
  the search's, which bounds neither; scoring the finalists on as many
  threads, which D508 bounds; fewer finalists, which costs every turn the plans of the turns with time to spare.

## D510: A cast the server leaves unanswered is left out for the turn, and for the fight once a second turn left it unanswered

*2026-11-24, F2 of the next release's study; the lead's choice. Refines D203.*

- **Found.** `FightPlay` left a spell out of the whole fight at its first cast unanswered within its delay (D203, a
  transformed character's spells): one answer lost removed a key spell for the fight. On the 41 captures of 22 to 24
  November, none of the AI's 18,330 casts went unanswered: the rule is a safeguard.
- **Made.** A spell unanswered is not weighed again that turn, and from the next turn on only once two turns left it
  unanswered. Test (`FightPlayTests`): a spell unanswered is cast again the next turn, then not the turn after once
  unanswered again; red before.
- **Rejected**: retrying it within the turn, each try costing the answer's whole delay of a turn of 30 seconds; a count
  of casts rather than turns, which one turn's retries on several cells would use up.

## D511: A weapon's blow is not struck in a state that prevents fighting

*2026-11-24, the owner's ask to see the fight AI's inconsistencies and fix them; the lead's choice. Builds on D217, D220.*

- **Found.** On the 41 captures of 22 to 24 November (1,577 fights of the AI's), the server refused 74 of the AI's weapon
  blows with text 372, 71 the Pandawa's and 3 the Forgelance's: each was struck in state 1 or 42, which the pack flags
  `PreventsFight` (the client's `preventsFight`), and none of the 600 blows it let through was. `CastRules` read only
  `PreventsSpellCast`.
- **Made.** `CastRules` refuses a weapon's blow (spell 0) while the caster holds a state that prevents fighting, but for a
  level that asks for that state, as for the casts. Test (`WeaponTacticsTests`): a character in such a state strikes no
  blow and casts its spells; red before.
- **Replayed** with `scripts/fights.cs --turns` on the 5 captures of 24 November from 04:16, with D509: the Pandawa's
  blows planned 10 instead of 14, its damage foreseen 36,165 to 36,570.
- **Not measured in game**: the server's answer to a blow in state 42 alone, refused once on these captures.
- **Rejected**: leaving the refusal to the server, which costs the turn a request and a refusal each time, and the AP the
  plan counted on.
