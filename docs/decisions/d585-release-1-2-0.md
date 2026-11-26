# The app's 1.2.0 (D585)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The release before
it is [D574](d574-release-1-1-2.md).

## D585: The app's release after 1.1.2 is 1.2.0, holding going on full, the mules, the bank's lists, the transfer's own load and the harvest's memory

*2026-11-26, the lead session of the owner's report of 26 November, the owner's « une fois la pr de la banque faite, publish ce qu'il
y a à publish & release », then « 1.2.0 » on the lead's recommendation. Builds D574 to D584.*

- **What it holds.** Four pull requests merged into `main` since 1.1.2: profiles that may go on full rather than bank, and fight
  as mules (#456, D578, D579); the harvest's growing back's bound from below and the maps' elements kept in `d3b.db` (#457, D583,
  D584); the transfer's own load carried to the chest, a run settling a walk a stopped run left, the bank's puts by one request,
  a move refused as too soon asked again, and the harvest's shut maps kept and the map an aggression was lost on left (#458, D575
  to D577, D581, D582); the bank's takes by list (#459, D580).
- **Published first.** `mapping-27.binpb` on `client-3.7.14.18` (D580); the pack stays at format 15 and the schema at 1. The
  release's three files were fetched and checked by `scripts/embed.cs` on `main` at `e6ef0b45`. `d3b.db` takes its migration
  `RegrowthFloorAndSightings` by itself at the host's first start.
- **Tagged.** `v1.2.0` on `main` once this entry is merged and its CI is green.
- **Not measured.** None of it has been seen in game yet: what the server does over the pods, a mule's turns, the bank's list
  take cut short by the pods, the shut maps, the regrowth's bound.
- **Rejected.** 1.1.3: two new options and a new mapping format are more than a patch.
