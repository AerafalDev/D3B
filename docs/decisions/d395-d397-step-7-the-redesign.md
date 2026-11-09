# Step 7, the redesign (D395 to D397)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D395: 7r, the studio's redesign: what hurts today, and the studio seen as a game engine

*2026-11-09, step 7, session 7r, the owner's: the app is "not practical at all", to be redesigned from A to Z, the example being
that a route needs each of its steps configured by hand; the owner chose the global redesign with mockups of the whole app,
as 7c did (D319), over a tour of the journeys. **Proposed; the owner chose the engine and variant B in D396.** Builds
D295, D319 to D328, D371, D375 to D381, D386; reads D394 (session 7d4, commit `f0c0cd2`, not merged when this was written). The
mockup: `docs/mockups/studio-7r.html`, four scenes (keys 1 to 4), three ways of setting a route's steps (keys A, B, C), N for
the notes on what changed.*

- **Method.** The app's current screens were drawn by its own headless tests (`D3B_DRAWINGS`, `Drawings.Draw`) and read beside
  the code; the numbers below are counted from the dialogs and view models, not timed.
- **What hurts, by journey.**
  - *Make a route that does something on each map.* Draw it (one click a map), then for each step: choose it, "+", a dialog of
    eight actions, a token, a popover, a picker dialog for the resources, back: five to seven gestures a step, the same ones on
    each map when the route harvests the same resources throughout (8 steps: about 50). All of it in the inspector, 340 pixels wide,
    while the map above stays on its own; no copy, no several steps at once, no value for the whole route. Recording, which was
    to do most of it, is not there (7d4b).
  - *Run that route on a character.* The route is not a thing a character can play. Add a behavior (a "list of steps"), add the
    step "follow the route", pick the route; add a profile ("plays: an activity or a list of steps"), pick the list; give the
    profile to the character from the card's right-click menu or the inspector; press play. About fourteen gestures in four
    editors before the first step, with four names for one idea (route, list of steps, behavior, profile). A looping route
    followed by another step is a warning on the list ("the steps after step 1 never play"): the model leaking.
  - *Start a harvest or a fight.* The card shows no profile and no play button; the profile is chosen from a right-click menu, the
    character from the card, the play from the title row or F5, three places for one intention. The welcome tab is an empty page of
    shortcuts, the inspector reads "Choose something to see its properties" most of the time.
  - *Set a profile.* The harvest profile is one scrolling form of 22 settings in five sections, over more than a screen, beside a
    preview that is a thumbnail; "Variant of Harvesting, 4 values changed" speaks the model, not the player; the place is picked
    by name and token, never on the map the studio now draws; the rare settings (rotation, safe level, zaap saving, isolated
    maps, after a lost fight) weigh as much as the three that matter.
  - *Follow a character.* Its tab shows the live map and what it does, but nothing knows a route is being played: no step 3 of 8,
    no marker on the route's own tab, nothing on the card (`RouteRuns` is not read by the app).
  - *Zones.* Painted on the map (D394), then read by nothing: a harvest or a hunt still takes its sub-areas by name.
- **The causes**, four. The app is built from the model outward (an editor per kind of asset, in tabs) and not around the
  world the bots play in; the work happens in the inspector, a side panel, where the centre holds a map or a form; configuring is
  by step and by action, never for the route or for several steps; and what runs is not seen where it is edited.
- **A first proposal turned down.** Built around the player's intent (a "mission" on each card, a start page, the route as a
  strip of cards, the mission read as a sentence), it drew the owner's answer that they did not like the design at all: it should
  be seen as an engine, routes should be really practical, or else an all-in-one that does everything, intuitive and practical.
  Replaced by what follows.
- **The proposal: the studio as a game engine** (D295 already says so; the shell of 7c has the engine's panels, not its idea).
  One persistent **viewport on the world** (the map of D308 to D372) is the studio's centre, not a tab per route: characters
  live on it with their names, routes with their steps, zones, all selectable; its toolbar holds the tools (*Select*, *Route* to
  trace, *Zone* to paint, *Record* for 7d4b) and the **Play in Editor**: the chosen character plays its profile, the world stays
  under the eyes, the viewport gets a green frame, the title's run block stays. An **Outliner** lists what is in the world
  (characters, routes with their steps as children, zones, each with an eye; a click selects in the viewport and the viewport
  selects in the list); the project's **Content** keeps the assets' tree (D326, D351); the **Details** panel shows the
  selection and sets the same fields for several objects at once (steps framed in the viewport, or Shift-clicked in the
  Outliner); the bottom dock holds Journal, Trace, Problems and, for a route, the Sequencer. What a step does is drawn in the
  viewport as small dots under its node, so that a route reads at a glance. A profile opens as an **asset editor** in a tab:
  categories, the rare settings under "Advanced" in each, a bar and an arrow on what differs from the model, and the map beside
  it to pick the place (and zones, which gives them their use). Kept: the look, scale, panels, search, settings, the route's
  line (D371, D375), the sentences and tokens (D380), undo and the keys (D394).
- **Setting a route's steps, three variants** over one model, to choose one: an action becomes a *clip*, an action over a range of
  steps, derived by grouping equal consecutive actions of the anchors, so that `RouteAnchor.Actions` and the file stay as they
  are (D378); "everywhere" is a clip over every step.
  - **A, Details (Unreal's):** frame steps in the viewport; the Details panel lists the actions of the selection, "on 2 of 3
    steps" where they differ, "Add to the 3 steps", "Copy those of step 3"; the route's own Details carry "on every step".
  - **B, Sequencer (Unreal's Sequencer, Unity's Timeline), the lead's recommendation:** under the viewport, a ruler of steps and
    a track per action (Harvest, Fight, Door, NPC…): a clip is drawn across the steps it covers, its edges dragged, copied with
    Shift; one gesture sets ash and chestnut on seven steps; the green playhead is the character, so following a run is nearly
    free (7r4); hovering a column lights its step in the viewport. The Details panel keeps the clip's sentence and range.
  - **C, Components (Unity's):** the route and its steps are objects of a hierarchy; actions are components added with "Add
    component"; those on the route hold for all its steps, a step adds its own, a selection of steps takes one together.
- **Sessions** (each ends with an app that works; the order is the lead's, to be confirmed):

  | Session | Content |
  | --- | --- |
  | 7r2 | The engine's frame: the world viewport as the centre with its tools, the Outliner, the Details panel, the Play in Editor frame; the route, zone and character tabs become selections of it; a route dragged onto a character, or its Play, makes the list and profile behind it, visible in the project (D386). |
  | 7r3 | Setting a route's steps with the variant chosen, the clips derived from the anchors, the palette in place of the dialog, several steps at once, copy; recording (7d4b) in the toolbar after the measure in game. |
  | 7r4 | Following a run: the cursor told to the app, the playhead or the lit step, the character's marker on its route, the card's and the Outliner's progress. Tried with the owner on a client. |
  | 7r5 | The profile's asset editor: categories, Advanced, the place picked on the map, zones read by the harvest's and the hunt's place. |
  | 7r6 | Sequences as blocks, laps and "until" for a route (a change of `RouteRunner`, to confirm), then 7e's canvas on the same files. |

- **Not measured**: whether the sequencer is what the owner means by practical (their use is the check, as in every step of 7);
  that grouping equal consecutive actions loses nothing a route's file holds (to test on every route kind in 7r3); the cost of the
  host telling the app the run's step (7r4 reads `RouteRuns`, which no view model reads yet). Not in the mockup: the Outliner's
  drag onto a character, and the route-played-alone path of 7r2.
- **Left to the owner, one decision at a time**, in this order: (1) the engine direction itself; (2) A, B or C for the steps;
  (3) the words (profile, route, sequence, as the mockup has them, or others); (4) the sessions' order. Nothing is coded before
  the first two are answered.
- **Rejected.**
  - The intent-first redesign with missions and a start page: turned down by the owner (above).
  - A tour of the journeys with fixes in place: each fix is in the proposal, but the owner chose the global redesign.
  - Two modes, "simple" and "studio": it doubles the screens to keep, and the studio is the product (D295).

## D396: The studio as a game engine, and a route's steps set in a sequencer: the owner's choices

*2026-11-09, step 7, session 7r, the owner's choices among the mockup's options, as the lead recommended the second. Settles the
first two of D395's questions; the rest of D395 stands as proposed.*

- **Direction chosen: the engine.** The studio's centre is one persistent viewport on the world, with the Outliner, the Details
  panel, the Content browser, the Play in Editor and an asset editor for a profile, as `docs/mockups/studio-7r.html` draws them
  (D395). The first, intent-first proposal stays turned down.
- **A route's steps: variant B, the Sequencer.** Under the viewport, a ruler of steps and a track per action; an action is a clip
  drawn across the steps it covers, its edges dragged, copied with Shift; the playhead is the character. The Details panel keeps
  the clip's sentence and its range, and the properties of the steps chosen (variant A's "Add to the selection" stays only where
  the Details already list a step's own figures). Variant A as the main path and variant C (components) are not made.
  The model stays D395's: a clip is derived by grouping equal consecutive actions of the anchors, `RouteAnchor.Actions` and the
  file unchanged (D378), to be tested in 7r3 on every kind of action before the editor writes through it.
- **The words stay**, the owner's choice: profile, route, sequence (and zone, character), as the mockup and the app already have
  them; "mission" and the intent-first vocabulary are dropped. The Add menu offers Profile, Route, Zone, Sequence, Folder.
- **The sessions' order stays D395's**, the owner's choice: 7r2 the engine's frame, 7r3 the Sequencer, 7r4 following a run, 7r5 the
  profile's asset editor, 7r6 sequences, laps and recording. Nothing is left to decide before 7r2. Nothing blocks 7r2's start.
- **Next**: session 7r2, the engine's frame, in a session of its own; no code was written in 7r.

## D397: 7r2, the engine's frame: the world viewport, the Outliner, the Details panel and Play in Editor

*2026-11-09, step 7, session 7r2, the lead's, within the owner's choices of D396 (the engine, the Sequencer for 7r3, the words kept,
the order of the sessions). Builds D295, D319 to D328, D371 to D375, D394 to D396; the owner chose to merge D394 first. The mockup:
`docs/mockups/studio-7r.html`, scenes 1 and 2.*

- **What was made.** The studio's centre is one persistent viewport on the world, not a tab per route (`WorldViewportViewModel`,
  `WorldView`); the left column is an Outliner (`OutlinerViewModel`, `OutlinerView`) over the Content browser, which stays the
  assets' tree (D326, D351); the right column is the Details panel, the inspector of D324 under its new name and with the
  selection's kinds; the run block keeps its place and the viewport gets a green frame while the chosen character plays. What is
  selected is one thing, `WorldSelection`, which the three show and set, so that a click in the map selects in the list and a
  click in the list selects in the map. The architecture's "Desktop app" section says how, panel by panel.
- **D394 merged first**, the owner's choice among merging and working around: the zones, their layer on the map (`WorldMapZoneLayer`)
  and `UndoHistory` were there to build on, and nothing was written twice. The conflicts were the docs' alone. This pull request
  therefore holds D394's commit too, until 7d4's is merged.
- **Route and zone have no tab.** Their editors are kept as D371, D375 and D394 made them, with anchors, pinning, painting, typed ids,
  undo and the keys; selecting a route or a zone opens its editor without a tab and the viewport forwards the map's gestures to it.
  Nothing of the editors' model changed but two read-only members: `RouteEditorViewModel.CurrentAnchors` and
  `ZoneEditorViewModel.Current`, which let the map draw a route or zone that is open with changes not saved while another is
  selected, and a step's `Actions` on the scene's stations. An editor is closed only when its asset is gone: there is no tab to
  close, an unsaved one shows a dot in the Outliner and in the status bar, and Save all saves it.
- **The tools** are the viewport's: *Select* presses what is there (a character, a step of any route, a zone) and moves a step or
  pans; *Route* traces the route selected, or **makes a new one, a file, on the first press** when none is selected; *Zone* paints
  the zone selected likewise, with Paint, Erase and Sub-area under the toolbar; *Record* is drawn disabled until 7d4b. A tool needs
  what it works on: a selection that moves to something else leaves it for Select, so that a stray press never writes into a route
  the user is not looking at. Undo and Redo follow the route or the zone selected.
- **The map draws the world.** The other routes as faint straight lines with a node a step (the editor's route keeps its path, its
  chevrons and its gaps), the zones tinted, the characters as discs with their names, under each step's node a small dot by what it
  does (harvest green, fight red, door amber, NPC blue, else grey: `WorldMapActionKind`). The viewport's layer follows the selection:
  a route in another world shows that world. An eye closed in the Outliner leaves its route, zone or character off the map only.
- **The characters' cards are gone**, replaced by the Outliner's rows (portrait, a dot of the state, name, the step of its run or
  what it does, an eye; the offline ones dimmed under them) and, for the rest of what a card said, a State section in the character's
  Details (where it stands, life, pods). The menu of the right click moved to `CharacterMenu` unchanged. The character's tab stays
  a tab: its map is the cell-level one, which the world's viewport does not replace, and the owner's D396 asked for the loss of no
  feature.
- **Names.** The panels are `StudioPanel.Outliner` and `Details` in the code and in the Window menu (they were `Characters` and
  `Inspector`; `InspectorViewModel` and its view are the protocol's, which an "inspector" of the code made ambiguous); an
  `app.json` that closed the old characters' panel opens the Outliner.
- **The welcome is no longer shown at start**: the world's tab is the first and is pinned (`EditorTab.IsPinned`, no ×, `CloseAll`
  keeps it); the welcome's command and shortcuts stay (`ShowWelcome`).
- **Until 7r3** the actions of a step are set where they were, in the Details panel's route section, through the same picker and
  forms: nothing is lost. The Sequencer, the clips, the following of a run and the profile's asset editor are 7r3 to 7r5.
- **Not measured**: whether the viewport, the Outliner and the tools are what the owner means by practical (their use is the
  check); the cost of drawing every route and zone of a big project (the routes are straight lines, the zones cached by asset
  and layer; the Outliner and the viewport are read every 250 ms and change only when their signature does); the Outliner with
  hundreds of routes (its rows are not virtualised). In the headless window the Escape key reaches the map's handler marked as
  handled by something earlier in the window, so that leaving a tool by Escape is tested on the view alone, not in the whole
  window; the keys V, B, E and R are tested in the whole window.
- **Left for the sessions after**: the multiple selection of steps and the "same fields for several objects" of D395 (7r3, with the
  Sequencer); the Outliner's drag onto a character and a route played alone, which the mockup does not draw (D395); the Content
  browser's own menu entries for the new world (Add > Zone and Route exist; Sequence is the list of steps of D386).
- **Rejected.**
  - Keeping the characters' cards beside the Outliner: two lists of the same characters, and the left column too tall for the
    Content browser.
  - One `WorldMapView` per editor, shown in turn: the pan, the zoom and the tiles are lost at each selection, which is the tab per
    route again.
  - Drawing the other routes with their path: a search for each leg of each route at each change; straight lines tell the shape,
    and the path is the selected route's.
