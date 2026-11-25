# The studio's shell after next, empty states that guide (D543)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the layout kept
by [D542](d542-layout-kept.md).

## D543: An empty view guides: the Characters view says how to connect a character through D3B, the World's and the Library's offer to make a route, a zone or a profile, the statistics every session

*2026-11-25, the studio's shell after `next`, stage 4 of 4; the owner's « Oui, les 4 premières » and « je te laisse faire les
choix tout seul »: **taken by the session under that delegation**. Refines D513, D515 and D516.*

- **What `next` says** (its locales, read-only): an empty view says how to fill it, the characters' « Lancez Dofus depuis le
  launcher d'Ankama : chaque personnage qu'il joue apparaît ici », a zone's « Aucune zone : « + » en peint une ».
- **What was made**, a block of its own (`StackPanel.EmptyState` in `Themes/Styles.axaml`: a title, what fills the view, its
  steps, its buttons, centred and 300 pixels wide):
  - **Characters**, none connected: « Aucun personnage connecté », that D3B relays the game, then three steps: keep D3B open until
    its status bar says « Relais prêt » (D541), start Dofus from Ankama's launcher, choose a character
    (`OutlinerViewModel.IsGuiding`, `EmptyTitle`, `EmptySteps`);
  - **World**, no route or zone: what each is, and « Nouvelle route » and « Nouvelle zone », which the World's header makes
    (`ShowsMakeButtons`, `MakeRouteCommand`, `MakeZoneCommand`);
  - **Library**, the project empty: what a profile is, and « Nouveau profil » (`AssetsViewModel.NewProfileCommand`);
  - **Statistics**, nothing in the period: what fills them, and « Voir toutes les sessions » while the period is not all
    (`StatisticsViewModel.ShowsAllRuns`);
  - **Journal**: « Les actions de chaque personnage s'affichent ici dès qu'il joue ».
- **A host that did not start says where to look**: its message points to « Relais arrêté » in the status bar instead of a
  welcome page that no longer opens at start (D531); stopped, the relay opens the problems, where the failure and its fix are,
  and the bar counts that failure among them (found in the running app: a pack of format 13 where `main` reads 14).
- **A search that finds nothing says so** (« Rien ne correspond à … ») and guides nowhere: the steps are for an empty view, not
  an empty search.
- **Rejected.**
  - A button that starts Dofus: D3B never starts the client (D2's spirit, the player keeps the launcher), and the relay works for
    any client started after it.
  - Illustrations: the studio's look is sober (D530); the words and one button do the work.
