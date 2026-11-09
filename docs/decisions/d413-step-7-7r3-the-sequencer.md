# Step 7, 7r3, the Sequencer (D413)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D413: 7r3, the Sequencer: a route's steps as clips on tracks, set by gestures, keys and the Details

*2026-11-09, step 7, session 7r3, the lead's, within the owner's choices of D396 (the Sequencer, the words kept, the order of the
sessions). The owner chose, when asked, to build 7r3 now and as planned, and that the actions' cards of a step stay in the
Details for the step selected; they added, mid-session, that the accessibility and the shortcuts are to be strengthened too.
Builds D295, D299, D371, D375, D378, D395 to D397.*

- **A clip is derived, never stored** (`RouteClips`, `RouteClip`, in `D3B.Studio.Routes`). It is the longest run of consecutive steps
  that hold an equal action (`RouteAnchor.SameCall`), so that `RouteAnchor.Actions` and the route's file are as D378 made them. Three
  cases the mockup did not draw, each with its rule: two equal actions on one step are two clips, never one (a `Layer`); two
  different actions of one kind on a step (harvest ash, harvest oak) stand in lanes of their track; the order of a step's actions, which
  is the play order and which a track cannot show, is never changed by what the editor does not touch: a clip drawn appends its action to
  the end of each step, one taken off or stretched leaves the others where they were, and the order is set in the step's own card.
  Drawing over a step that already holds the action leaves it alone, so an action is never doubled. Two clips of equal actions that come
  to touch read as one: the file cannot tell them apart. A value changed on one step of a clip splits the clip around it.
- **The edits are minimal** (`Draw`, `Reshape` for stretching and moving, `Remove`, `Revalue`, `RevalueOn`, `CommonCalls`): a step the
  edit does not reach is the very same object, and a step's map, its pin and what the file holds that the version does not read are
  never touched. Tested on every kind of action the editor offers, on `route.follow` and on a node the host does not know, with a
  property: every action of a route belongs to exactly one clip, whatever the route.
- **Several steps at once** (`RouteStepSelection`, `WorldSelection.Steps`): Control adds a step or takes it off, Shift reaches out
  from the one chosen first, in the Outliner, on the map's nodes, on the Sequencer's ruler and by the keys; Delete takes every one
  off as one change; a change of the route's order leaves the step chosen last alone. A press with Control or Shift on a node does
  not carry it off. The map's frame (Shift-drag on empty ground) is not made.
- **The Sequencer** (`SequencerViewModel`, `SequencerGrid`, `SequencerView`): a tab of the bottom panel that exists while a route is
  selected and is shown when one is: the ruler of steps (number and coordinates), a row of their zones, a track for each kind of action
  in the order of the editor's menu (those a route holds, and those the user added from the palette, "Add a track", which lists the
  kinds the host knows with their help), a clip drawn in the kind's colour as the map's dots are, the steps selected tinted from top
  to bottom, the clip selected outlined, a clip that lacks a value outlined red. A press on an empty cell draws a clip with the node's
  own values over the steps the pointer crosses and selects it; a drag on an edge stretches it, on its body moves it, with Shift
  copies it; a gesture is one change to undo, the steps recomputed at each move from what they were before it began. The playhead
  is 7r4's; hovering a column does not yet light its step in the viewport.
- **The Details**: a clip selected shows its action's sentence and values, set once for every step it covers, with "Extend to the
  whole route" and "Delete"; several steps selected show the actions that all of them hold, set on them alone, once for all, or say
  that none is on all; one step selected keeps its action cards (one step's own values, and the order of its actions), as the owner
  chose, without the "+" and the dialog. A form is not made again under the hand typing in it.
- **The palette replaces the dialog** for a route: the step rows' "+", the Details' "+" and the editor's `OfferedActions` and picker are
  gone; `RouteActionPickerViewModel` stays, the sequences' editor uses it. The Sequencer's logic is in its own view model, so that the route editor, already over 900 lines, took only
  the selection and `ReplaceAnchors`.
- **Keys and names** (`SequencerViewModel.PressKey`): Left and Right choose the step before and after (Shift reaches out), Home and
  End the first and the last, Ctrl A every step; Up and Down choose the clips over the step chosen one after the other, Escape lets
  go; Alt with Left and Right moves the clip, with Shift its last edge, with Control its first; Ctrl D copies it right after itself;
  Delete takes it off; each is one change to undo. The grid takes the focus with a ring, carries a name and a help for a screen
  reader, a line under the toolbar says what is selected (`Status`, a polite live region), the tracks' names are named, and the
  command "Show the Sequencer" is in the search. The grid's clips are not yet children of an automation peer of their own.
- **Not measured**: how a drag feels on a long ruler and at the bottom panel's height (the Sequencer takes the panel's own, about 260
  pixels at 900, where the mockup gave it 292); whether the Sequencer is what the owner means by practical; a screen reader on the grid
  (the names are set, no reader was run); the cost of making the clips and their sentences at each change of a big route. All of it is
  tested on view models and in the headless window, not seen by the owner in the app.
- **Known limits.** Ctrl D puts the copy right after the clip, so that it touches it and reads as one longer clip (at the route's
  end it only extends it): a copy that lands apart is the Shift drag. Setting the actions several steps share edits the first of
  equal actions on each step, not a second one. Going from one route to another keeps the bottom panel's tab: the Sequencer is shown
  by the first route selected only.
- **Left**: recording in the toolbar (the measure in game, D395), the playhead and following a run (7r4), the map's frame, hovering a
  column lighting its step, a panel taller for the Sequencer, an automation peer with the clips as children, the cards' buttons in
  the Details styled as the studio's.
- **Rejected.**
  - Storing the clips in the route's file: the file and the run read the anchors' actions (D378).
  - Rebuilding the steps' actions from the clips at each edit: it would reorder what a track cannot show.
  - Dropping the actions' cards of a step from the Details: a value for one step only and the order of a step's actions have no place
    in a track; the owner chose to keep them.
  - A track for each of the eight kinds at all times: eight rows is a panel too tall; a route's own, and the palette for the rest.
