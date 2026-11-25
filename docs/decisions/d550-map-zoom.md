# The studio's character map, zoomed (D550)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the map of
[D120 to D122](d110-d125-step-5.md) and the world's viewport of [D308](d293-d358-step-7.md).

## D550: A character's map zooms with the wheel around the pointer from its fit to four times it, a drag pans it once zoomed, and the world's zoom bar says and steps it

*2026-11-25, the studio after the shell's stages; the owner's « sur la view de la map du personnage faudrait qu'on puisse zoom
dezoom et faudrait mettre un zoom », passed on by the shell's session: **taken by this session**. Builds D120 to D122, D308, D325.*

- **What was asked.** A cell of the character's map, fitted to its tab (`MapLayout.Fit`), is some 50 pixels wide in a window
  1280 wide: a crowded cell, a monster group on a resource, cannot be read. The world's viewport zooms; the character's map did not.
- **What was made.** `MapZoom`, in `D3B.App.Controls`, is the zoom's arithmetic, as `WorldMapViewport` is the world's: a scale,
  a multiple of the fit, held between 1 and 4, and an offset. The wheel zooms by the world's notch (1.22) around the pointer,
  the cell under it staying under it; a drag, once zoomed, pans; the zoomed control always covers the view, so that a drag never
  loses the map and the fit has nothing to pan. The view model holds the scale (`MapViewModel.Zoom`), which the canvas binds
  both ways, and its steps: −, the zoom in % of the fit, + by the world's step (1.25), and the fit.
- **Drawn as the world's.** The bar is the world viewport's (−, the %, +, the fit's four corners), its `Glass` style moved
  from `WorldView` into `Themes/Styles.axaml` for both; it sits at the map's bottom right, where nothing else is. Zoomed,
  the map runs past the canvas's margin, kept clear of the pills at the fit, to the panel's edge, which clips it.
- **The shapes scale, the pan only moves them.** The cells' geometries are built for the zoomed layout, so that the grid's
  and the marks' lines keep their width; a pan draws them again moved, without building them again.
- **The zoom stays** when the character changes map or the search previews one: the bots change map every few seconds, and a
  zoom given back to the fit each time would be lost at once.
- **Rejected.**
  - The world viewport's 100 % as the client's full size: the character's map has no image, and its fit is the only size it
    means; 100 % is the fit.
  - Zooming out below the fit: the whole map already shows, with room around it for the exits' labels.
  - Keys (+, −, Home) as the world's: not asked, and the map takes no focus in its tab; the wheel and the bar cover the need.
  - Scaling the whole drawing with a transform: the grid's lines and the marks' edges would thicken with the zoom.
