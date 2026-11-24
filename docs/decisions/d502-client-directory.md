# The client's directory, chosen or found by the launcher (D502)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The client's
directory in the configuration [D31](d024-d033-step-2.md#d31-the-host-gets-the-pack-of-the-installed-build-before-it-starts);
the host's settings kept by the app [D151](d126-d175-step-6.md#d151-the-clients-display-time-and-every-delay-of-d3b-in-a-settings-page).

## D502: The client is found in the folder the player chose, else where the launcher installed it, else its default folder

*2026-11-24, a player's suggestion relayed by the owner (some players install their games on another drive): the lead
recommended both a setting and the launcher's location, the owner chose both. Builds D31, D151.*

- **What was found.** The host and the tools looked for the client in `%LOCALAPPDATA%\Ankama\Dofus-dofus3` unless
  `Client:Directory` named another, which only the command line or an environment variable could: a client installed
  elsewhere left the host without its build's pack, and its configuration never redirected (D35).
- **What was measured.** On the owner's machine, on 3.7.14.18, the launcher records each installed game in
  `%APPDATA%\zaap\repositories\production\dofus\<release>\release.json`; the one of `dofus3` names the installation
  directory in its `location` string, beside its version and whether it is installing, updating or repairing
  (client.md, "Installation"). The owner's client is on the default path: a location on another drive is not measured.
- **What was made.** `ClientLocator` (`D3B.Data`) gives the directory in one order, for the host and every tool of
  `scripts/`: the one `Client:Directory` names, else the launcher's `location` when it holds a client (its `version` file),
  else the default. The launcher's file is read as it is, its format not ours: missing, unreadable or without a location
  it is passed over. The app's settings gain « Dossier du jeu » in Files: the folder shown is the one the host will use,
  the host's command line or environment shown when they name one, since they win over the app's settings; a folder chosen is refused when it holds no client, kept as `Client:Directory` among the host's settings, and applies
  from the next start, its arrow giving the launcher's back.
- **Rejected.** A setting alone: every player on another drive would have to find it before the host could start. The
  launcher alone: a player who moved the game by hand, or a launcher whose file changes, would have no way out but the
  command line. Searching the drives for `Dofus.exe`: slow, and it could pick a stale copy. Windows' uninstall entries: on the
  owner's machine the launcher has one, Dofus 3 none.
