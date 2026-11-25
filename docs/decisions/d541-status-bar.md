# The studio's shell after next, the status bar (D541)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the status bar of
[D531](d531-next-layout.md) and the palette of [D540](d540-command-palette.md).

## D541: The status bar is VS Code's: the version and the relay on the left, the client and its data, the bots playing, the problems, the saving and the update on the right, each item opening what it names

*2026-11-25, the studio's shell after `next`, stage 2 of 4; the owner's « Oui, les 4 premières » and « je te laisse faire les
choix tout seul »: **taken by the session under that delegation**. Refines D531 and D173.*

- **What `next` has** (`src/shell/status-bar/`, `src/relay/relay-item.tsx`, `relay-hover.ts`, `src/updates/update-item.tsx`,
  read-only): 22 pixels on the frame; on the left the app's name and version after a small blue dot, then the relay, « Relais
  prêt · N en jeu », red with an error icon once stopped; on the right the client build of its data, then « Redémarrer pour
  passer à … » once an update is downloaded. A hover says only what the bar does not: why the relay stopped, that quitting ends
  the game connections, to start Dofus after D3B.
- **What was made** (`StatusBarViewModel`, `StatusFacts`, in `D3B.App.ViewModels.Studio`): `next`'s left side and its relay's words
  and hover, the host's state behind them (starting, ready with the characters connected, stopped in red with the host's failure,
  closed); on the right the client build and, once the host opened it, the data pack's format (« Client 3.7.14.18 · données v13 »,
  its extractor in the tooltip), the bots playing (« 1 bot actif »), the problems as VS Code counts them (errors, warnings), whether
  everything is saved, and the update's offer with its version. The bar keeps 24 pixels (D531).
- **Each item opens what it names**: the version the settings' About, the relay the journal, the client and its data the settings'
  Files, the bots the Characters view, the problems their tab, the saving item saves everything, the update restarts into it.
- **The problems are read at every refresh**, not only while the bottom panel shows, so that the bar counts them; the
  `ProblemsViewModel` reads them again only when the project, the accounts or the host change.
- **Seen** in the headless drawings (`StatusBarViewTests`, `D3B_DRAWINGS`), not in the running app: starting it would take over
  the client's configuration while the owner plays.
- **Rejected.**
  - The host's own words (« En marche ») beside the relay's: the relay is what the player meets, and the host's detail stays on
    the home page.
  - Hiding the problems' item at zero, as some editors do: VS Code keeps its counts, so that the problems are always one click away.
  - An icon of the relay drawn for it: the icons are written by `scripts/icons.cs` (D532); the host's dot, already drawn, says its state.
