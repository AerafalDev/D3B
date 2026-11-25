# The studio's parts slide and its window is rounded, as next's (D545)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the Details
tidied by [D544](d544-details-tidied.md).

## D545: A part shown or hidden slides open or closed in 200 ms on next's curve, and the window's corners are rounded on Windows 11

*2026-11-25, the owner's asks while checking the shell in the running app: « pour les panneaux quand ils s'ouvrent et se ferment
faut une animation comme la branche next », « l'app faudrait qu'elle soit rounded comme la branche next ». Refines D320, D347 and
D531.*

- **What `next` does** (`src/shell/parts/resizable-part.tsx`, `src/index.css`, `src-tauri/tauri.windows.conf.json`, read-only): a
  part shown or hidden by a command slides, `flex-grow` over 200 ms on cubic-bezier(0.2, 0, 0, 1), and follows the pointer
  without a slide while it is dragged; on Windows its window has no frame but keeps the shadow, which has Windows 11 round its corners.
- **What was made.** The side bar, the bottom panel and the Details slide from their size to nothing, or back, on next's curve
  (`DockSlide`, the window's `Place`), their content shown until closed and clipped while it slides; a drag of an edge sets the
  size at once; a second toggle during a slide takes it over. The window asks Windows 11's desktop window manager for round corners
  as it opens (`WindowCorners`, `DWMWA_WINDOW_CORNER_PREFERENCE`); Windows 10 keeps its square ones.
- **Tests**: the curve's values (`DockSlideTests`); the views' tests that measure a part let the slides finish
  (`Slides.Finish`, the headless frames ticked).
- **Rejected.**
  - A transition on the grid's columns in XAML: Avalonia does not animate a `GridLength`, and a drag would slide too.
  - Removing the window's frame as next's Tauri does: ShadUI's window already draws its own, and the frame is what Windows rounds.
