# Banking settings (D405)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D405: Banking: one general share of pods and zaap rule, a profile's own on request, and a journey that outlives a refused zaap

*2026-11-09, outside the roadmap's steps, players' reports the owner relayed; the place of the settings chosen by the owner, as the lead
recommended it. Builds D79, D80, D83, D127, D129, D167, D248 and D151 for the machine's settings.*

- **The reports.** The share of pods at which a bot banks was set in every profile (`FullAt`, 90 by default, on the harvest and on
  the hunt). There was no way to tell the AI not to take a zaap to the bank. And from Incarnam to Astrub, where a zaap costs 1,500
  kamas the character did not have, the character failed to zaap, walked on and never banked.
- **Found by reading and by tests on the fake game, nothing measured in game.** A teleport the character cannot pay is not taken
  (`WorldPathfinder`, `WorldPathRules.Kamas`, D83), and one the server refuses leaves every teleport out of the rest of the
  journey (D74): both tests pass as they were. What the code did wrong: a journey fixed the changes it may make from its first
  path, twice its steps and ten more, and that path was the zaap's, one or two steps; once the zaap failed, the walk on foot, many
  maps long, ran into the same cap after a dozen maps and the journey failed (`WorldTravel`), three times, so that the bank was
  never reached. The budget is now made again from the path on foot once a teleport fails (`JourneyBudget`). This is the lead's best
  reading of the report, not seen on the owner's capture: the cost of a zaap leaving Incarnam's world map is measured only towards it
  (client.md), so the real 1,500 may also lie above what `TeleportCost` reckons, and the answer to a teleport that cannot be paid is
  not measured either.
- **One general setting** (`BankingPreferences`, in `D3B.Bot.Banks`, the host's configuration `Banking:FullAt`, 50 to 100, 90 by
  default, and `Banking:UseZaaps`, on by default), shown in the app's Settings, section Bots, group Banking, kept in `app.json` like
  the delays and applied at once to the bots' next step. A bad value stops the host before it starts, as a delay does.
- **A profile follows it unless it asks for its own**: `OwnBanking` in the settings of the harvest and of the hunt, off by default,
  shows the profile's `FullAt` and a new `UseZaaps`. The stored names are unchanged; a profile stored before has `FullAt` and no
  `OwnBanking`, and reads with `OwnBanking` on, so that it banks as it did. A profile saved from now on stores all three.
- **`UseZaaps` concerns the journey to the bank only**: `Banking.BankAsync` and `GoToBankerAsync` take it, and the journey walks
  whatever zaaps the character knows (`WorldTravel.TravelAsync`, `useTeleports`). The tour's zaap, "if it saves at least N maps"
  (D167), stays the profile's. With zaaps allowed, the bank's journey is as before: a zaap taken when it saves enough maps and the
  character can pay.
- **Rejected.**
  - The share only in the profile, with 98 % as the default: nothing general, as the players asked.
  - The general setting alone, the profile's field removed: stored profiles' own values would be lost, and a hunt that wants to
    bank early could not.
  - A field of the profile that is empty to follow the general setting: no kind of setting holds an optional number (D98); a flag
    that shows the fields is the existing mechanism (`VisibleWhen`).
  - Reckoning a margin on a zaap's cost: nothing measured sets it.
- **Left.** A refused teleport still bans teleports from that map for the bot's life (D248), a cost refusal included, as the texts
  are not told apart; the text of a zaap that cannot be paid is to be read on the owner's next capture. The steps of a list (D386)
  and the routes never bank: only the harvest and the hunt do.
