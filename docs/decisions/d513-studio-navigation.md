# The studio made simpler, its navigation and shell (D513)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The docks it changes are
[D321](d293-d358-step-7.md#d321-all-docked-characters-over-assets-the-editor-over-the-bottom-panel-the-inspector-on-the-right)
and [D397](d395-d397-step-7-the-redesign.md#d397-7r2-the-engines-frame-the-world-viewport-the-outliner-the-details-panel-and-play-in-editor).

## D513: The studio opens on an activity bar and one side bar, the bottom panel and the Details hidden until needed, the protocol an advanced tool

*2026-11-24, the redesign of the next release, stage 1 of 4; the owner asked for "une refonte globale de l'UI, pour la rendre
plus intuitive et plus simple (prendre exemple sur next)", then "je te laisse faire les choix tout seul": **taken by the lead
under that delegation**. Refines D321, D322, D327, D339 and D397; keeps D396 (the engine, the Sequencer, the words).*

- **What was found.** Read from the studio at `b120999f` and from the `next` branch's shell (`docs/shell.md`, `src/shell/`),
  read-only. The studio opened with five regions at once (the Outliner over the Content browser, the editor, the Details,
  the bottom panel and its six tabs), had no navigation but the Window menu, keys and the search, kept the statistics as the
  fifth tab of the bottom panel, and spoke an engine's words ("Outliner", "Assets", "Content") in French too. `next` opens on
  an activity bar, one side bar and the editor, the panel and the secondary side bar hidden, the protocol behind
  "Advanced tools". The whole study and the four stages are in the session's `artifacts/studies/ui-redesign.md`.
- **The plan, in four stages**, each a pull request that leaves the app working: (1) the navigation and the shell, this
  one; (2) the home page and the character's card, where the profile, play and the start on connection join the character;
  (3) profiles and routes, the library grouped by kind; (4) the settings regrouped and the statistics a tab of their own.
- **An activity bar** on the left of the window, 36 pixels of buttons, as `next`'s: Characters, Library, World at the top,
  each a view of the side bar, the one shown a rounded pill, chosen again it hides the side bar (Ctrl B); Journal, Statistics
  and Settings at its foot, the first two opening the bottom panel on their tab or hiding it, the gear leaving the title row.
- **One side bar** instead of the Outliner over the Content browser: the Characters view is the Outliner's characters, the
  World view its routes and zones (`OutlinerScope`), the Library view the assets' tree with its tools; its header names the
  view and counts those online. Ctrl Space shows the Library whether the side bar showed or not.
- **The bottom panel and the Details start hidden.** The Details show when something is selected (a character, a route, a
  step, a zone) and stay as the hand left them while it stays selected: closing them is not undone by the next step of the
  same route, a new selection shows them again. The bottom panel shows when its journal, statistics, trace or a route's
  Sequencer is asked, as before.
- **Advanced tools**, off by default, a check in the View menu, the search and the Interface section of the settings: the
  Protocol tab of the bottom panel only with them; turned off while it shows, the journal takes its place.
- **Plain words.** The Window menu becomes View (« Affichage »): Home, the three views, the three parts with their keys,
  Advanced tools, the modules' pages and the default layout. "Outliner" and "Assets" leave the screens for Characters,
  Library and World (« Personnages », « Bibliothèque », « Monde »); the logo in the title row opens the home page.
- **`app.json`** keeps the side bar and the bottom panel shown or not (`Shown`), the side bar's view (`View`) and the advanced
  tools; the default is still not written. The Details are never kept: shown by a selection, they would open the next start on
  nothing selected (found by the review). A file of an older D3B gives its widths back, hides the side bar only when it closed both left panels, and
  starts with the bottom panel and the Details hidden, the new defaults.
- **Rejected.**
  - A side bar per view kept alive at once (two Outliners): one Outliner whose scope follows the view keeps its rows, folds
    and filter, and reads the world once.
  - Layout toggles in the title row, as `next` has: the activity bar already shows and hides the side bar and the bottom
    panel, the Details follow the selection; three more buttons would crowd the run block. The View menu and the keys keep them.
  - Keeping the Details shown at start: on an empty selection they said "Choose something to see its properties" most of the
    time (D395's own finding).
  - Hiding the trace and the problems with the protocol: the character's tab and the problems' fixes lead to them.
- **Not measured**: whether the owner finds it simpler (their use is the check); the app was seen through its headless drawings
  and run once (see the pull request).
