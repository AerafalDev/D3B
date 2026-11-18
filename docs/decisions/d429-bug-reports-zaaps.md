# Bug reports, the profile's zaaps (D429)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D429: A profile that takes no zaap takes none on any of its journeys, to its place, back and to the bank

*2026-11-18, outside the roadmap's steps, the owner's report; the meaning chosen by the owner, as the lead recommended it.
Supersedes D405's "`UseZaaps` concerns the journey to the bank only"; builds D167 and D405.*

- **The report.** "Even when a profile chooses not to take the zaap, the bot takes it anyway": asked, the owner said the zaap was
  taken on the journey to the hunting or harvest place, or back to it after the bank.
- **Found by reading and by tests on the fake game, nothing measured in game.** The profile's only zaap flag, `UseZaaps` in
  `HarvestSettings` and `FightSettings`, was what D405 made it: the journey to the bank only, shown only under `OwnBanking`. Every
  other journey followed "Zaap if it saves at least N maps" (D167), whose least value, 1, still takes a zaap. A profile had no way
  to walk everywhere.
- **Made.** `UseZaaps`, "Take zaaps", now holds for every journey of the activity: `BotActivity.UsesTeleports`, read by
  `BotContext.UsesTeleports` and `WorldTravel.RulesOf`, leaves the zaaps the character knows out of the path's rules, so the tour's
  planning and its journeys walk alike; a behavior speaks for the activity it plays (`BehaviorActivity`). The flag shows in the
  place's section, its "N maps" beside it only while it is on, whatever `OwnBanking` says.
- **The bank's journey**: no zaap when the profile takes none; else, with `OwnBanking`, as the profile; without it, as the general
  setting `Banking:UseZaaps` (`BankingPreferences.UseZaapsOf`, `profile && (own || general)`). The general setting can only
  forbid more, never allow a zaap the profile refuses.
- **The stored profiles** keep their stored value, now read with the new meaning: one that unchecked the bank's zaap walks
  everywhere, what the owner expected of it.
- **Rejected.**
  - The meaning of D405 kept, the bug sought elsewhere: the journey that took the zaap was not the bank's.
  - Two flags, one for the bank and one for the tour: more settings for a case no player asked for.
  - "Never" as a value of `ZaapSaving`: a number that means "no" is a hidden flag, and the bank's journey would still need its own.
