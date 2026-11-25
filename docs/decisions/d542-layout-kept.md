# The studio's shell after next, the layout kept (D542)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the layout kept by
[D513](d513-studio-navigation.md) and the status bar of [D541](d541-status-bar.md).

## D542: The editor's tabs, the one shown and the bottom panel's tab come back after a restart, once the host runs; a tab whose subject is gone falls back quietly

*2026-11-25, the studio's shell after `next`, stage 3 of 4; the owner's « Oui, les 4 premières » and « je te laisse faire les
choix tout seul »: **taken by the session under that delegation**. Refines D513.*

- **What `next` keeps** (`src/shell/layout-store.ts`, read-only): nothing on disk; its parts and side bar's view live for the
  session. The studio already kept more since D513: the edges' sizes, the parts shown, the side bar's view and the advanced tools.
- **What was made.** `app.json` keeps the editor's tabs in their order, the one shown and the bottom panel's tab
  (`StudioTabsState`, `AppPreferences.Tabs`), written whenever one changes, a profile never saved left out. They come back on the
  refresh after the host runs and its modules' pages are read, since most of them show what the host holds (`MainViewModel.RestoreTabs`).
  The bottom panel's tab comes back without opening the panel, which `StudioLayoutState.Shown` opens or not.
- **A view that no longer exists falls back quietly**: a profile deleted, a character forgotten, a module's page no longer
  registered, a list of steps gone are left out with no word; the tab shown, gone, leaves the last one; the Sequencer, which needs a
  route selected, and the protocol without the advanced tools leave the journal.
- **Nothing is written before the tabs came back**, so that a tab opened while the host gets ready, or a host that fails, never
  forgets the last run's.
- **Rejected.**
  - Opening the tabs that need no host at once and the others later: their order would change, and the home page or the settings
    would show alone for a moment.
  - Keeping the Details' state or a world's selection: the Details are shown by a selection (D513), which a restart does not keep.
