# The bot

What the player tells the bot and how they see what it does (roadmap step 3), designed with the owner on 2026-11-12
and 2026-11-13 from mock-ups, then a clickable prototype in the real shell (local branch `prototype/step3-model`,
never merged). Its model is Unreal's: a **blueprint** is a class, a **character** an instance that runs one, and a
variable marked "set per character" is a setting each character sets for itself (ADR 0014). Nothing acts in the game
before step 4: step 3 delivers the model, its editor, its storage, and its runtime tested on captures; Play, what it
does now, its History and the debugger come with step 4's first actions.

```mermaid
flowchart LR
    given["D3B's blueprints (locked, in the app)"] -->|Duplicate| mine["my blueprints (Documents/D3B)"]
    given --> char["a character: its blueprint, its own values"]
    mine --> char
    zones["zones and routes, the game's sub-areas"] --> char
    char --> run["bot: the runtime, one flow per character"]
    live["game::Live: the observed state"] --> run
    run -->|"actions, from step 4"| act["travel, harvest, bank, fight"]
    run --> hist["History, 7 days"]
```

## The concepts

The six scenarios the owner chose (first launch; two characters, two zones; a goal; my own behaviour; understanding
what it does; a D3B update) need these and nothing else: no profile, activity, place or goal field.

- **Blueprint**: an event graph, functions and variables. D3B's own ship with the app, **locked**: automatic harvest,
  profession harvest (one blueprint, its profession set per character: the owner, 2026-11-14) and hunt, until v2's
  eight replace them ("The language, v2", D3B's blueprints); **Duplicate** makes one of mine, fully editable, noting "based on
  X vN": named "<name> (copie)" in the player's language, its texts taken from the interface since a given
  blueprint's are translations, its id made from its name (accents dropped, a number when taken by a blueprint or by
  any file of the folder, in any case); a copy of mine keeps what mine was based on (the owner, 2026-11-14). A given blueprint's new version reaches the characters that run it, their values kept; my copy stays as it is
  and says its original has a newer version (step 3's PR 23, the owner's choices of 2026-11-16): at start, each
  character running a given blueprint of a newer version than its file notes has its settings carried as when another
  blueprint is chosen, a value kept when the new version's setting of the same id accepts it (the old version's types
  are not shipped), and the values lost are said in a notice above its settings, live and offline, until the player
  closes it; a copy whose original is newer says "its original has a newer version, vN" in its tab's line and its
  Library hover, a blue arrow on its row, and offers nothing more yet (a compare may come later).
- **Character**: runs one blueprint, chosen in its tab, none until the player chooses one; another blueprint keeps
  the values of its settings of the same id and type and drops the others (the owner, 2026-11-16), and those out of
  their new bounds or equal to their new default. Its **settings** are the blueprint's variables marked "set per
  character" (Unreal's Instance Editable): the blueprint holds the default, the character its own value, marked when
  it differs and reset in one click. Two characters, two zones, one blueprint.
- **Variable**: a type (zone, route, percentage, number, items, bank, profession, yes or no…), a default, bounds, a help line, set
  per character or not, and a duration: one run, or **memory**, kept per character across sessions. A goal is a
  variable tested in the graph ("stop at 5,000 harvests").
- **Zone**: maps the bot goes through its own way. The game's **sub-areas are ready-made zones** (`sub-areas.json`
  lists their resources and monsters); a zone of mine is painted map by map or a whole sub-area at a time.
- **Route**: an order of passage only: anchor maps joined by the shortest way on the world graph (its scrolls, doors
  and map actions; zaaps join it with step 4's travel), as a loop, one way or a round trip. What is done on the way comes from the blueprint.
- **History**: what each character did, as episodes ("Bank · 3 min") with their reason.

## How a blueprint runs

As decided on 2026-11-12, on Unreal's event graph (ADR 0014):

- **One flow per character, one action at a time.** "On start" is the main flow. Another event (a fight starts, life
  below, pods above, every N minutes, the map changed, reconnected) interrupts it by **priority** (1 before 2) and by
  its **mode**: interrupt then resume (the default), wait for the current action, interrupt and restart from the
  start, or in parallel for a graph that never acts. A threshold fires once and re-arms past its reset value. "You took
  control" (step 4) is above everything, outside the graph.
- **Actions are goal-shaped**: an intent and a check on the observed state that says it is done ("on map X", "pods ≥
  80 %"), so an interrupted action resumes by running again. Each ends **Done** or **Failed** with its reason; a Failed
  left unlinked blocks the character with that reason.
- Unlike Unreal's, a **function** may hold actions that last (2026-11-12): moving to a bank is one.
- **Queries** are read when the node that uses them runs; the value used is kept for the debugger.
- **Events the game forces**, when the blueprint does not handle them, have a default: a fight is fought by the
  default AI (step 6), then the flow resumes; a death stops the run, blocked.
- A setting changed while running applies at the next safe point, between actions, and says so. The states: without
  blueprint, ready, running, paused, finished, blocked (with its reason), disconnected (listed greyed, its blueprint
  and values kept). A blueprint with a problem cannot run.

### The runtime

`bot::run` (step 3's (25), designed with the owner on 2026-11-16, ADR 0017) runs a blueprint for one character, a
node at a time, against the observed state, on a clock it is given. It knows no kind of node: each brings its own
behaviour, so v2's plug in without touching it. Nothing acts in the game before step 4, whose Play drives it.

```mermaid
flowchart LR
    file["a blueprint without problems"] -->|compiled| program["program: each output to its node, each input to its source"]
    program --> run["Run.advance(view, now)"]
    view["game::View, a snapshot"] --> run
    kinds["one Behaviour per kind"] --- run
    run --> out["state, trace of values, memory changed"]
```

- **Compiled**: a blueprint is compiled into a program, each flow output to the node it leads to and each value input
  to its wire, its typed value or its pin's default, so the file's schema stays out of the engine (v2.1's schema 2
  changes the compilation only). One with a problem is refused.
- **Advanced**: `Run::advance(view, now)` runs nodes until the flow waits on the clock or the observed state, ends or
  blocks; the clock is injected, so tests drive it. Step 4 drives it from one actor per character fed the view's
  snapshots (the owner, 2026-11-16: the engine alone in (25), the C# version's actor per character kept).
- **One behaviour per kind**, behind one trait in a registry by kind id: when the flow reaches its node it goes out of
  a pin, runs a chain and comes back (Sequence's outputs, a loop's body), waits, returns an exit, leaves the loop,
  finishes, pauses or blocks; a value node gives its output when read. Get, Set and Call are the engine's own.
- **An explicit stack**, no async: a frame per call (its locals, its nodes' counters, its outputs) and per chain, which
  comes back to the node that ran it when its flow ends; Leave the loop unwinds to the nearest loop. A waiting run is
  its stack, so an interrupted flow keeps its place, its loops' counters and its calls.
- **Values are pulled** when the node that uses them runs, through their wires; each value used goes to a bounded
  trace (its node, pin, value and time) for the debugger.
- **Variables**: a run's from their defaults, a setting from the character's own value; memory is given at the start
  and its changes handed back, to be saved in the character's file under `memory` at safe points (the owner,
  2026-11-16; from step 4's wiring, the file's schema 3), kept on another blueprint for the same id and type, as a
  setting.
- **An action before its step** waits for its check on the observed state (the owner, 2026-11-16): the player doing it
  by hand completes it, and step 4 only adds the doing; a check on what D3B does not observe yet fails "D3B does not
  see this before step N".
- **It blocks**, with the reason and the node, on an unlinked Failed, a death (the character's fights lost rising:
  the forced default; a `died` event held by the blueprint is (26)'s), 10,000 nodes in a row without waiting ("runs
  without pause"), a call deeper than 64 (settled alone), or a value that cannot be computed. Its states are ready, running, paused,
  finished and blocked; without blueprint and disconnected are the character's: no view holds the run, and the node
  at work runs again once the character is back, as actions are goal-shaped. A Wait counts the clock's time (settled
  alone).
- **(26)'s seam**: a stack of flows, one today; an event pushes its own, and when it ends the flow under it enters its
  node at work again; Restart empties the main flow; a safe point is the moment between two nodes.

## The language, v2

After measuring the editable graph, the owner asked on 2026-11-16 for blueprints "much more complete", able to do
much more, and "as practical as a Lua script and as complete" (the reference bots' Lua API and 58 profession scripts,
read for ideas only: `lua-reference-analysis.md` in the sessions' notes). v2, designed that day from a mock-up of the
catalogue and of the Summary (ADR 0016), keeps the model above and gives the player a real language: loops, many-way
branches, conditions and arithmetic, typed expressions, level ranges, lists, timers, events of their own and of any
condition, more of the game read, the actions of steps 4 to 7, and richer functions and routes. It goes from 25 kinds
to about 130.

```mermaid
flowchart LR
    ev["events: game, timers, 'when … becomes true', mine"] --> flow["flow: if, switch, sequence, loops, waits"]
    flow --> act["actions, Done or Failed (steps 4 to 7)"]
    val["values: compare, logic, math, random, text, lists, expressions, ranges"] --> flow
    q["queries over game::Live"] --> val
    var["variables: run, memory, setting, local"] <--> flow
    fn["functions: exits, pure, locals"] --- flow
```

- **Values are compact nodes**, as Unreal's: `=`, `≠`, `<`, `≤`,
  `>`, `≥`, between; AND, OR, NOT, Choose (a value if true, another if not); `+`, `−`, `×`, `÷` (whole), remainder,
  min, max, clamp, "… % of …"; a random number between two bounds, "true N % of the time" and a random element of a
  list (the owner, 2026-11-16: randomness is the player's to use); Text, a template whose `{holes}` are inputs of any
  type, and Join; N minutes and N seconds; a list made of its inputs, its length, contains, element n° (from 1),
  first, position of. They have no flow and are read when the node that uses them runs, the value kept for the
  debugger; v1's "at least" becomes `≥`.
- **Expression**, beside them (the owner, 2026-11-16, for Lua's one line): a value node holding a typed formula,
  `pods_percent >= 80 and job_level(bûcheron) < 100`, over the queries, the variables and the values' operators,
  parsed and checked by Rust (its type, each name, each argument), with completion as it is typed; it reads and never
  acts, and the Summary shows it as written. A name it does not know is a problem, as a wire would be.
- **Ranges** (the scripts' tables by level): a type "from level N: a value" of any type (a zone, a route, a number…),
  edited as a sorted table, gaps and overlaps marked, pasted from text; "According to the range of …" gives the value
  of the row a number reaches, or follows that row's output. "Equip the best I carry for my level" (below) needs no
  table.
- **Types**: v1's (yes or no, number, percentage, text, zone, route, bank, items, profession) and a **map** (a map's
  id, picked on the World), one **item**, a **duration** (whole seconds, typed "1 min 30") and a **time** of day
  ("23:00"), an **NPC**. Any type has its **list** (a list of zones, of maps, of numbers…), its pin a square. A generic pin, a
  **wildcard**, takes the type of its first wire or value, and every wildcard of a node the same one: one `=`, one
  Switch, one For each for every type. A percentage feeds a number, any value a Text's hole; numbers, percentages,
  durations and times are compared. v1's Items stays the selection actions take ("all", "none", these items); a list of
  items feeds it, and For each takes lists only.
- **Flow**: v1's If, Wait (now a duration), Wait for life and Finish; Sequence (its outputs one after another, "+" adds
  one); Switch on a value (an output per case, typed in the node, and Otherwise); Repeat N times (its index), While,
  For each element of a list (the element, its index), each with its body and Completed, and Leave the loop; Do once
  and Do N times (each re-armed by its Reset input); Alternate (A, then B, then A); Wait until a condition, with an
  optional longest wait (Then, or Too long); Retry an action up to N times, then Failed; Pick from a list, in turn,
  the least recently picked or at random (its memory per node, for the run); Trigger an event of mine, now or after a duration, and Cancel its timer;
  Block with a reason; Pause and notify (the player resumes it, from step 4). Gate, MultiGate and Unreal's rarer
  nodes stay out: a bot hardly uses them (the owner, 2026-11-16).
- **Variables**: a duration of one run, or **memory** per character; set per character or not; and **local** to a
  function, gone at its return. Beside Get and Set: Add N to, Reset to its default, and a list's Add, Remove and Empty.
- **Events**: v1's six; **When … becomes true**, on any condition built of values and queries, with a priority and a
  mode, firing as it turns true and re-armed when it turns false or, through its optional "Re-arm when" input, when
  that turns true (hysteresis, as Life below); **Every** a duration; **At** a time of day; the map changed (its map and
  sub-area); a fight ended (won or not, from the fight end's results, the pin
  that the death needs too, `artifacts/studies/death.md`); a level gained; reconnected (step 4); an item obtained and a profession's
  level reached (after the inventory's and the professions' studies, step 5). **Events of mine**, declared in My Blueprint with their inputs, each a node of the event graph with
  its priority and mode, are fired by Trigger, at once or by a timer; a timer lasts for the run. Life below and Pods
  above stay, ready-made.
- **Queries over `game::Live`**: v1's life %, pods %, level and a profession's resources; life, its maximum, pods and
  their maximum, kamas, energy, the level's experience %, the current map, its sub-area, whether in a zone, its X and
  Y, whether in a fight, the monster groups on the map (up to a level), the resources harvestable here, the time and
  how long the run has lasted; the run's own counts (harvests, fights won and lost, kamas earned), which the runtime
  keeps. What D3B does not observe yet is listed and marked, studied and pinned at the step that reads it (the
  owner, 2026-11-16): whether in the haven bag and the zaaps known at step 4; an item's quantity in the inventory, what
  is equipped and a profession's level and experience at step 5 (with the bank's content); the quests at step 7.
- **Actions**, each Done or Failed with its reason, marked with their step, each studied before it is done: go to a
  zone, to a map, follow a route N laps; go to the haven bag, take a zaap to a map, save a zaap, go through a door or
  use an interactive element (its skill), talk to an NPC and give its replies in order, leave the dialog (step 4, an
  **NPC** a type); harvest until, harvest this map, go to the bank, deposit all but, withdraw, deposit to the haven
  bag's chest, use an item, open the resource bags, equip the best I carry for my level, equip an item or a preset,
  set the game's own "experience received" (step 5); hunt until, attack a group (its levels), fight with the default
  AI (won is Done), leave the fight (step 6); craft N of an item, sell items at the auction house (step 7); notify,
  and note in the History (step 4); destroy items (step 5); switch to another blueprint (step 4, the owner,
  2026-11-16): the character's file then names it, its settings carried as when the tab chooses one, and it starts
  from its On start. Choosing a fight's challenges waits for step 6's design.
- **The fight AI's direction** (the owner, 2026-11-16, from the reference scripts' fight rules), designed at step 6
  with the fight's state, the spells and the effects: a **fight profile** is a value edited as an ordered list of
  rules (a spell, a target, a cell, typed conditions, how often it repeats, whether to move to cast), the first
  that holds is played and the list read again until none does; D3B ships one per class, shareable, and **the
  default AI is the character's class profile**; for advanced players, an event "On my turn" overrides it in the
  graph. The fight profile (a type) and "On my turn" (an event) are marked step 6 until then.
- **Routes** ((21), the owner, 2026-11-16): an anchor may hold an action (harvest, fight, nothing, or call a function
  of mine) and a forced exit (a door, a cell) where the world graph is not enough; Follow the route resumes from the
  map the character is on, as the scripts' "on map X, do Y"; a route is recorded while playing (the relay observes the
  maps crossed and makes them its anchors), and pasted or copied as text (`5,-18 left; 4,-18 …`, or maps' ids).
  An anchor holds its map's id, shown by its coordinates, since a building's inside and its street share them (pasted
  coordinates that two maps share take the one the world map draws, and say so); a
  forced exit's cell is picked on the map.
- No library of functions shared between blueprints: functions and per-character settings already cover the
  scripts' copies.
- **Functions** gain **named exits** beside Done and Failed ("Full", "Empty"), each a flow output of their call;
  **locals**; and the **pure** function, which computes values without flow, actions or waits, its call a value node.
  No macros: a function already holds actions that last.
- **Practical, intuitive, complete** (the owner, 2026-11-16), the bar of every kind: few kinds, each well named and
  running as it is dropped, from its defaults (an input no default can fill, a zone, a map, an NPC, an item, stays
  required, its problem saying what to choose); a node shows its main inputs in its sentence and folds the others
  under "Advanced" (a longest wait, a re-arm, a group's levels), unfolded as soon as one is wired or changed, as
  Unreal's advanced pins; data editors (routes, ranges, item lists) rather than long chains of nodes. The **picker
  groups by intent** (the owner, 2026-11-16): Move, Harvest, Bank and items, Fight, Wait and repeat, Conditions and
  calculations, Lists, then My variables, My functions, My events; "Often here" first by context, extended to v2's
  kinds; its search finds synonyms too ("boucle" finds While and For each). A node keeps its family's colour.
- **D3B's blueprints** (the owner, 2026-11-16), all configurable, all running as given with no setting changed:
  **Récolte** (every profession's resources the character can take, the Base ones such as wells left out as in the
  C# version), **Combat** (monsters for experience and loot), one per profession, **Mineur**, **Paysan**,
  **Alchimiste**, **Pêcheur**, **Bûcheron**, and **Chasseur**, the profession (job 41: fights with a hunting weapon
  for meat, after its study). This reverses 2026-11-14's one profession blueprint, its profession a setting. Where
  they go is, by default, **ranges computed from the data** as the app runs, never written in the blueprints'
  committed files: a resource's or a monster's worth halved every 20 levels below the character's and nothing above
  it, Combat's sub-areas holding at least 10 maps of the world map (no dungeon), reckoned again each tour; the
  monsters' levels and drops come from the extractor's **format 5** (the owner, 2026-11-16). A setting replaces them
  by ranges, a zone or a route of mine. Five or six settings show, those changed often: where, the pods' threshold,
  the bank (the nearest, a map, or the haven bag's chest) and the items kept, fighting on the way, the goal (a
  profession's or the character's level, a number of harvests or fights); the others fold under "Advanced",
  unfolded when one differs from its default (the owner, 2026-11-16; the C# version reached 24 and hid half): the
  order a map's resources are taken in, the rotation, the zaaps, food, the life to wait for, what follows a lost
  fight, the monsters chosen or left out, equipping the best carried for the level (on) or a named preset, an
  optional night pause, restocking (N of an item kept on the character, taken back from the bank after a deposit,
  step 5), items to destroy now and then to free pods (step 5, none by default; destroying cannot be undone, which
  the setting says, and the History notes each), and from step 6 the fight profile, the class's by default. Every
  resource of the profession is taken where the character is (only the place follows its level), and Combat attacks every group its size and the monsters left out allow, no bound of level by
  default, one optional under "Advanced" (the C# version's D387: the zone chosen for the level bounds the risk).
- **What the C# version measured**, carried into the actions of steps 5 and 6, its values starting points to measure
  again (the owner, 2026-11-16): the **rotation** over the N richest sub-areas of the level, the one left longest ago
  first (D391, D392); **regrowth learned** by skill while playing, a tour waiting on the longest seen and stopping on
  the way at a resource offered (D145); **losses and banks made safe**: after a loss, wait for the life to resume
  at, three losses in a row stop, a bank tried three times and still full stops with the reason (D198, D405);
  **interiors**: a house entered only when known to hold what the level can take, mines kept reachable, Combat out
  of interiors but dungeons (D388, D416, D417). Its notes: `csharp-settings-for-v2.md` in the sessions' notes.
- **Kept out**: anything about other players (counting them, fleeing them, answering them), and what `CLAUDE.md`'s
  "Never" lists.

At run time ((25) and (26)): a loop's body ends where its flow does (an output wired to nothing) and the loop goes on;
Leave the loop goes to its Completed; Sequence runs its next output when the previous one ends. **10,000 nodes in a
row without the flow waiting** on the clock or on the observed state block the character, "runs without pause", on the
node (the owner, 2026-11-16): an action already done, a Notify or a Wait of 0 s does not count as waiting. A When's condition and a Wait until's are read again at each change of the observed state and each
second; Every and At follow the clock, At once a day. An interrupted flow keeps its place, its loops' counters and its
calls, so "interrupt then resume" runs the action cut again inside its loop. A random value is drawn when its node is
read and shown by the debugger; reversed bounds are swapped. A value that cannot be computed (÷ or remainder by 0,
an element out of its list, the first of an empty list) blocks the character with the reason and the node, as an
unlinked Failed does. Times are the computer's local time; "between 23:00 and 08:00" goes past midnight. Memory variables, lists too, are saved in the character's file at safe points.

The **Summary** reads a loop's or a Sequence's body as sub-steps ("1.1", "1.1.1") with Completed after them, a Switch
as one line per case ("Bûcheron → 2.1", "Sinon → 2.4"), Trigger as "Déclencher « Zone vide »" or "Dans 10 min, …"; a
section per event of mine by priority beside the game's; values are read in place in the sentence that uses them
("Tant que récoltes < objectif"), never as steps.

In the file (`bot::json`, **schema 2**, its shape settled alone, open to change until (v2.1) merges): a variable, a
parameter or a local holds `"list": true` for a list; a node's own pins (Sequence's outputs, Switch's cases, Text's
holes, a list's inputs) and the event, variable or exit it names are its `options`; the blueprint's own `events`
beside its `graphs`, each with its id, name and inputs; a function's signature gains `exits`, `locals` and `pure`. A
schema 1 file reads migrated ("at least" to `≥`, Wait's minutes to a duration) and is written as 2 at its next change;
D3B's blueprints ship as 2. A node still does one thing (`kind`, `get`, `set` or `call`), with `options` beside;
the pins its options make are numbered from 1: Sequence's `then_1` to `then_n`, Switch's `case_1` to `case_n` (the
cases' values in `options.cases`) and `otherwise`, a list's inputs `in_1` to `in_n`; Text's inputs are its holes'
names (`{count}` is the pin `count`), a hole renamed or removed dropping its wire. A call has a flow
output per exit of its function, the exit's id its pin's, beside `done`, `failed` and `reason`; a function's locals
live in its call's frame. A pure function's call has no flow pin and is read on demand, as a query is, its value kept
for the debugger. New problems: an exit named as a built-in pin (`run`, `done`, `failed`, `reason`); wildcards of one node fed two types; a list into a pin that takes none; an
option naming an event, variable or exit the blueprint lacks; a case typed twice or of another type; Leave the loop
outside a loop's body; a flow, action or wait in a pure function; a time not "HH:MM"; an Expression that does not
parse, names what the blueprint lacks or is not of its pin's type; ranges whose rows overlap.

## In the app

The shell's parts (`docs/shell.md`); no information is shown twice:

- **Characters** view: the characters the player has played, a closed client's greyed with its blueprint and values
  kept (a change: step 2's view lists open game connections only). From step 4, a row's hover holds Play, Pause and
  Stop, and the view's header Play all and Pause all.
- A **character's tab**: its blueprint (a list, and an arrow that opens it) and its settings above its observed state
  and map (`docs/game.md`), as the prototype laid them out (step 3's PR 11, the owner's choices of 2026-11-16): "No
  blueprint" at first; each setting by its name, its help on hover, the character's own value marked by a bar on its
  left and reset by a button saying the blueprint's value; a Zone by `ZoneField`, a number or percentage in a field
  within its bounds (one out of them marked and put back), a yes or no by a switch, a text by a line, a profession by
  the game's harvesting professions with their icons; a bank and items say their value, tagged "étape 5", until the
  step that uses them brings their editors. An offline character's tab holds the same, editable. From step 4: one line saying what it does now (its step, on what, since when), a tick on
  the Pods gauge at the current step's target, and Play, Pause and Stop as the tab's editor actions.
- **Library** view: "Given by D3B", then "Mine"; a row opens the blueprint's editor. A row is its name alone, its
  description or what a copy is based on in its hover, an amber mark with how many problems it has; a right-click
  offers Duplicate and Export…; Import… in the view's header (step 3's PR 22); last, folded, "Ignored": the player's files kept as they are, blueprints' and characters', each
  with why in its hover (the owner, 2026-11-14, mock-up 9). A blueprint's tab is its editor, D3B's read-only and mine
  edited in its graph (step 3's PR 12): on top its name, whose it is, its problems, its description, Duplicate and Export; under them My Blueprint (the event
  graph, the functions, the variables with an eye on those set per character, a variable's help and default in its
  hover) and the graph chosen in it, and Details on the right (the owner, 2026-11-14): a node chosen says its sentence,
  its description, the step from which the bot does it, its inputs' values (or "wired"), what an unlinked Failed does,
  its problems, and for an event its priority and its four modes drawn as timelines, the one it has marked; a variable
  chosen in My Blueprint its type, default, bounds, whether each character sets it and its help, how long it lasts and
  the nodes that use it; nothing chosen, the blueprint's problems, each in the player's words. A node with a problem is
  marked in the graph. "Graph | Summary", the tab's editor action in the tab strip (the owner, 2026-11-14), shows the
  **Summary** in the graph's place: a section per event, "On start" first and the others by priority, with their mode;
  its steps numbered as the flow runs them, an action's Failed and an If's True and False numbered under their step
  ("2.1"), a node met again said "Back to 2" (or "Go on to 4" forward), a step's "étape N" tag; the events the game
  forces, when the blueprint leaves them, with their default (a fight by the default AI from step 6, then resume; a
  death stops the run; an unlinked Failed blocks with the reason); then each function from its entry. A step chosen
  shows its node in Details.
- The **blueprint editor** is Unreal's Blueprint Editor in a tab. Graph or Summary are its editor actions; its toolbar holds
  the graph's path, the problems, the character to debug, and Duplicate on a given blueprint, which opens locked. **My Blueprint** on the
  left lists the graphs, functions and variables, an eye on those set per character; the graph is in the centre,
  **Details** on the right. Nodes are sober sentence boxes: an icon coloured by family, the values in the sentence,
  typed pins (a triangle carries the flow, a circle a value, coloured by type), a visible Failed output. A wire dropped
  on the ground, Space or a right-click opens the picker: sentences with their description, what often comes there
  first; an action the bot does not do yet is marked with the step that brings it (the owner, 2026-11-13). "If" is a node with True and False; a function opens in place, its inputs and outputs as nodes. An event's
  Details hold its priority, its re-arming and its four modes, each drawn as a small timeline. **Summary** reads the
  blueprint as sentences. The IPC's `blueprint_graphs` gives the editor each graph's nodes (what each does, its family,
  the step from which it runs, its pins, the values typed and its place) and wires, typed; a blueprint's variables come
  with the Library's list; its problems, each with its kind, graph, node, pin and what it names, an event's priority and mode, a function's
  inputs and outputs, and a variable's bounds, for the Details (PR 8). The editor's changes (step 3's PR 12, the owner's
  choices of 2026-11-16) go through the IPC's `edit_blueprint`, one at a time, each saved at once into a blueprint of
  mine (D3B's are refused, locked), which gives back the graphs and their problems: a wire, checked by what its pins
  carry, in place of the one that fed its input (and of the value typed there) or left its flow output, since an input
  has one value and a flow one way on; a node the picker offers, where it opened, under an id read from what it does
  (`notify`, `notify_2`, `get_zone`), wired to the first pin of its that fits the one a wire dropped on the ground came
  from; nodes moved together, one change; nodes and wires deleted together, a node's wires with it (step 3's PR 13).
Undo and redo (the owner, 2026-11-16) are Rust's: `storage` keeps a blueprint of mine as it was before each change
that changed it, 100 at most, for the session and across its graphs, and the IPC's `undo_blueprint` and
`redo_blueprint` write it back whole under the lock every save takes, so the file and the editor never disagree; a
deletion is not confirmed, undo brings it back. Each graph gives the picker its **choices** (`bot::choices`) with their pins: the catalogue's
  kinds in their place, an event held once never again and a function's entry once, then the blueprint's variables,
  read and written, and its functions called; creating a variable comes with PR 14. In a graph of mine, a wire
  dragged from a pin is checked by type as it goes: a pin that refuses it says why beside the pointer (another type,
  two ends of one side, a node to itself); dropped on a pin, it is made; dropped on the ground, it opens the picker
  there, as Space (at the pointer) and a right-click do: a search, "Often here" after a Failed (Notify, Wait, Finish),
  after another flow (If, Notify, Wait) or from a value (the first three that take or give it), then every choice by
  family, only what the dragged pin fits, the arrows and Enter choosing. A node dragged keeps its place. A change not
  saved says why, and the blueprint is read again. The graph is drawn with React Flow: each node where the file puts it (a column on the left for those it does not place), its sentence's
  holes filled with what feeds them (a variable's name, a query's own sentence, a source pin's label, a value typed or
  a default), a get as a pill of its type's colour, a wire going back (a loop) out to the right, under its two nodes and into its input from the left (the owner, 2026-11-16), an action the bot does not do yet tagged "étape N", pins and wires
  coloured by type (the owner, 2026-11-14). From step 4, "Debug" a character lights the node at work and its wire; nothing is lit otherwise.
- **World** view: my zones, my routes, then the game's sub-areas, searchable. "My zones" (step 3's PR 20, the owner's
  choices of 2026-11-16) lists mine by name with their maps, matched by the search too; "+" on its header makes
  "Nouvelle zone" on the world the tab shows and opens it there with the brush; a row opens its zone on its world, F2
  or a right-click renames it in its row (Enter or leaving saves, Escape leaves it), and the right-click deletes it to
  the system's trash, closing it on the map; a zone's file kept is listed in the Library's "Ignored". A zone of mine
  open shows on the World tab in translucent amber with a sharp outline, its name, "Zone of mine" and its maps on top
  left, the view fitted to it, and a floating toolbar on top of the map: Select (V), Brush (B), Eraser (E) and Whole
  sub-area (S), their keys taken wherever the focus is while the World tab is the active one, unless a field holds it.
  The brush and the eraser paint by a press and a drag, every map the pointer crosses (those of the world only), with
  the tool the stroke began with, shown as it goes and saved once the button is released (a stroke the system cancels
  is dropped); the right or middle button, or Space held with the main one, moves the map; the hovered map is outlined
  alone. The whole sub-area tool
  outlines the hovered map's sub-area and a click adds it, or takes it away when the zone holds all its maps there;
  Select chooses a sub-area as without a zone, which closes it, as another world does. Details then holds the zone's
  name, edited in place (Enter or leaving saves, a blank one or Escape puts it back), "Zone of mine · <world>", its
  maps, the sub-areas it touches with how many of their maps are its, by that share then by name (the floors of one
  map's coordinates may belong to two sub-areas, and the zone holds them all, so such a map counts in both), whose
  Zone it is, those sub-areas' resources
  together by profession, and Delete, to the system's trash. The **World tab** shows the world map
  from the data's tiles, at the level the zoom needs, with the client's **hints** as on the C# version (the owner,
  2026-11-13): zaaps, dungeons, auction houses, workshops, banks, class places and transports, each with the client's
  picture, at the zooms that have room for them, one switch per kind, the name on hover. Its tools: select, brush,
  eraser, whole sub-area, route; Details on the right. A Zone setting is chosen in a searchable list or by its target, which opens the map.
  The owner's choices of 2026-11-15 (mock-up 10): the map drawn by hand, its tiles as images moved and scaled, the
  level of tiles the zoom needs; one world at a time, the list of worlds being the tab's editor action, and a sub-area
  of another world switches to it; the view's sub-areas grouped by area in a folded tree, the search unfolding what it
  finds; the sub-area chosen in translucent teal with a sharp outline, the map around it untouched. The IPC's
  `world_atlas` gives the worlds the client shows (origin, a map's size, the image's size, the client's zoom bounds,
  each zoom with its tiles' files) and the named areas' named sub-areas that hold a map, each with its level and its
  maps' coordinates on each world shown. The World view lists the sub-areas under their areas by level, then by name (the
  owner, 2026-11-15), a level on each, the search unfolding what it finds; a click opens the World tab. The World tab (`src/world/`) shows the world its editor action chose, fitted
  to the sub-area chosen in the view, its name, area, level and number of maps on top, or, for one of the 211 no world
  shows (dungeons, houses, hidden worlds), "not on the map" (the owner, 2026-11-15), or the world it is on. The client
  parks at 0, 0 the maps it draws nowhere (1,820 on the Twelve): only those flagged with the priority on the world map
  are truly there, and on a map two sub-areas share, a click and the hover take the one with that priority (the
  owner, 2026-11-16). The map takes the focus: the arrows move it, + and − zoom, 0 fits it again; its zoom
  shows bottom left, its buttons bottom right; a map's sub-area and coordinates on hover. The hints (the owner's mock-up 11,
  2026-11-15): the client's pictures, 28 pixels on the screen at every zoom; where two would overlap, the more useful
  kind stays, chosen over the whole world for a zoom so that moving the map never changes it; nine kinds, the picture
  read before the client's category, which mixes the zaaps with the boats and the banks with the taverns: zaaps,
  banks, dungeons, workshops, auction houses, class places, other transports, other places and passages, the last two
  off by default; their switches, with each kind's picture and count, behind "Hints" in the tab's editor action, for
  the session; a hint's name, and a dungeon's level, on hover in place of its map's. `world_atlas` gives each world its
  hints by kind, each with its name, coordinates, level and picture's file, the one the client lacks drawn as a dot.
  The game's sub-areas as zones (the owner's mock-up 12, 2026-11-15): a Zone's value is a sub-area's id or a zone of
  mine's (the blueprint's file, `bot::variable`). A Zone setting is a field saying the zone chosen, its area and level;
  a click unfolds a list under it, searched as the World view, "My zones" first by name, with their maps, then
  the sub-areas by area and level, the arrows and Enter choosing (a zone of mine gives `{"zone": id}`, said by its
  name, "Zone of mine" and its maps, or "Zone <id> not found" once gone); its target opens the World tab in a choosing mode, a
  banner saying for what, a map hovered outlining its whole sub-area, one click choosing it and going back to the tab
  it came from, Escape cancelling. A click on the map otherwise chooses its sub-area as the view does, the map staying
  where it is (a drag chooses nothing, nor a click off the maps), and **Details**,
  a column on the World tab's right, says what it is as a zone: its area, level and maps, its resources by profession
  with their icons and levels, how many kinds of monsters live there (their names need a data format the hunt, step 6,
  can bring), and, from step 3's PR 11, whose Zone it is: the characters whose
  blueprint sets a Zone per character that their own value, or its default, makes it ("Zone de Kalyx et Orrin"). `world_atlas` gives each sub-area its resources, the items
  of its `harvestables` that a profession's skill gathers, the loot boxes left out, by profession then level, and how
  many kinds of monsters it lists, with the professions and resources named and their icons' files. The field is
  `ZoneField` (`src/zone/`), which step 3's PR 11 puts in a character's tab; the World store holds the choice on the
  map under way, for whom and what takes the sub-area, and the tab active when it began, brought back when it ends;
  choosing on the map also shows the sub-area chosen there, and a map's hover outlines its sub-area dashed in white.
- The panel's **History** (step 4) follows the active character's tab. The **Protocol** view shows only with the
  "Advanced tools" setting, off by default (step 3's PR 24, [shell.md](shell.md#settings)); step 2's checklist items
  that read the inspector begin by turning it on.

The prototype fixed the model and the layout; each PR polishes its look, the World more beautiful and modern (the
owner, 2026-11-13).

## Storage

One JSON file per object in **Documents/D3B**, a schema version each, written atomically; History kept 7 days; a
`.d3b` file to share a blueprint with its zones and routes (ADR 0015). `storage` reads `blueprints/<id>.json` and
writes it whole (a `.d3b-tmp` file beside it, then a rename); a file that does not read, of a newer schema, or whose
name is not its id, in any case (a copy made by hand: the owner, 2026-11-14), is kept untouched, reported with why,
and never written over, as is one of mine holding the id of one of D3B's. The temporary file is flushed to the disk
before the rename, one save at a time. The folder is the system's documents folder's `D3B`, else the app's data
folder; the IPC lists D3B's blueprints, then mine, and the files kept with a typed reason the interface words.
The **`.d3b` file** (step 3's PR 22, the owner's choices of 2026-11-16, ADR 0018) is one indented JSON, `{"d3b": 1,
"blueprint": {…}, "zones": [], "routes": []}`: its format read first, then the blueprint's own file, read and checked
as one of the folder; zones and routes empty until PRs 20 and 21 fill them with those the blueprint names (at import,
one identical to mine reused, another under a taken id given a free one, the blueprint rewritten to name it), and a file
that holds some refused by a version that cannot import them. Export… (a row's right-click in the Library, or beside
Duplicate in its tab) writes it where the player chooses in the system's save dialog, named by the blueprint; one of
D3B's goes as mine based on it, in the player's words, under an id made from its name. Import… (the Library's header),
or a file dropped anywhere on the window, opens a **preview** first, a dialog: the name, description, what it is based
on, the settings per character then the other variables with their type and default, its problems in words, and what
it holds (graphs, nodes, zones, routes); Import puts it among mine under its own id when no blueprint and no file of the
folder holds it, in any case, else a free one, never over a file, and opens its tab; Cancel leaves all as it was. A file
not named `.d3b`, that does not read, not of its shape, or of a newer format or schema is refused with why in a
notification. The dialogs are `tauri-plugin-dialog`'s, called from Rust only: the webview has no permission for them.
A character played is `characters/<server>-<id>.json` (its id carries its server in its low 16 bits,
`artifacts/studies/character-key.md`): its id, name, level, breed, sex, when and on which map it was last seen,
written when it is first known, when who it is changes, on another map a minute after its last write, when
another character takes its connection and when the connection closes, by one writer thread so the protocol never
waits; a file that does not read is kept and listed. From schema 2 (step 3's PR 11; schema 1 reads as a character
without either) it also holds the blueprint the character runs and its own values of its settings, by variable, which
are the player's: a sight in game rewrites the rest and keeps them. The IPC's `set_character_blueprint` chooses a
blueprint, or none, under the same lock, keeping the values of the settings of the same id and type, and
`set_character_setting` sets one, refused when not of its type or out of its bounds, a value set back to its default
or reset dropped, so it shows unmarked (the owner, 2026-11-16). The IPC's `delete_blueprint` sends a blueprint of mine
to the system's trash, its undo history gone, without asking first, as Forget; the characters that run it keep naming
it, their tab saying it is not found, so restoring its file gives it back to them (the owner, 2026-11-16). From schema 3 (PR 23; schema 2 reads as
running a given blueprint's version 1) it notes `blueprintVersion`, the version of D3B's blueprint its settings were
carried to (none for one of mine), and `dropped`, the settings a new version could not keep, which
`dismiss_dropped_settings` clears; choosing a blueprint notes its version and clears them. Forget sends it to the system's trash (the `trash` crate; on
macOS through the file manager, without asking to control the Finder: the owner, 2026-11-14). A character's file is named by a key stable across
servers, from its study (below).
A zone of mine is `zones/<id>.json` (step 3's PR 20, the owner's choices of 2026-11-16): schema 1, its `id`, `name`,
`world` (the game's world map id, the Twelve's 1) and `maps`, its maps' coordinates on that world, each once, in order,
one a line so that a map painted changes one line; every floor of those coordinates counts, and the runtime resolves
them into the game's maps. It is created empty on the world the World tab shows, under an id made from its name as a
copy's is (a number when any file of the folder holds it, in any case), and keeps its id when renamed. The IPC's
`zones` lists them with the files kept; `create_zone`, `rename_zone` (a name of nothing but spaces refused),
`paint_zone` (a stroke: the maps added, or taken away by the eraser, saved at once) and `delete_zone` (to the system's
trash, without asking, as Forget). A file that does not read, of a newer schema, or whose name is not its id is kept
untouched, listed with why, never written over nor sent to the trash.
The app's own settings are `settings.json` at the folder's root (schema 1, step 3's PR 24,
[shell.md](shell.md#settings)), written whole under the same lock; one that does not read gives the defaults, is
reported at start and is never written over.

A blueprint's file (`bot::json`, schema 1, the owner's choices of 2026-11-13): its `id`, `name`, `description`,
`version` and `based_on`; its `variables` (`type`, `default`, `bounds`, `per_character`, `memory`, both allowed at once
as in Unreal, and `help`); its `graphs`, an event graph or, with a `function` signature, a function, each a list of
`nodes` and of `wires` from `[node, pin]` to `[node, pin]`. A node does one thing: a catalogue `kind`, or `get`, `set`
or `call` of the blueprint's own variable or function; its `inputs` hold the values typed on pins no wire feeds, and an
event's `event` its priority and mode. Where nodes sit is under `layout`, by graph then node, at the end of the same
file, so a moved node changes only those lines. Ids are lowercase letters, digits, `_` and `-`. A file of a newer
schema, not JSON, or not of its shape (a field missing, unknown or given twice, a node doing two things) is refused; one that reads but does not hold (an id
twice, a wire to no node, a default its type or bounds refuse, a node on a variable or function the blueprint lacks)
opens with its **problems** and cannot run; a node kind this version does not know is one of them.

The **catalogue** (`bot::catalogue`, the owner's v1 list of 2026-11-13) holds 25 kinds by family: the events on start,
life below, pods above, a fight starts (forced: the default fight, then resume), died and an action fails (forced:
blocked with the reason); the actions go to a zone (step 4), harvest until, go to the bank and deposit
all but (step 5), hunt until (step 6), and notify; If, Wait, Wait for life (sitting no longer gives life back
faster: the owner, 2026-11-14) and Finish; the queries life %, pods %, level, at least,
add and the resources of a profession; a function's entry, done return and failed return. Flow pins are `run` in, `then` out, an action's `done`
and `failed` with its `reason`; a get has `value`, a set and a call their variable's or function's. Rust holds ids
only: sentences, descriptions and icons are the interface's. A wire carries one type, but a percentage feeds a number.
The catalogue adds its problems: a kind unknown or out of place (an event in a function), a priority on a node that
takes none, a value on no input or not of its type, a wire to no pin, the wrong way or of another type, a flow
output wired twice, an input fed twice or by nothing, an event held once (on start, a forced one) held again. v2
(above, "The language, v2") extends this catalogue and the file to schema 2.

## Measure

On Windows x64 by the owner, on what step 3 shows; running a blueprint is measured with step 4's first actions:

1. The Library lists D3B's blueprints; one opens locked, its graph and its Summary readable.
2. Duplicate makes one in "Mine", editable; a node added, linked and saved is still there after a restart.
   The files are in `Documents/D3B/blueprints`, also in a Documents folder OneDrive syncs; on macOS, after its
   access prompt.
3. A character in game gets a blueprint in its tab; a setting changed is marked, its reset clears the mark; a second
   character keeps its own value; both are still there after a restart.
4. The World's map is sharp at every zoom; a sub-area is chosen as a Zone from the list and on the map; a zone painted
   and a route of four anchors are still there after a restart.
5. A `.d3b` exported, then imported on another D3B, shows its preview, then the same blueprint.
6. v2, once (16) declares variables and events of mine: in a blueprint of mine, a While on a goal holding a For each over a list of zones, a Switch on a profession, a
   When on "time ≥ 23:00 AND not in a fight" and an event of mine fired by a timer are drawn without a refused wire
   and read in the Summary as on the owner's mock-up of 2026-11-16; a file of schema 1 opens migrated, its
   "at least" a `≥`.
7. v2: Space opens the picker grouped by intent, "boucle" finds Tant que and Pour chaque; a Wait until dropped shows
   its condition and folds its longest wait under "Avancé", unfolded once set; the Library lists D3B's eight
   blueprints, each without problems and its settings at defaults that work (running them is measured at steps 5
   and 6).
