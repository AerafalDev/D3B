# The studio made simpler, next's icons (D532)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the style and
layout of [D530](d530-next-dark-islands.md) and [D531](d531-next-layout.md).

## D532: The studio draws Lucide's icons, as next does, under the keys its IntelliJ icons had

*2026-11-25, the redesign of the next release, stage 7; **the owner's**: "les icons sont pas les mêmes que sur next, faut les
mêmes", then the download of Lucide's files allowed. Supersedes D331's IntelliJ icons (D355 had removed Lucide's in 7c4).*

- **What `next` draws** (`src/icons/icons.ts` on the next line): Lucide's icons, lucide-react 1.72.0, grey with a stroke of 1.5
  on Lucide's grid of 24, colour only for a warning, an error and an update offered.
- **What was made.** `scripts/icons.cs` now takes Lucide's SVG files from lucide-icons/lucide at 1.72.0, the next line's
  version, checks the repository's ISC license before it writes anything, and writes `Themes/Icons.axaml`: each of the app's
  keys (`General.Search`, `Toolwindows.Web`…), kept from the IntelliJ icons so that no view changed, drawn by a Lucide icon
  scaled from 24 to 16, grey `#ced0d6` with next's stroke, the colours next gives (warning `#f2c55c`, error `#db5c5c`, update
  `#c77d55`) and green for play and success. next's own choices where it has one: the characters `user`, the library
  `library-big`, the world `map`, a profile (next's blueprint) `workflow`, the title bar's toggles `panel-left`,
  `panel-bottom` and `panel-right`. Lucide 1.x renamed some: `circle-exclamation-point`, `triangle-exclamation-point`,
  `octagon-exclamation-point`, `trash`. `tag` and `palette`, drawn with a filled dot the tool does not read, are `hash` and
  `paintbrush`.
- **Licenses**: Lucide's ISC was in `THIRD-PARTY-NOTICES.md` already, now naming the app's icons; the IntelliJ icons' notice
  stays for the mockup that still draws them.
- **Rejected.**
  - Renaming every key after Lucide's names: two hundred places in the views for the same drawing.
  - Drawing Lucide's icons from memory or from the history's 77 geometries: not the version next draws, and some missing.
