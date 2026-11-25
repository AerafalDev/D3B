# Movement and banking, a walk refused with its confirmation and a way failed on the bank's journey (D517)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The walk held by the
server is [D456](d456-groups-one-cell-each.md) and [D464](d464-groups-held-follower.md); the banking's journey,
[D79](d064-d090-step-4.md); a failed activity left idle, [D481](d481-harvest-losses-go-on.md).

## D517: A walk refused after it was shown has not arrived, and a bank's journey whose way failed is not travelled, not without a bank

*2026-11-25, the owner's report relayed by the lead ("there is a real problem with the miner"), a bug fix within the harvest
session's scope. Builds D79, D456, D464; leaves D481 as it is.*

- **Measured** on the owner's capture of 2026-11-24 20:58 UTC (`…ADMCGBN6K`, client 3.7.14.18, mapping 25; `scripts/capture.cs
  --messages` and a scratch probe of the pack printing ids and cells, never committed) and the run's record: the miner banked
  three times, then at 03:32:27 into the capture, 90.1 % full in the sub-mine 188486154, its bank's journey walked to the door
  516143 (cell 532). The server showed the walk, then sent its refusal, naming cell 269, and the confirmation's response in one
  batch. D3B used the door at once, from 19 cells away; the server refused it twice. In the same second the run failed, nothing
  sent after, and the server closed the connection 30 minutes later. The same refusal at 02:02:33, the miner not full, cost only
  that journey.
- **The cause**, read in the code: `MapWalk` waited for the confirmation's response once the walk was shown and took it for
  arrived, the refusal between unread (the state took the cell it named). The journey left the door out, as any way that fails
  (`WorldTravel`), found no other way out of the mine and said no path; `GoToBankerAsync` took that for no bank at all, and the
  activity's unloading gives up on no bank at once, the run failing still full.
- **Made.** A walk the server refused after showing it is refused, confirmed or not (`MapWalk`), so a door or an element is not
  used from where the server holds the character. A bank's journey that says no path while a way to a bank leads from where the
  character stands is not travelled (`BankOutcome.NotTravelled`), the journey's own leaving out of a failed way not the world's:
  the unloading tries again, each try a journey of its own, three times as before (`HarvestActivity.MostBankings`).
- **Not measured in game.** Tested on the fake game: a walk shown, refused and confirmed is refused; a bank's journey whose only
  way failed is not travelled, one from where no way leads to a bank has no bank.
- **Left.** A journey of the harvest whose only way failed once still says no path, which leaves the tour's maps it sought, or
  the zone in a rotation, for the tour: `AJourneyWhoseOnlyWayFailsEndsWithNoPath` holds it on purpose, and the harvest counts no
  failure for no path. A run that fails still leaves the connection idle until the server closes it (D481, the owner's choice).
  The guild chest's journey (`GuildChest`) keeps no path as no chest.
- **Rejected.**
  - Every journey ending failed rather than no path once a way failed on it: the harvest would count three such journeys as lost
    and end, where it now leaves the maps for the tour.
  - Not leaving out a way whose walk was refused, the journey trying it again: a change's outcome does not tell a refused walk from
    one the server never answered, and a way that never answers would be tried until the journey's failures end it.
  - Ending no run on an unloading that fails: the owner's D481, not this report's.
