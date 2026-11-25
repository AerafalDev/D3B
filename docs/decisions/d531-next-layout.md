# The studio made simpler, next's layout (D531)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the style of
[D530](d530-next-dark-islands.md#d530-the-studio-takes-nexts-dark-style-vs-codes-dark-modern-as-discreet-islands-and-stays-dark-only).

## D531: The studio takes next's layout: a thin title bar with the layout toggles, the views alone in the activity bar, tabs on the frame, no tab at start

*2026-11-25, the redesign of the next release, stage 6; **the owner's**: "faudrait reprendre le même layout que next aussi, next
est mieux sur tous les points". Refines D322, D338, D339, D406, D412, D513, D514 and D516.*

- **What was taken from `next`'s shell** (`docs/shell.md`, `src/shell/`), its layout, not its code:
  - a title bar of 35 pixels: the icon and the menus, the command centre (the search) in the middle, the layout toggles (the
    side bar, the bottom panel, the Details) pressed while their part shows, then the window's buttons; the run block leaves
    it, staying the model behind F5, F6, a character's tab and its home card;
  - the activity bar holds the side bar's views alone, the settings' gear at its foot as VS Code's Manage; the journal is the
    bottom panel's, its toggle in the title bar, the statistics open from the View menu and the search;
  - a side bar's header names its view small and muted, its tools and the button that hides it after;
  - the editor's tabs lie on the window's frame, the one shown rising with rounded top corners into the editor's island;
  - the studio opens on no tab, the editor's island showing the mark faint and the main keys; the home page opens from the
    View menu;
  - a status bar of 24 pixels, the app's name and version after a small blue dot and the host's state on the left, the client
    build, the saving and the update on the right;
  - a character's tab with its state column on the left (who it is, its profile, its start on connection, play, then what it
    does) and its map on the right;
  - no island outlined in the accent when pressed.
- **Kept**: the Details panel (next's secondary side bar), shown by a selection; the bottom panel's journal, trace, problems and
  Sequencer, which next does not have yet; the world's viewport.
- **Seen** in the running app on the owner's project (the opening on no tab, the Characters view, the toggles, the status bar),
  closed by its window so that the client's configuration came back, and in the headless drawings (`ThemeViewsTests`, `StudioViewsTests`).
- **Rejected.**
  - Removing the home page: its characters' cards and recent assets are still the studio's quickest way in; it opens from the
    View menu instead of at start.
  - Keeping the run block beside the toggles: `next` has none, and a character's tab and its card now play it.
