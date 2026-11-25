# The studio made simpler, the Library by kind (D515)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). Stage 3 of the four
[D513](d513-studio-navigation.md#d513-the-studio-opens-on-an-activity-bar-and-one-side-bar-the-bottom-panel-and-the-details-hidden-until-needed-the-protocol-an-advanced-tool)
planned; the assets' tree is [D351](d293-d358-step-7.md#d351-the-assets-tree-and-the-projects-folders) and
[D358](d293-d358-step-7.md#d358-the-assets-workspace-as-an-ides).

## D515: The Library groups the project by kind, its folders on demand, and the profile's header speaks of settings

*2026-11-25, the redesign of the next release, stage 3 of 4; the owner's delegation of D513 ("je te laisse faire les choix tout
seul"): **taken by the lead under that delegation**. Refines D326, D351, D358 and D418; builds D513 and D514.*

- **What was found.** The Library showed the project as an IDE's explorer, its folders first: a player looking for "my harvest
  profile" or "my routes" read a tree of folders they had not chosen to make, the files that do not read mixed into it. `next`'s
  Library lists blueprints in two groups, given and mine. The profile's header said "Variant of Harvest · 2 values changed",
  the model's words.
- **By kind, the default.** The project's assets under a heading a kind, in a player's order (Profiles, Routes, Zones,
  Behaviors, then the kinds to come), each folding and counting what it holds; then "Files that do not read"; then "Given with
  D3B" as before (`AssetTreeBuilder.AddKinds`, `AssetRowKind.Heading` and `AssetRowViewModel.Group`, the folds kept by group).
  The search, the kinds hidden, the selection and the menus keep working on the asset rows, which keep their folder; the
  keyboard stops on a heading without folding it, Left folds it or goes back to it, Right unfolds it. A row is not moved by a
  drag while the folders do not show, since nothing on screen would change (found by the review); a file dropped is imported.
- **The folders on demand.** A button of the Library's header shows the folders, as before, and the choice is remembered in
  `app.json` (`LibraryByFolder`); making a folder shows them.
- **The World's view makes a route or a zone** from two buttons of its header, as the home page's "Make" does.
- **The profile's header** says "Based on Harvest · 2 settings changed" (« Basé sur Harvest · 2 réglages modifiés »).
- **Rejected.**
  - Removing the folders: a player who sorted a big project in folders keeps them; they are a second view, not gone.
  - Two Library views at once (kinds and folders, as two side views): one view with a switch keeps the activity bar short.
- **Not measured**: whether the owner finds it simpler; seen through the headless drawings.
