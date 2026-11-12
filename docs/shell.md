# The shell

The app's interface is rebuilt from scratch in VS Code's layout, one piece at a time, each decided with the owner and
shown to him before the next (the owner, 2026-11-12). The step-2 interface it replaces was removed (#185).

## Layout

VS Code's, part for part (`src/shell/`):

- **Title bar** (35 px): the app's icon and menus, the command centre, the layout toggles, and on Windows the window's
  buttons, drawn by the app since the window has no frame there. On macOS and Linux the system keeps its frame. It
  comes with the next PR; until then the parts start at the window's top.
- **Activity bar** (48 px, left): empty; each entry comes with its feature.
- **Primary side bar** (300 px, left), **panel** (260 px, under the editor area), **secondary side bar** (300 px,
  right): each resizable, and hidden by its header's button or VS Code's keys (Ctrl+B, Ctrl+J, Ctrl+Alt+B); the
  title bar's toggles and View menu come with it. Hidden by a command, a part keeps its size for its return; dragged
  below its minimum it hides, and reopens at its last size before a command hid it, else at its minimum. Shown or
  hidden by a command, it slides open or closed in 200 ms (none when the system asks for less motion); a drag follows
  the pointer. Parts keep their pixel size when the window is resized; the editor area takes the rest. As in VS Code,
  the secondary side bar starts hidden. Before any view, each part says its views come with the features.
- **Editor area**: tabs and splits by `dockview-react`. With no editor open it shows the app's mark in relief and the
  main shortcuts, these only when it is at least 480 px wide.
- **Status bar** (22 px): the app's name after a small blue dot; its items come with their features.
- **Command palette** (Ctrl+Shift+P, or the command centre): every command with its shortcut, filtered as one types,
  dimming nothing behind it. It comes with the title bar.
- **Shortcuts** are written as the platform and the language do (`Ctrl+Maj+B` in French, `⌘B` on macOS).

## Look

The owner's "2 / 3 sober" (2026-11-12), chosen from mock-ups: VS Code's layout with its parts as discreet islands.

- **Islands**: each part (side bars, editor area, panel) is a card with 6 px corners and a faint 1 px edge (`#232323`),
  3 px from the next on the window's frame (`#111111`), the title, activity and status bars lying on the frame. The
  gaps turn blue under the pointer, as VS Code's sashes.
- **Colours**: VS Code's Dark Modern, the parts `#181818`, the editor `#1f1f1f`, text `#d6d6d6` and `#a8a8a8`; the
  blue accent `#0078d4` kept rare. Dark only: nothing switches to a light theme.
- **Shapes, for the features to come**: the activity bar's active entry is a rounded pill, not VS Code's blue line; a
  list's selection is a rounded grey row; a tab is a rounded pill holding its icon, its title and its close button,
  the active one on a lighter ground (the owner's example: VS Code's "Release Notes" tab).
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

Vitest and Testing Library: the shortcuts (each part, a field focused, the key taken from the webview), the
shortcuts' labels per platform and language, a part's hide button, and a part's collapse and slide against a fake panel (starting hidden, following the layout, the 200 ms
slide, two quick toggles, a drag below the minimum). The layout itself is checked in the browser preview, not in
happy-dom, which has no layout.
