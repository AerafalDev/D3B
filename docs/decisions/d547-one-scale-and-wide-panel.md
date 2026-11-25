# The studio on one scale, its bottom panel to the right edge (D547)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the status bar of
[D546](d546-status-icons-and-held-docks.md).

## D547: The studio's radii, small text and colours follow one scale, and the bottom panel spans to the window's right edge

*2026-11-25, the owner's asks in the running app: « faut uniformiser l'UI partout pour qu'elle ait le même style propre et
cohérent », the scale left to the session (« oui, lance-la »), and of the bottom panel « faudrait que cet onglet prenne toute la
taille à droite aussi ». Refines D159, D335, D513 and D530.*

- **What the audit found** (every `.axaml` of `Views/` and `Themes/`): radii from 2 to 14 pixels (7, 9, 10, 11, 12, 13 and 14
  beside 4, 5, 6 and 8), the small text at 11 and at 11.5 in about 35 places, about a hundred colours written in the views, some
  a tone of the palette's (an amber `#E9AD3C` beside `WarningColor`, a blue `#3B82F6` beside the accent), and the same control
  styled several times (four section titles, six fields, eight pills, five kind squares).
- **One scale**, next's (`--radius` 6 pixels): radii 4 for rows and chips, 6 for fields, buttons and parts, 8 for menus and cards;
  a circle keeps half its size, a mark of 1 to 3 pixels its own (52 radii moved). Small text 11, the studio's text 12.5, the pages'
  16 and 22 (48 sizes moved; the palette keeps next's 13). Colours by the palette's keys: an exact copy takes its key, a tone of
  one its key's tone (`WarningColor20`, `ErrorColor10`, `PrimaryColor10`…), and the glass the pills over a map rest on is
  `GlassColor` and `GlassHoverColor` (43 colours moved). Written in `Themes/Styles.axaml` and the `studio-ui` skill.
- **The bottom panel spans the editor and the Details**, to the window's right edge, the side bar keeping its height: the
  docks' grid holds the bottom panel's row (`MainWindow`), its edge dragged within the grid's height.
- **Left for later**, each a change of its own: the same control styled several times made one style (the section titles, the
  fields, the pills, the kind squares, the links), the colours of the map and the Sequencer drawn in C#, and the colours with no
  key in the palette (the kinds' and the routes' own).
- **Seen** in the headless drawings; the owner checks it in the running app.
- **Rejected.**
  - The scale the skill wrote before, 6, 7, 10 and 12: next's is 6 pixels, with smaller and larger by little; four radii so close
    read as noise.
  - A bottom panel under the side bar too: VS Code's panel aligned "right" leaves the side bar its height, the owner asked for the right.
