# The studio's shell after next, the command palette (D540)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the layout of
[D531](d531-next-layout.md) and the search of
[D328](d293-d358-step-7.md#d328-search-everywhere-ctrl-k).

## D540: The search is also next's command palette: Ctrl Shift P types its sign, the title bar's command centre names the app, the views and each character's play, pause, stop and forget are commands

*2026-11-25, the studio's shell after `next`, stage 1 of 4; the owner's « Oui, les 4 premières » to the lead's list and « je te
laisse faire les choix tout seul »: **taken by the session under that delegation**. Begun by the redesign session, which stopped
before committing; finished here. Refines D328, D513 and D531.*

- **What `next` has** (`src/shell/command-palette.tsx`, `commands.ts`, `title-bar/command-centre.tsx`,
  `use-shell-shortcuts.ts`, read-only): VS Code's palette on Ctrl Shift P, every shell command with its shortcut and Forget for
  each character whose client is closed, a dialog 600 pixels wide 4 pixels under the title bar, `#222222` edged `#454545`,
  radius 8, rows of 26 pixels with a radius of 4 in 13-pixel text, the row chosen `#04395e`; and a command centre in the title
  bar, 24 pixels, the search's glyph and the app's name centred on a white veil of 3 % edged at 9 %, lit to 7 % and 20 %.
- **One palette, not two.** The studio's search (D328) already had the commands, their shortcuts and the `>` sign. Ctrl Shift P
  now opens it with `>` typed, as VS Code does: a backspace takes the sign away and the same box searches everything again.
  Ctrl K and the command centre open it on everything. Both take `next`'s values above. The footer of hints goes: the field's
  watermark says the two signs (`>` for the commands, `@` for the characters). The scopes' chips stay smaller, since Tab moves through them.
- **The command centre** is `next`'s: the glyph and "D3B", 24 pixels, up to 600 wide, its tooltip naming Ctrl K.
- **The views are commands**: the characters, the library, the world, the home page, the problems, the trace, the statistics, the
  settings, the journal and the Sequencer; the protocol only with the advanced tools, as its tab (D513).
- **A character's actions are commands, by its name**, as its tab and its right click give them: « Jouer », « Mettre … en
  pause », « Arrêter », only as its state allows (`RunBlockViewModel.Read`), driving the run block as the tab does so that the
  Details stay as they were (D514); « Oublier le compte de … » for one offline, after the question (D344). Each shows the
  character's portrait. A character's own result still opens its tab.
- **The search speaks of the Library**: its scope « Assets » becomes « Bibliothèque », the word D513 gave the studio.
- **Seen** in the headless drawings (`CommandPaletteViewTests`, `D3B_DRAWINGS`), not in the running app: starting it would
  take over the client's configuration while the owner plays.
- **Rejected.**
  - A palette of its own beside the search, as `next` has (cmdk): two boxes that find the same commands, and the search's
    recent items, scopes and maps lost to the palette.
  - Opening Ctrl Shift P on the Commands scope instead of the sign: a backspace would not bring everything back, as VS Code's does.
  - A Views scope: the views are few, and VS Code lists them among the commands ("View: Show …").
  - Keeping the scrim under the palette: `next` dims nothing, and the title bar stays readable.
