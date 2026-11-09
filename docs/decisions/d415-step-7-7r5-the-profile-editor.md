# Step 7, 7r5, the profile's editor (D415)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D415: 7r5, a profile names a zone as its place: a third value of the place and a zone setting, read through a seam

*2026-11-09, step 7, session 7r5, the owner's four choices, each as the lead recommended it. Builds D296, D299, D386, D393, D394, D395 to D397, D413.*

- **Found.** A zone (`ZoneAsset`, D394) is read by nothing but its editor and `ZoneMaps`; a harvest or a hunt names its place by an area and
  sub-areas (`Place` Automatic or Chosen, `Area`, `SubAreas`), which is what the owner did not see the zones used for. The zone's file lives
  in `D3B.Studio`, which `D3B.Bot` does not reference (D393), and the activities live in `D3B.Bot`.
- **Made.** `HarvestPlace` and `FightPlace` gain a third value, `Zone`, beside Automatic and Chosen (the sub-areas, stored as it is: its
  name in the editor becomes "Sub-areas"), and the settings gain `Zone`, a zone's id as text, picked from the project's zones and never
  typed (`SettingKind.Zone`, as `SettingKind.Route` for a route, D386). A profile stored before reads as it did and nothing of it is
  rewritten. The activities reach the zone through a seam of `D3B.Bot`, `IPlaceZones`, which `D3B.Storage` implements over the asset
  library and `ZoneMaps.Resolve`; a host without it, or a zone gone from the project, ends the run with a note, as a missing route does (D379).
  The zone is read when the activity starts (a preview reads it at each call): a zone changed while a bot plays is taken at its next start.
- **The profile's editor** (session 7r5b, the second pull request). A profile's form has four categories, What, Where, Bank and Care and
  fights (Upkeep and Fight merged, as the mockup draws them). A setting may be marked rare (`Advanced()`, one mark per setting, easy to
  change by use): a category folds its rare settings under "Advanced", which says "Advanced · N changed" with an amber dot when some
  differ from the model, so that none is hidden. Harvest, rare: Order, Excluded; Rotation, Safe level, Isolated maps, Zaap saving;
  Bank by its own rules with its share of pods, its zaap choice and the chest; Food, After a fight, After a lost fight. Hunt, rare: the
  least and most monsters in a group, Excluded; Rotation, Isolated maps, Zaap saving; the own banking rules; Food, After a lost fight.
  In view: the mode and what to harvest or hunt, the place and its zone or sub-areas, what to deposit, heal and its share, "the AI plays
  the fights", open the bags. Beside the form, the world's map shows the project's zones tinted and the chosen one outlined: with the
  place Zone a click picks the zone (the field says its name and "N maps") and a button "Edit in the world" selects it in the world's Zone
  tool; with Sub-areas a click adds or removes the sub-area. Nothing is painted in the profile's tab: painting stays in the world's Zone tool.
- **What the editor says.** Under the Zone field, in plain words, what the zone does: the harvest goes only through the maps of the zone,
  in turn, and gets there by itself; the hunt attacks groups only on them. A zone that holds no map says "This zone holds no map yet", in
  red, and blocks Save. The words come first, in part 1, in today's form.
- **Two pull requests**, the owner's choice. Part 1, this session: the zones read (the place, the setting, `IPlaceZones`, `HarvestZone`,
  `HuntingGround` and the previews from a zone's maps, a missing or empty zone ending with a note) and a first way to pick the zone in
  today's form, a list of the project's zones with "N maps" and the words above, tested on the fake game. Part 2, a session of its own
  (7r5b): the editor above.
- **Not built**: the zone as a filter of the Automatic place (the ranking of sub-areas would count a part of one), painting in the
  profile's tab, a zone for a list of steps' profile.
- **Words.** In French the setting "Zone" already named the game's area, which would have made two zones: it is now "Région", and the place "Chosen" reads "Sub-areas" ("Sous-zones"); the stored names do not change.
- **Rejected.**
  - The zone replacing the area and the sub-areas: one way to choose and fewer settings, but every stored profile naming sub-areas would
    have to be turned into a zone file, which writes the owner's files; the owner turned it down.
  - Painting a new zone from the profile's tab: two editors of one thing, and the map would have to carry the tools.
  - Folding nothing, or folding Deposit and the heal's share too: the first leaves the form as long as today's, the second hides what is chosen most.
  - One branch and one pull request for the zones and the editor: a wide review, and the zones' reading would wait for the editor's look.
