# The studio's glass, tiles and drawn colours one each (D560)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the fields,
squares, badges and toolbar buttons of [D549](d549-fields-squares-badges-tools.md). D550 to D559 are the bank transfer's.

## D560: The glass over a map, a tile of figures, and the colours the drawing code paints with each have one source

*2026-11-25, the owner's « go la suite » after D549: what D549 left, under the owner's delegation for the studio's look. Refines
D547 to D549.*

- **The glass over a map** (`Themes/Styles.axaml`): `Border.Glass` and the pills, `Border.Pill` and `Button.Pill` (with
  `Preview`), shared, on `GlassColor` edged `GlassEdgeColor`; the character tab's two panels written inline and the world's HUD,
  which had colours of its own, take them. The pill's preview tint is `GlassAccentColor`.
- **A tile of figures**: `Border.Tile`, the card's grey, no edge, radius 8, padding 12: the character tab's `Tile` and two
  inline cards, the statistics' `Stat`.
- **One source for the drawn colours** (`StudioColors`, in `D3B.App.Controls`): the maps, the Sequencer, the Outliner and the
  kinds' icons painted the accent, the green of play, the amber and the red of their own (`#3B82F6`, `#4ADE80`, `#E9AD3C`,
  `#EA5D52`), next to the palette's (`#0078D4`, `#3FB950`, `#D29922`, `#F85149`); they now paint the palette's, and a route, a zone, a
  behavior and a profile have their colour once, which the views read as keys (`RouteColor`, `ZoneColor`, `BehaviorColor`, their
  tints). `StudioColorsTests` keeps the code's colours equal to the keys.
- **Kept**: the maps' own inks (their grounds, the hints' glyphs, the actions' dots for a harvest and an NPC), which no view shows.
- **Seen** in the headless drawings (the world with a route and its Sequencer, a character's tab); the owner checks it in the running app.
