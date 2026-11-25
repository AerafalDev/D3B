# The studio made simpler, the character's card and the home page (D514)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Stage 2 of the four
[D513](d513-studio-navigation.md#d513-the-studio-opens-on-an-activity-bar-and-one-side-bar-the-bottom-panel-and-the-details-hidden-until-needed-the-protocol-an-advanced-tool)
planned.

## D514: A character's tab chooses its profile and plays it, and the home page is the characters' cards

*2026-11-25, the redesign of the next release, stage 2 of 4; the owner's delegation of D513 ("je te laisse faire les choix tout
seul"): **taken by the lead under that delegation**. Refines D325, D338, D346, D397 and D406; builds D513.*

- **What was found.** One intention, three places (D395 said it of the cards): a character's profile was chosen from the right
  click or the Details, play from the title row's run block or F5, the start on connection from the right click or the Details;
  its tab, which shows the character best, had none of them. The home page listed the characters in a small card where a click
  chose and a double click opened, beside quick actions that the activity bar now holds (the settings, the project's folder).
- **A character's tab has a header** (as `next`'s character tab holds its blueprint and settings): its portrait, name and level,
  its profile in a field whose menu opens it or gives another (`ProfileMenu`, the Details' own menu shared), its start on
  connection, and play, pause and stop as its state allows (`CharacterTabViewModel.Character`, `Run`, `RunState`). The buttons
  drive the run block, which then chooses the character, so that F5, F6 and the title row follow what the tab did; their state
  is read as the block reads it (`RunBlockViewModel.Read`). A character chosen so is not a selection: the Details, which would
  say again what the header says, stay as they were (`RunBlockViewModel.Drive`, `IsDriving`; found by the review). The right click and the Details keep the same choices.
- **The home page is the characters' cards**: one card a character, its portrait with a dot of its state, its name and what it
  does and the profile it plays; a click opens its tab, a button plays or pauses it while it is connected (`HomeCharacterViewModel.PlayOrPause`, through
  the run block). With no character known, three steps say how to start. Under them, "Make" (a profile, a route, a zone), the
  assets changed last and the shortcuts (search, side bar, bottom panel, play).
- **Rejected.**
  - A play button on the Characters view's rows: the rows are the Outliner's, with an eye and a drag of their own; the card and
    the tab are the places that play.
  - The tab playing its character without the run block: two drivers of one bot, and F5 acting on another character than the
    one just played.
  - Keeping "Settings" and "Project folder" among the home page's actions: the activity bar's gear and File have them.
- **Not measured**: whether the owner finds it simpler; the app was seen through its headless drawings, not run (the owner's
  D3B was playing four clients).
