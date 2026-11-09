# Architecture (D393)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D393: The studio's model leaves `D3B.Bot` for a project of its own, `D3B.Studio`

*2026-11-08, architecture, the lead's and the owner's wish to decide, before 7e to 7i add the canvas, nodes and debugging, how the studio's model is kept apart from the bots' run time. The owner chose among three splits. Refines D296 to D300; supersedes the "one project" of `D3B.Bot` for these folders.*

- **Measured.** `D3B.Bot` held 365 files. The studio's model is `Assets` (16 files), `Behaviors` (69) and `Behaviors/Nodes` (34), `Routes`
  (27): 146 files. It depends on the run time (`ActivityCatalog`, `BotActivity`, `BotContext`, `BotActor`, settings, journal,
  characters, fights, `IHostPreparation`) and, for routes, on the harvest and fight actions. The run time and the modules do not
  depend on it but for three links: `BotActor.Trace` (`TraceEntry`, `TraceKind`, `TraceOutcome`), `BotActivity.Outline`
  (`BehaviorOutline`, `OutlineState`), both records the run time fills for the studio to show, and `DefaultModules`, whose
  `BehaviorsModule` `StorageModule` already adds. A trial in a throwaway move, undone, built `D3B.Bot` with no error once those links
  were cut, and `D3B.Studio` with about 20 errors, all internal members of `D3B.Bot`. Inside the cluster `Assets` and `Behaviors`
  refer to each other, and so do `Behaviors` and `Routes`: one project holds them.
- **Decided.** A project `D3B.Studio`, referencing `D3B.Bot`, holds `Assets`, `Behaviors` (with `Nodes`) and `Routes`, in the
  namespaces `D3B.Studio.Assets`, `D3B.Studio.Behaviors`, `D3B.Studio.Behaviors.Nodes` and `D3B.Studio.Routes`. `D3B.Storage` references
  `D3B.Studio`, so that the references run `D3B.App` to `D3B.Storage` to `D3B.Studio` to `D3B.Bot`. The five records above go down to
  `D3B.Bot` (`Runtime` and `Activities`). `DefaultModules` no longer adds `BehaviorsModule`: the hosts that want behaviors add it, as
  `StorageModule` does. Harvesting and fighting stay modules of `D3B.Bot`, and declare no node: the route actions that play them
  are `D3B.Studio`'s. The studio's tests move to a project `D3B.Studio.Tests`, which compiles the helpers of `D3B.Bot.Tests` it needs as linked files, as
  the app's and the storage's tests do, `TestProfiles` and `TestBehaviors` now being its own.
- **The internals.** What `D3B.Studio` uses of `D3B.Bot` and was internal becomes public, with its documentation: `BotContext.Actor`,
  `BotActor.Time`, `Log` and `PauseAfterStep`, `SettingDefinition.TryRead`, the constructor and `Build` of `ActivitySettingsBuilder`,
  `MapHarvest` and `MapFight`. No `InternalsVisibleTo` is given to `D3B.Studio` in the source projects, so that the border stays the
  compiler's; the test projects get theirs, `D3B.Studio.Tests` among them. A narrower seam (a service of the actor for what the
  behaviors need) is left for a task of its own. A module that declares nodes must
  reference `D3B.Studio`, whose `AddNode` is the node API: a future module in its own project does, and a module of `D3B.Bot` would
  not be able to, which is the limit of this choice.
- **Rejected.**
  - One project and a test that forbids the references: almost free, but the border is not in the build, the catch-all stays and
    `D3B.Storage` keeps referencing it.
  - A wider split, the run time apart from the modules: premature, `Harvesting` and `Fighting` use `Activities`, `Movement`,
    `Runtime` and `Settings` closely, and no need has appeared.
  - Keeping the namespaces `D3B.Bot.*` in the new assembly: nothing to change in the 110 files that use them, but a namespace that
    lies about its assembly.
  - Leaving the studio's tests in `D3B.Bot.Tests`: nothing to move, but a test project would cover two projects.
