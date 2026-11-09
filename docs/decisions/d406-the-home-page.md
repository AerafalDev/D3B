# The home page (D406)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D406: The studio opens on a home page; the world appears when a route or a zone is selected

*2026-11-09, outside the roadmap's steps, a fix to D397 the owner asked for after seeing the app: "faut faire une page d'accueil propre à l'ouverture de l'app, là ça affiche le monde, c'est pas normal, le monde doit s'afficher quand on select une zone ou un trajet". Builds D320, D397.*

- **Found.** D397 made the world's viewport the first tab, opened with the studio and pinned, and removed the welcome from the start.
  The map therefore showed at every opening, before anything was selected, and with no character, route or zone it showed an
  empty world. The welcome that already existed (`WelcomeView`: the logo, four shortcuts, and where the host stands while it does
  not run) was kept as a command only.
- **The owner chose the full home page** among a full one, a light one and the old welcome pinned.
- **What it holds** (`HomeViewModel`, `HomeView`, the first tab, key `home`, pinned: no ×, `CloseAll` keeps it): the logo, the
  version and a pill of where the host stands, with the banners of a host starting, of a build the installed app does not ship
  and of a host that failed, as the welcome had them; quick actions (new profile, new route, new zone, the project's folder,
  the settings; the first four disabled until the host runs); the characters, online first, each with its portrait, a dot of its
  state and what it does or when it last played; the six assets of the project changed last, newest first, each with its kind and
  its age; the four shortcuts as keycaps. A click on a character chooses it, as the Outliner does, without leaving the page;
  a double click opens its tab. A click on an asset opens it where it is edited. A route or a zone made from the quick actions
  is made and shown on the map, like any other.
- **Recent means last changed**: the file's time, read again when the project's version moves (a save, a rename, a reload), not at
  every refresh. Nothing is recorded in `app.json`: a "last opened" list would be a new file to keep and to migrate for what
  the file system already says. If the owner wants "opened" rather than "changed", it is a list in `app.json` and a small change.
- **The world is a tab like the others, opened by a selection.** `ShowWorld` is called when a route, a step or a zone is
  selected in the Outliner, when one is opened from the assets, the search or the home page, and when a new one is made; the tab
  is not pinned any more and its × closes it, the selection and the editors staying where they were. Selecting a **character** in
  the Outliner no longer shows the world: the owner named routes and zones, and the character's Details and run block follow
  its selection wherever the page is. Its disc is on the map when the map is next shown.
- **Names.** `WelcomeView` became `HomeView`, `ShowWelcome` `ShowHome`, the tab's key `welcome` `home`, the strings `Welcome*`
  `Home*` ("Home" and "Accueil" for the tab).
- **Not measured**: whether the page is what the owner means by proper, which only their eye settles in the app; the home page
  with hundreds of characters (the lists are not virtualised, as the Outliner's are not).
- **Rejected.**
  - Opening on the last tab the owner left: the studio would open on the world again as soon as the map was the last thing
    used, which is the report.
  - A "recent" list kept by the app: see above.
  - Keeping the world pinned beside the home page: a tab that stays empty until something is selected.
