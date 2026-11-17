# The shell

The app's interface is rebuilt from scratch in VS Code's layout, one piece at a time, each decided with the owner and
shown to him before the next (the owner, 2026-11-12). The step-2 interface it replaces was removed (#185).

## Layout

VS Code's, part for part (`src/shell/`):

- **Title bar** (35 px): the app's icon and menus, the command centre, the layout toggles, and on Windows the window's
  buttons, drawn by the app since the window has no frame there (`src-tauri/tauri.windows.conf.json`, which repeats the
  main window's entry since Tauri replaces the array whole; a test keeps the two equal). The window keeps its shadow,
  which rounds its corners on Windows 11, and the window-state plugin restores everything but its frame, which former
  versions saved. Everything in the bar but its controls drags the window, and double-clicked maximises it. On macOS
  and Linux the system keeps its frame and buttons; on macOS its menu bar holds the menus, so none is drawn. The menus
  hold only what works: File, Exit; View, the palette, the three parts (the panel only with Advanced tools) and
  Advanced tools, checked while on. Measured by the owner on Windows 11
  (2026-11-12): our frame only, rounded corners, drag, double-click, the window's buttons.
- **Activity bar** (48 px, left): one entry per view of the primary side bar (`src/shell/side-views.ts`), a 20 px
  Lucide icon (Characters: a person, the owner's pick of 2026-11-13); the view showing is a rounded pill, and choosing it again hides the side bar, as in VS Code. Today:
  Characters, then the Library (Lucide's `library-big`, the owner's mock-up 9 of 2026-11-14).
- **Primary side bar** (300 px, left), **panel** (260 px, under the editor area), **secondary side bar** (300 px,
  right): each resizable, and hidden by its header's button or VS Code's keys (Ctrl+B, Ctrl+J, Ctrl+Alt+B); the
  title bar's toggles or the View menu. Hidden by a command, a part keeps its size for its return; dragged
  below its minimum it hides, and reopens at its last size before a command hid it, else at its minimum. Shown or
  hidden by a command, it slides open or closed in 200 ms (none when the system asks for less motion); a drag follows
  the pointer. Parts keep their pixel size when the window is resized; the editor area takes the rest. As in VS Code,
  the secondary side bar starts hidden. The primary side bar's header names its view, and the view may add to it after
  its name; a part without a view says its views come with the features. The panel's view is the protocol inspector,
  "Protocol" ([protocol.md](protocol.md#the-inspector)): its counts and tools share the header's one line. It shows
  only with **Advanced tools** on (below); off, the panel has no view until step 4's History, so the panel itself, its
  toggle in the title bar, View's Panel, Ctrl+J and its command in the palette are gone (Ctrl+J is still taken from
  the webview, whose own opens its downloads), and the panel starts hidden. On, the panel still starts closed and
  opens as a connected character's tab becomes active (a click on its row in the Characters view, or on its tab):
  closed by hand, it stays closed while tabs change, until a click on a character online in the Characters view; a
  character whose client is closed never opens it (the owner, 2026-11-17).
- **Editor area**: tabs and splits by `dockview-react`. An editor opens by `openEditor` (one tab per id; opening it
  again brings its tab forward). With no editor open it shows the app's mark in relief and the main shortcuts, these
  only when it is at least 480 px wide. At the right of a group's tabs, VS Code's **editor actions**: those of the
  editor its active tab holds (`EditorActions`, the owner, 2026-11-14): a blueprint's "Graph | Summary", the World's
  list of worlds.
- **Tabs** (`EditorTab`, the owner's choice from mock-ups, 2026-11-12): an icon, the title, a close button; the active
  tab rises from the strip with rounded top corners on the editor's ground and merges into it, without a blue line;
  the strip lies on the frame's ground so the active tab stands out, the others' text dimmer.
- **Status bar** (22 px, `src/shell/status-bar/`), the owner's layout "A" from mock-ups (2026-11-13), as VS Code's:
  the app's state on the left, its context on the right. Left, the app's name and version after its one accent, a
  small blue dot (the name alone until the back end answers, and in the browser preview), then the relay
  ([relay.md](relay.md#in-the-app)). Right, the game data:
  a library icon and "Client 3.7.13.17", or its yellow warning and "No game data"; then, once an update is
  downloaded, the offer to restart into it ([updates.md](updates.md#pieces)), the bar's one button. An item is an icon and a
  short label, grey; colour only marks a problem. An item with details takes the focus and opens a hover as VS Code's
  rich ones: sections of lines set apart by a rule, the first brighter, the others grey. A hover never repeats what
  the bar already says (the owner, 2026-11-13), not even as a title: the data's build has none; no data has its reason.
- **Command palette** (Ctrl+Shift+P, or the command centre): every command with its shortcut, itself included,
  filtered as one types, under the title bar, dimming nothing behind it; "Toggle advanced tools" ("Afficher ou masquer
  les outils avancés") has no shortcut.
- **Shortcuts** are written as the platform and the language do (`Ctrl+Maj+P` in French, `⌘⇧P` on macOS).

## Settings

The app's own settings, apart from a character's (the owner, 2026-11-16, for step 3's PR 24): no Settings view yet,
which comes when there are several; each setting is a command of the palette and an entry of the menus.
`settings.json`, at the root of the player's folder (`Documents/D3B`, [bot.md](bot.md#storage)), holds them all, with
its schema (1), written whole by `storage` as every file there; a setting missing from it takes its default, and a
file that does not read gives the defaults, is never written over, and a notification at start says why. A setting is saved the moment it
changes (`set_app_settings`, off the webview's thread, one save after another so the last change is the one kept); one
that cannot be saved holds for the session, and a notification says why. The front
end reads them once at start (`app_settings`, `src/settings/app-settings.ts`); the browser preview keeps the defaults.

- **Advanced tools** ("Outils avancés"), off by default: the Protocol view and its panel (above). Turned on, the panel
  slides open only if a connected character's tab is active (the owner, 2026-11-17); off, it closes, which is no close
  by hand. The inspector follows the frames even while off, so turning them on shows the
  session's frames (the owner, 2026-11-16).

## Look

The owner's "2 / 3 sober" (2026-11-12), chosen from mock-ups: VS Code's layout with its parts as discreet islands.

- **Islands**: each part (side bars, editor area, panel) is a card with 6 px corners and a faint 1 px edge (`#232323`),
  5 px from the next on the window's frame (`#111111`; 3 px at first, the owner asked for a little more room), the
  title, activity and status bars lying on the frame. The gaps turn blue under the pointer, as VS Code's sashes.
- **Colours**: VS Code's Dark Modern, the parts `#181818`, the editor `#1f1f1f`, text `#d6d6d6` and `#a8a8a8`; the
  blue accent `#0078d4` kept rare; a problem's text VS Code's error red `#f48771` (`--error`). Dark only: nothing switches to a light theme.
- **Shapes**: the activity bar's active entry is a rounded pill, not VS Code's blue line; a list's selection is a
  rounded grey row; tabs as above.
- **Inter** at 13 px (`@fontsource-variable/inter`, OFL-1.1, the owner's go).
- **Lucide's icons** for every icon (the owner, 2026-11-13, replacing the JetBrains icons chosen on 2026-11-12):
  `lucide-react`, ISC, already the shadcn components'; `src/icons/icons.ts` names those in use. Drawn grey (`#ced0d6`)
  with a 1.5 px stroke; colour only for a warning (yellow), an error (red) and the update offer (orange). The one
  exception: the window's buttons on Windows draw Windows 11's own glyphs, 10 px with a 1 px line (the owner,
  2026-11-13).
- **Licences**: the font's, Lucide's and those of every other package and crate the app bundles ship in its packages
  as `THIRD-PARTY-NOTICES.txt`, beside the app (the owner, 2026-11-13; [updates.md](updates.md#third-party-notices)).
  The interface does not show it yet.
- No part ever shows a native scrollbar while it resizes; a scrollbar, where content scrolls, is VS Code's: 10 px, no
  arrows, a translucent thumb.

## Next

Features one at a time, the owner choosing which and how each looks: the characters list, a character's details, the
world map, the blueprint editor, the history. The stores (`src/characters/`, `src/relay/`)
are followed again by the first feature that shows them.

## Tests

Vitest and Testing Library: the shortcuts (each part, a field focused, the palette, the key taken from the webview),
their labels per platform and language, a part's hide button, the title bar (the window's buttons on Windows only,
restore once maximised, no menus on macOS, each toggle pressed while its part shows, the command centre, File > Exit
through the close request, View's checked parts, no panel toggle nor View's Panel without Advanced tools, View's
Advanced tools turning them on and off and saved), the app's settings (off and the panel hidden at start, read at
start leaving the panel closed, on leaving it closed and off closing it, the browser preview asking nothing, a save failed holding for the session and saying why,
no folder, saves sent one after another, a file kept said at start, a read ending after a change dropped, a read
failed), `settings.json` (no file, read back with its schema,
a missing setting's default, a newer or malformed file kept and never written over), the Windows window's config against the main one, the palette (its
commands, filtering, running one, the panel's left out without Advanced tools), the shortcuts (Ctrl+J doing nothing
without Advanced tools but still taken from the webview), the Protocol panel's opening (closed at start, opened by
a connected character's tab and not without Advanced tools, nor by a closed client's, another editor or none, a close
by hand holding while tabs change, a click on a character online reopening it and an offline one not, Advanced tools
turned on opening it only with such a tab, turning them off forgetting a close by hand, a client connecting under its
tab), the activity bar (its pressed entry, hiding and showing the side bar), an
editor's tab (its title followed, its close), the status bar (the name alone before the back end answers, the
version and the data's build without a hover, no data and its reason in the hover, the relay after the version), the
relay's item (nothing before its status, grey arrows, red once off, its hover from the keyboard) and its hover's lines, the
update's offer (nothing until downloaded, the restart once chosen, its notes and the warning in its hover, none when
it has nothing to add, no second choice while installing) and the app's info (asked once, not in the browser
preview, asked again after a failure), and a part's collapse and slide against a fake panel (starting hidden,
first opening at its default size, hide and show, the 200 ms slide, two quick toggles, a drag below the minimum). The
layout itself is checked in the browser preview, not in happy-dom, which has no layout.
