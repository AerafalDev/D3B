# Now

_Updated 2026-11-23._ Under 150 lines (`bun run rules`): the step, what is done, what comes next, and the open
checklists. History is in git and the ADRs, design in the domain pages, measures in [checklists/](checklists/README.md).

## The step

**Step 3, the bot's model** ([ROADMAP.md](../ROADMAP.md), [bot.md](bot.md), ADRs 0014 to 0019), started on 2026-11-13 by
two exceptions of the owner to "the next feature does not start": step 2 is measured on Windows but for items waiting on
outside events. Today's feature is **blueprints v2's (v2.4), the values** ([bot.md](bot.md#the-values-v24)); its
remaining PRs finish a started feature and carry `step-finish`.

**The cap** (`CLAUDE.md`; the owner, 2026-11-23): more than five checklists are open (`bun run rules` counts them), so
no new feature PR until at most five are, step 4's first slice included; fixes, docs and v2.4's remaining PRs go on.
The owner's session of tests closes them.

## Done

Steps 0 (the shell, typed IPC, packed data, i18n, the Claude setup, CI, release-please, six targets, signed updates) and
1 (the extractor) are done and measured on Windows x64; the data is now in format 6, the NPCs added for step 4's R7 ([data.md](data.md)); its asset waits for the owner's go. Step 2 (the
relay, the protocol and its pins, the live view, the inspector, the new shell) is done in code and measured on Windows
x64 but for the items below. Step 3 is done in code up to v2.4's Text and Join: the blueprint and its storage, the Library,
D3B's three blueprints, the editor (graph, Details, Summary, picker, undo), the World and my zones, a character's
blueprint and settings, the `.d3b` file, the runtime on the captures, v2.1 to v2.3, and v2.10's ranges; the blueprints'
whole catalogue (ADR 0019) and step 4's first slice (ADR 0021) are designed. Pre-releases up to v0.14.0
([updates.md](updates.md#pre-releases)); data assets in [data.md](data.md#published).

## Next

One line per item, in order; each is designed in [bot.md](bot.md) or [play.md](play.md).

- v2.4: drawn compact.
- v2.4: sentences in place, and its checklist; the language is frozen there (ADR 0019, the owner, 2026-11-23).
- Step 4's first slice, F1 to F10: the game layer reading every family the C# version reads, paths, typed acts
  ([play.md](play.md#the-stack), ADR 0021); F8, the cell path, done ([game.md](game.md#the-paths)).
- Step 4's first slice, R1 to R9: Récolte automatique playing on Windows; its measure, a 30-minute loop unblocked.
- (26) events at run time: priorities, modes, thresholds, settings at safe points, When, Every, At, Trigger; an
  event cutting a long action at its next inner step (ADR 0020).
- The catalogue's slices, each node with its first user ([bot.md](bot.md#the-catalogue-complete-2026-11-23)): 4·Play
  (History, debugger), 4·Travel, 5·Récolte (and the five profession blueprints), 6·Combat (and Chasseur), 7.
- (14) variables, with locals, lists and the new types' editors.
- (15) functions opened in place, with their exits and the pure ones.
- (16) events: priority, re-arming, modes, the defaults, and the events of mine declared.
- (17) comments and reroutes.
- (18) Align, copy and paste, search.
- (19) editing by keyboard.
- (21) routes: an action and a forced exit per anchor, the recorder, the text import and export.
- Step 2: Q17, a build that changes the schema, at a later patch ([protocol.md](protocol.md#on-the-wire)).

## Open checklists

One line each: what is left to measure. Step 3's own measure is [bot.md](bot.md#measure), taken with these.

- [v2.2, the editor reads it](checklists/v2-2-editor-reads-it.md): open, items 1 to 7.
- [v2.3, the flow's kinds](checklists/v2-3-flow-kinds.md): open, items 1 to 5.
- [the protocol](checklists/protocol.md): open, item 7 with v0.14.0 on client 3.7.14.18, before the next release.
- [the breed](checklists/breed.md): open, items 1 and 2 (the owner's extraction and `data:fetch`), 5 with v0.14.0.
- [the characters played](checklists/characters-played.md): open, items 1 to 6.
- [the Library](checklists/library.md): open, items 1 to 6.
- [the World](checklists/world.md): open, item 8's pictures, unticking, "Tout cacher", "Tout montrer", Incarnam.
- [my zones painted](checklists/zones-painted.md): open, items 6 to 8 and the parts of 2 to 5 not tried.
- [undo and redo of my zones](checklists/zones-undo.md): open, items 3 and 5, and the parts of 4 and 6 not tried.
- [a character's blueprint and settings](checklists/character-blueprint.md): open, item 9, the parts of 2, 5, 7.
- [deleting and undoing in a graph](checklists/graph-delete-undo.md): open, item 1, the parts of 2 to 5 not tried.
- [values typed on a node's inputs](checklists/node-values.md): open, item 4, the parts of 2 and 3 not tried.
- [my variables](checklists/my-variables.md): open, items 1 and 4, the parts of 2, 3 and 6 not tried.
- [renaming a blueprint of mine](checklists/rename-blueprint.md): open, the parts of 1 to 3 not tried.
- [Advanced tools](checklists/advanced-tools.md): open, items 4 and 6.
- [the Protocol panel's opening](checklists/protocol-panel.md): open, item 5, the parts of 1 and 3 not tried.
- [a given blueprint's update](checklists/blueprint-update.md): open, items 1 to 3; 4 waits on a given v2.
- [the `.d3b` file](checklists/d3b-file.md): open, items 4 and 6, and the rest of 5.
- [the relay](checklists/relay.md): waiting, item 10's offer on the release after v0.14.0, 11 on an accented account.
- [macOS, Linux and ARM64](checklists/platforms.md): waiting on testers.

Measured, kept as records: [step 0](checklists/app-and-updates.md), [step 1](checklists/step1-extractor.md),
[the life](checklists/live-view-life.md), [the graph edited](checklists/graph-edited.md).
