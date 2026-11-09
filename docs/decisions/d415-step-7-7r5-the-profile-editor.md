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
  rewritten. The activities reach the zone through a seam of `D3B.Bot`, `IPlaceZones`, which `ProjectZones` implements in `D3B.Studio` over the asset
  library and `ZoneMaps.Resolve` (it holds both; the owner's answer said `D3B.Storage`, which only references the studio); a host without it, or a zone gone from the project, ends the run with a note, as a missing route does (D379).
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
- **Made in part 1** (2026-11-09): the above, and `ZoneFieldViewModel`, the zone's field in today's form: it reuses the route picker's dialog, shows
  the map count and the plain words under the button, and the profile cannot be saved with a zone that holds no map or is gone. Tested on
  the fake game, the settings' round trip, the view models and the real module's registration; **not seen in the running app**, and nothing
  tried in game.
- **A zone's maps are all taken** (found by the review): the rule that leaves out the maps reached only far round (D149) is not applied to a zone, so
  that a map the owner painted is never dropped silently, whatever the setting "Isolated maps" says. Left: a hunt's preview counts the monsters of the
  whole sub-areas a zone touches, and the activity names that sub-area as its place when there is one, not the zone's maps alone.
- **Not built**: the zone as a filter of the Automatic place (the ranking of sub-areas would count a part of one), painting in the
  profile's tab, a zone for a list of steps' profile.
- **Words.** In French the setting "Zone" already named the game's area, which would have made two zones: it is now "Région", and the place "Chosen" reads "Sub-areas" ("Sous-zones"); the stored names do not change.
- **Rejected.**
  - The zone replacing the area and the sub-areas: one way to choose and fewer settings, but every stored profile naming sub-areas would
    have to be turned into a zone file, which writes the owner's files; the owner turned it down.
  - Painting a new zone from the profile's tab: two editors of one thing, and the map would have to carry the tools.
  - Folding nothing, or folding Deposit and the heal's share too: the first leaves the form as long as today's, the second hides what is chosen most.
  - One branch and one pull request for the zones and the editor: a wide review, and the zones' reading would wait for the editor's look.

## D418: 7r5b, the profile as an asset editor: four categories, an Advanced fold counted by what differs, the world's map beside the form

*2026-11-10, step 7, session 7r5b, the owner's choices of D415 built as decided, and the owner's answer for the layout at the default size. Builds D395 to D397, D415; refines D330.*

- **Made, the categories.** A profile's settings are in four categories in both modules, What, Where, Bank and Care and fights (the harvest's
  Upkeep and Fight, the hunt's Upkeep and After, merged; the hunt's Bank now comes before Care, as the harvest's does; the harvest's two first
  titles read What and Where, as the hunt's). The texts' keys are `SectionWhat`, `SectionWhere`, `SectionBank` and `SectionCare`.
- **Made, Advanced.** `SettingBuilder.Advanced()` marks a setting rare (`SettingDefinition.IsAdvanced`), one mark per setting, set by the modules
  as D415 lists them: harvest Order, Excluded, Rotation, Safe level, Isolated maps, Zaap saving, Bank by its own rules with its share of pods,
  its zaap choice and the chest, Food, After a fight and After a lost fight; hunt the least and most monsters in a group, Excluded, Rotation,
  Isolated maps, Zaap saving, the own banking rules, Food, After a lost fight. A category shows those in view, then a fold, "Advanced", that says
  "Advanced · N changed" with an amber dot when N rare settings that show differ from the model (`SettingSectionViewModel`); a rare setting that
  a condition hides is not counted, as the editor's own count leaves it out. The fold opens by itself while a rare setting has a problem, so that
  none is hidden, and the folds the hand opened stay open when the editor is made again (a value given back, undo). The bar of sections that
  followed the scroll is gone: four short categories need none.
- **Made, the map beside** (`ProfileMapViewModel`, in the profile's tab). The world's map, read through the same services as the world's tab, shows
  the project's zones tinted and the place the settings name outlined (`Scene.Squares`), in the world the place lies in. A click, a press let go
  within four pixels (`WorldMapView.Clicked`; a longer one moves the map), picks: with the place Zone, the zone under the pointer
  (`ZoneFieldViewModel.Choose`, the field and its words follow); with Sub-areas, the sub-area of the map clicked (`SubAreasFieldViewModel.ToggleSubArea`),
  added or taken out, the sub-area of the map the world shows and not that of an inside sharing its coordinates; with Automatic, nothing, and
  the map says so. The button "Edit in the world", shown with a zone picked, selects it in the world's Zone tool (`MainViewModel.EditZoneInWorld`).
  Nothing is painted in the profile's tab. The name of the zone or sub-area under the pointer shows above the map.
- **Layout** (the owner's answer, as recommended). At the default window (1440 pixels, the Outliner and the Details open, the tab about 790
  pixels) the map is a column of 40% of the tab, between 300 and 460 pixels, and the form, in its narrow mode (labels above the fields), takes the
  rest. Below 640 pixels the map leaves its column and lies above the form as a band, 150 pixels of map in at most 280 of height, scrolling inside.
  The preview of the settings (the tour's mini-map with its probable, empty and isolated maps, the figures, who plays the profile) stays under
  the map: it carries the states the world's map does not draw, and D397 asks for no feature lost.
- **Not built.** A check that a sub-area clicked lies in the region chosen (the field's own "not within" problem says it); a menu of worlds on
  the profile's map (it follows the place picked, and the field's dialog still picks a zone of another world); the band folding away; the tour
  drawn on the map itself; a profile playing a list of steps has no map (it has no place).
- **Not seen**, only tested on view models and headless windows: the tab in the running app (the map was drawn without tiles, its source empty
  in tests), how a click and a drag tell apart under the owner's hand, how the folds and their count read, the band at a narrow window. The
  marks of Advanced are the lead's application of D415's list, to be changed by use.
- **Rejected.**
  - The fold state kept in the profile or in `app.json`: it is the view's, per editor, and a profile's file is the player's behavior.
  - A pick on the press: a drag to move the map would pick whatever lay under its start.
  - The tour drawn over the map in place of the thumbnail: it would lose the probable, empty and isolated states, and the map would have to
    wait for a preview the settings do not always give.
