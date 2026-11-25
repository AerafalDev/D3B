# The studio made simpler, next's dark islands (D530)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). A fifth stage after the
four of [D513](d513-studio-navigation.md#d513-the-studio-opens-on-an-activity-bar-and-one-side-bar-the-bottom-panel-and-the-details-hidden-until-needed-the-protocol-an-advanced-tool).

## D530: The studio takes next's dark style, VS Code's Dark Modern as discreet islands, and stays dark only

*2026-11-25, the redesign of the next release, stage 5; **the owner's**: asked whether the studio could "prendre le style de la
branche next", then "Oui, ajoute-le"; a light theme was proposed with it, and the owner refused it: "pas de theme white que le
theme dark comme la branch next". Refines D320, D335 and D347; builds D513 to D516. D518 to D529 are other sessions'.*

- **What was taken from `next`**, its values and its layout, not its CSS (`git show origin/next:src/index.css`, the owner's
  "2 / 3 sober" of 2026-11-12): the frame `#111111`, the parts `#181818`, the editor `#1f1f1f`, their edge `#232323`, the card
  `#212121`, the popover `#1f1f1f`, a hover of white at 8 % and a press at 12 %, the text `#d6d6d6`, muted `#a8a8a8`, faint
  `#6e7681`, an error's text `#f48771`, the blue `#0078d4` kept rare, the states' green `#3fb950`, amber `#d29922` and red
  `#f85149`, the inputs `#313131` edged `#3c3c3c`, the radius of 6 pixels.
- **What was made.** `Themes/Colors.axaml` holds those values under ShadUI's and the studio's keys; the parts are islands of 6
  pixels' corners (`Border.Dock`), ShadUI's radii 4, 5, 6 and 8. The colours the views wrote for a dark ground became keys of the
  palette (`ErrorTextColor`, `WarningTextColor`, `SuccessTextColor`, `SoftLineColor`, `VeilColor`, `ProfileTintColor`,
  `ProfileSquareColor`, `GridDotColor`, `ScrimColor`); the kinds' squares are their colour at a fifth (`AssetKinds.TintOf`); a
  character's header has a profile field of its own on the parts' surfaces. The map's ground and what floats over it are left as
  they were: they are the game's view.
- **Inter** was already the app's typeface, embedded by `Avalonia.Fonts.Inter`, its OFL in `THIRD-PARTY-NOTICES.md`.
- **Seen**: in the running app on the owner's project (the home page, the Library, the activity bar), closed by its window so
  that the client's configuration came back; every part through the headless drawings (`ThemeViewsTests`).
- **Rejected.**
  - A light theme, VS Code's Light Modern made into islands: made, then removed at the owner's word; `next` is dark only.
  - Copying `next`'s stylesheet: another stack; its values are ours to reuse, as D513 says.
