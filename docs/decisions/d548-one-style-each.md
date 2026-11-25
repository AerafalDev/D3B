# The studio's controls one style each, the profile's field a select (D548)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the scale of
[D547](d547-one-scale-and-wide-panel.md).

## D548: The profile's field is a select of its own, a field's hover is opaque, and the titles and links share one style each

*2026-11-25, the owner's asks in the running app: the profile's field « un toggle qui affiche/désaffiche son contenu quand on
clique/reclique ou perd focus », « quand on est en hover y'a une frame où ça affiche du blanc », then « corrige ça et fais la
suite », the suite being the controls D547 left styled several times. Supersedes D544's `MenuReopenGuard`; refines D547.*

- **The profile's field is a select**: the field's own flyout (`Button.Flyout`, `ProfileMenu.Attach`), which a click opens, the
  next closes, and a click elsewhere or the focus leaving closes, its items made as it opens; the guard of D544, which the running
  app showed did not hold, goes.
- **A field's hover is opaque** (`FieldHoverColor`, `#3A3A3A`): from a field's opaque grey to `HoverColor`'s white veil, the brush's
  transition passed through a white for a frame. The profile's field, the tab's profile button and the About's buttons take it.
- **One style each, in `Themes/Styles.axaml`**: a section's title (`TextBlock.SectionTitle`, 12, semibold, bright: the character
  tab's Heading, the profile's SideTitle, the statistics' StatTitle and the Details' own, which keeps only its row's height); a
  group's title (`TextBlock.GroupTitle`, 11, semibold, dim: the home page's CardTitle, the problems' and the settings' GroupTitle,
  which was 12 and muted); a link (`Button.Link`, 11, the accent's text, underlined on hover: the character tab's and the settings').
- **Left for later**: the fields (six styles), the pills (eight), the kind squares (five), the toolbar buttons copied from `Tool`,
  and the colours of the map and the Sequencer drawn in C#.
- **Seen** in the headless drawings; the owner checks it in the running app.
