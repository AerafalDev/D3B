# The home page's tab (D412)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D412: The home page closes like the other tabs, and the Window menu opens it again

*2026-11-09, outside the roadmap's steps, a report of the owner's from the app, the choice made by the owner. Supersedes D406's "pinned: no ×, `CloseAll` keeps it". Builds D397, D406.*

- **Found.** The owner saw the "Accueil" tab with no cross to close it and its label snug against the right edge, as if cut: the tab's face
  keeps 4 pixels on its right for the cross (`Button.TabFace`), which a pinned tab hides. D406 had pinned the home page.
- **Chosen by the owner**: the home page is closable. It is no longer pinned (`EditorTab.IsPinned` stays for a tab that must not
  close, and a pinned tab's face now takes the plain padding, `Button.TabFace.Plain`, so that its label is not snug). `CloseAll`
  closes it with the others, and a new entry of the Window menu, "Home" (`ShowHomeCommand`), opens it again, in the app's language.
  With every tab closed the editor shows nothing until a tab is opened.
- **Not measured**: how the closed editor looks to the owner; that is for their eye.
- **Rejected.** Keeping the home page pinned and only widening the padding: the owner asked for a cross. A home page that comes back
  by itself when the last tab closes: the owner chose a menu entry.
