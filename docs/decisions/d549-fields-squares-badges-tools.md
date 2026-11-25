# The studio's fields, kind squares, badges and toolbar buttons one style each (D549)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the titles and
links of [D548](d548-one-style-each.md).

## D549: A value's field, a search, a kind's square, a badge and a toolbar's button each have one style, shared by every view

*2026-11-25, the owner's « du coup ? » after D548: the suite D548 left, under the owner's delegation for the studio's look.
Refines D547 and D548.*

- **Fields** (`Themes/Styles.axaml`): a value's field, `TextBox.Field` (28 pixels, the field's grey, no edge: the Details' own,
  shared); a search, `TextBox.Search` (28, the input's grey and edge: the window's `PropertySearch`, which the side bar, the
  Library and the Details used, and the settings' `Find`, which was 30).
- **Kind squares**: `Border.Kind`, 18 pixels in a row, `Border.Kind.Large`, 22 in a card: the Library's and the home page's
  `KindSquare` and the statistics' `ItemSquare` (which keeps its raised ground as `Kind.Item`).
- **Badges**: `Border.Badge`, a short word on its tone's tint, radius 4, in three tones, `Good`, `Bad` and `Warn`: the trace's
  outcome `Pill` and the settings' `Badge`.
- **Toolbar buttons**: `Button.Tool` and `Tool Small`, with one state for a toggle that is on, `Tool.On`: the panels' `Ib`
  (26 pixels, now 24, its icons of 14 kept in the panels), the Details' `Open` and the settings' `Tiny` (22, now 24) and
  `Tiny.Wide` (26, now 28).
- **Left for later**: the pills over the maps and the facts' tiles, the colours of the map and the Sequencer drawn in C#, and
  the colours with no key in the palette (the kinds' and the routes' own).
- **Seen** in the headless drawings (the settings, the journal, the Library); the owner checks it in the running app.
