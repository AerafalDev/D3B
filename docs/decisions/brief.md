# Brief

Set by the project owner, not up for decision:

- An open-source bot for Dofus 3 (Unity), in .NET 10.
- Better than the existing bots, open source or sold: inspired by them, never copied.
- Every feature is a module, assembled as in Bevy, in the repository (D116, the owner's change: the brief's external
  .NET plugins, then the Lua scripts of D102 that replaced them, go).
- Mods are made in blueprint style, graphs of nodes edited in the app, after the fights of step 9 (D117, D169, the
  owner's changes).
- The desktop UI is Avalonia with ShadUI.
- The network goes through Kestrel's socket layer.
- Game data is extracted from the official client by file-based apps in `scripts/`, as in Dofus Remastered.
- Fights take their ideas from Bubble emu and BubbleBot, and do everything better.
- Every new large task starts in a new session, closed with a prompt for the next one.
