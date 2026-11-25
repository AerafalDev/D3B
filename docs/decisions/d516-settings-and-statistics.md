# The studio made simpler, the settings and the statistics (D516)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Stage 4, the last, of the
four [D513](d513-studio-navigation.md#d513-the-studio-opens-on-an-activity-bar-and-one-side-bar-the-bottom-panel-and-the-details-hidden-until-needed-the-protocol-an-advanced-tool)
planned.

## D516: The statistics are a tab of their own, and the settings read General, Bots, Files, Advanced and About

*2026-11-25, the redesign of the next release, stage 4 of 4; the owner's delegation of D513 ("je te laisse faire les choix tout
seul"): **taken by the lead under that delegation**. Refines D146, D327, D329 and D354; builds D513 to D515.*

- **What was found.** The statistics, what a player's bots bring, were the fifth tab of the bottom panel, beside the journal,
  the trace, the problems and the protocol, 260 pixels high under the editor. The settings opened on the bots' delays, then
  "Network" (the captures), then "Appearance" (the language): what every player sets first came third, and the captures, which
  a player rarely touches, second.
- **The statistics are a tab of the editor**, opened by the activity bar's Statistics (pressed while it shows) or the search,
  read while it shows (`MainViewModel.Statistics`, `ShowStatistics`, `IsStatisticsShown`); the bottom panel keeps the journal,
  the trace, the problems and, with the advanced tools, the protocol. The toolbars' styles the two share moved into
  `Themes/Panels.axaml`, which only they include, so that their short class names stay theirs.
- **The settings, in a player's order**: General (the language, the layout), Bots (the display time, the bank, the bots' and
  the relay's delays), Files, Advanced (the recorded sessions and the advanced tools), About. "Network" and "Appearance" are
  gone as words, the home page's notice of the recorded sessions and a report without a capture now send the player to the
  Advanced settings (found by the review); the relay's delays stay with the bots' since one "Reset all to default" gives both back.
- **Rejected.**
  - The statistics as a view of the side bar: their resources and sessions need the editor's width.
  - The relay's delays under Advanced: the restore of the delays covers both, and splitting it would leave a section with a
    change no button gives back.
- **Not measured**: whether the owner finds it simpler; seen through the headless drawings.
- **The four stages are done** (D513 to D516). What a later session could take: the character's tab's column (behavior, session,
  decisions) told in the player's words, the Details' sections for a character now that its tab holds the same choices, and the
  run in the app itself with the owner.
