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
  hold only what works: File, Exit; View, the palette and the three parts. Measured by the owner on Windows 11
  (2026-11-12): our frame only, rounded corners, drag, double-click, the window's buttons.
- **Activity bar** (48 px, left): one entry per view of the primary side bar (`src/shell/side-views.ts`), a 20 px
  JetBrains icon; the view showing is a rounded pill, and choosing it again hides the side bar, as in VS Code. Today:
  Characters.
- **Primary side bar** (300 px, left), **panel** (260 px, under the editor area), **secondary side bar** (300 px,
  right): each resizable, and hidden by its header's button or VS Code's keys (Ctrl+B, Ctrl+J, Ctrl+Alt+B); the
  title bar's toggles or the View menu. Hidden by a command, a part keeps its size for its return; dragged
  below its minimum it hides, and reopens at its last size before a command hid it, else at its minimum. Shown or
  hidden by a command, it slides open or closed in 200 ms (none when the system asks for less motion); a drag follows
  the pointer. Parts keep their pixel size when the window is resized; the editor area takes the rest. As in VS Code,
  the secondary side bar starts hidden. The primary side bar's header names its view; a part without a view says its
  views come with the features.
- **Editor area**: tabs and splits by `dockview-react`. An editor opens by `openEditor` (one tab per id; opening it
  again brings its tab forward). With no editor open it shows the app's mark in relief and the main shortcuts, these
  only when it is at least 480 px wide.
- **Tabs** (`EditorTab`, the owner's choice from mock-ups, 2026-11-12): an icon, the title, a close button; the active
  tab rises from the strip with rounded top corners on the editor's ground and merges into it, without a blue line;
  the strip lies on the frame's ground so the active tab stands out, the others' text dimmer.
- **Status bar** (22 px, `src/shell/status-bar/`), the owner's layout "A" from mock-ups (2026-11-13), as VS Code's:
  the app's state on the left, its context on the right. Left, the app's name and version after its one accent, a
  small blue dot (the name alone until the back end answers, and in the browser preview), then the relay
  ([relay.md](relay.md#in-the-app)). Right, the game data:
  JetBrains' library icon and "Client 3.7.13.17", or its yellow warning and "No game data"; then, once an update is
  downloaded, the offer to restart into it ([updates.md](updates.md#pieces)), the bar's one button. An item is an icon and a
  short label, grey; colour only marks a problem. An item with details takes the focus and opens a hover as VS Code's
  rich ones: sections of lines set apart by a rule, the first brighter, the others grey. A hover never repeats what
  the bar already says (the owner, 2026-11-13), not even as a title: the data's build has none; no data has its reason.
- **Command palette** (Ctrl+Shift+P, or the command centre): every command with its shortcut, itself included,
  filtered as one types, under the title bar, dimming nothing behind it.
- **Shortcuts** are written as the platform and the language do (`Ctrl+Maj+P` in French, `⌘⇧P` on macOS).

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
- **JetBrains' icons**: IntelliJ Platform's new UI icons, dark variants copied unchanged into `src/icons/expui/`
  (Apache 2.0 and the project's NOTICE, in `src/icons/`); `src/icons/icons.ts` names those in use. They are not
  counted by `pr:size`. The app's packages must carry these notices and the font's licence before the next release.
- No part ever shows a native scrollbar while it resizes; a scrollbar, where content scrolls, is VS Code's: 10 px, no
  arrows, a translucent thumb.

## Next

Features one at a time, the owner choosing which and how each looks: the characters list, a character's details, the
world map, the blueprint editor, the history, the protocol inspector. The stores (`src/characters/`, `src/relay/`)
are followed again by the first feature that shows them.

## Tests

Vitest and Testing Library: the shortcuts (each part, a field focused, the palette, the key taken from the webview),
their labels per platform and language, a part's hide button, the title bar (the window's buttons on Windows only,
restore once maximised, no menus on macOS, each toggle pressed while its part shows, the command centre, File > Exit
through the close request, View's checked parts), the Windows window's config against the main one, the palette (its
commands, filtering, running one), the activity bar (its pressed entry, hiding and showing the side bar), an
editor's tab (its title followed, its close), the status bar (the name alone before the back end answers, the
version and the data's build without a hover, no data and its reason in the hover, the relay after the version), the
relay's item (nothing before its status, grey arrows, red once off, its hover from the keyboard) and its hover's lines, the
update's offer (nothing until downloaded, the restart once chosen, its notes and the warning in its hover, none when
it has nothing to add, no second choice while installing) and the app's info (asked once, not in the browser
preview, asked again after a failure), and a part's collapse and slide against a fake panel (starting hidden,
first opening at its default size, hide and show, the 200 ms slide, two quick toggles, a drag below the minimum). The
layout itself is checked in the browser preview, not in happy-dom, which has no layout.
