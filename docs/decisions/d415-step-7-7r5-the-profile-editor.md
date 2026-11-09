# Step 7, 7r5, the profile's editor (D415)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D415: 7r5, a profile names a zone as its place: a third value of the place and a zone setting, read through a seam

*2026-11-09, step 7, session 7r5, the owner's choice between two stored formats, as the lead recommended the first. Builds D296, D299, D386, D393, D394, D395 to D397, D413.*

- **Found.** A zone (`ZoneAsset`, D394) is read by nothing but its editor and `ZoneMaps`; a harvest or a hunt names its place by an area and
  sub-areas (`Place` Automatic or Chosen, `Area`, `SubAreas`), which is what the owner did not see the zones used for. The zone's file lives
  in `D3B.Studio`, which `D3B.Bot` does not reference (D393), and the activities live in `D3B.Bot`.
- **Made.** `HarvestPlace` and `FightPlace` gain a third value, `Zone`, beside Automatic and Chosen (the sub-areas, stored as it is: its
  name in the editor becomes "Sub-areas"), and the settings gain `Zone`, a zone's id as text, picked from the project's zones and never
  typed (`SettingKind.Zone`, as `SettingKind.Route` for a route, D386). A profile stored before reads as it did and nothing of it is
  rewritten. The activities reach the zone through a seam of `D3B.Bot`, `IPlaceZones`, which `D3B.Storage` implements over the asset
  library and `ZoneMaps.Resolve`; a host without it, or a zone gone from the project, ends the run with a note, as a missing route does (D379).
  The zone is read at each tour, so that a zone changed while a bot plays is taken at its next tour.
- **Rejected.**
  - The zone replacing the area and the sub-areas: one way to choose and fewer settings, but every stored profile naming sub-areas would
    have to be turned into a zone file, which writes the owner's files; the owner turned it down.
