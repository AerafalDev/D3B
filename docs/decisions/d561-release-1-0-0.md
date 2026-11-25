# The app's 1.0.0 (D561)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the app's
releases of [D173](d126-d175-step-6.md#d173-the-app-updates-itself-from-the-repositorys-releases-when-its-user-chooses) and the
client build's release of [D428](d427-d428-client-3-7-14-18.md#d428-371418-mapped-from-371317s-pins-without-its-own-captures-and-released).

## D561: The app's release after 0.9.9 is 1.0.0, its pack of format 15 published first

*2026-11-25, a release session outside the roadmap's steps, the owner's « publish la release », then « 1.0.0 » among the two
numbers the lead put. Builds on D173, D428, D554.*

- **Found**: `main` read the pack of format 15 (the dungeons of D520, the Spanish of D554) while the release
  `client-3.7.14.18` held formats 12 and 13 only, so a tag would have failed to embed its data. The local pack had been
  extracted from an uncommitted tree; extracted again from `main` at #443, its client fingerprint is the same and its bytes
  differ by 9.
- **Published**: `data-15.pack` and its `.sha256` on `client-3.7.14.18` (`release.cs --publish`), fetched back by `embed.cs`
  into an empty folder as the release workflow does. The mapping 25 and the schema 1 were there already.
- **Fixed first**: `SequencerViewTests` pressed on a bottom panel still sliding open since D545 and failed in most of `main`'s
  CI runs, which would have failed the release workflow's tests; it now finishes the slide (#444).
- **Tagged**: `v1.0.0` on `5ade68a5` (#444), once `main`'s CI was green.
- **Rejected**: 0.9.10, MinVer's next and the lead's first proposal: the owner chose 1.0 for the largest update so far (the
  studio redesigned, the dungeons, three languages). The lead's reservation, given before the choice: five of its features
  are not yet seen in game (the dungeons, the mounts' capture, the transfer to the guild's chest, the run's restarts, the
  harvest's rotation), which the release's notes say.

## D562: The app's 1.0.1 shows the profile's menu again, which opened empty in 1.0.0

*2026-11-25, a release session outside the roadmap's steps, the owner's « On vois plus le contenu dans le select des profils », « faut
vite corriger et re publish release », then, checked in the running app, « Oui, publie 1.0.1 ». Refines D548; builds on D561.*

- **Found**: the profile's field opened a frame 240 wide and 10 high, the `MenuPopup`'s width and padding, with no row. Avalonia
  12's `MenuFlyout` makes its presenter over its `Items` the first time it opens; made over an empty `Items`, it never shows the
  items `Opening` adds to them, which is how D548's `ProfileMenu.Attach` filled it. Measured in a headless window: filled before,
  two rows of 28; filled as it opens from empty, none, on every opening.
- **Made**: the menu owns an `ObservableCollection` given as its `ItemsSource` from the start, cleared and filled as it opens.
  `ProfileMenuTests` opens the menu under a field and reads its rows, red before the fix. Seen in the running app by the owner.
- **Released**: 1.0.1 (a fix after 1.0.0). The pack is still of format 15, already on `client-3.7.14.18`, so nothing else is published.
- **Rejected**: keeping `Items` with a first item put at `Attach`, so that the presenter is made over a list not empty, which the
  probe showed works too, since it leans on the same undocumented behavior; filling them from the field's `Click`, which Avalonia raises after it opened the flyout.

## D563: A transfer run takes up what the character's last run left carried on the same connection, and a storage closed under its takes is opened again

*2026-11-25, a session of its own started by the lead session for the owner's first run of the transfer in game: « le bot quand j'ai
modif des trucs moi même a bugué, et après quand je l'ai relancé, au lieu de boucler pour mettre tout ce que j'ai en coffre de guilde,
il faisait que un transfert ». Refines D550 and D551; builds on D518.*

- **Found** on the owner's two game connections of 15:08 and 15:09 UTC, read with `scripts/capture.cs --messages` through mapping
  25, and the bot's journal of that day. The guild's chest to the bank, each round 2 stacks, the first taken in part, as the pods
  allowed. Two runs ended stopped from the app at the banker's question, the second carrying its round's take; the run started
  6 s later read that take as the character's own, which left no pods, and failed at once, « carries too much »; so did the run after
  the reconnection. Another run, paused and resumed, opened the chest 31 ms after its leaving; the client's own leaving, which follows
  every leaving of the bot by 3 to 135 ms (D81), closed it before any take, and the run ended done, having moved nothing.
- **Made**: `TransferBags`, one for the host, keeps for each character the stacks' own shares and the items its transfer carries or
  gives back, with the game connection their ids are of. A run on the same connection takes them up: it puts in what the last run
  carried before taking anything, every stack of an item not carried counted the character's own, as the player may have moved stacks
  by hand between the runs. On another connection what was carried stays in the bags with D550's warning. A dry run neither puts it in
  nor notes the bags. A storage found closed before or during the takes is opened again, three openings in all, as the recovery ladder
  tries (D518), then the run fails saying it could not be opened, rather than end done.
- **Rejected.**
  - Taking up what was carried after a reconnection by item rather than by stack: an object of its own effects, a player's own ring
    beside the chest's, would be told by its item alone (D550's review).
  - Moving what the bags hold of the items the settings choose, carried or not: it would move the player's own objects (D550).
  - Keeping the carry in D3B's database: a host's restart closes the game connections, whose new ids it could not use.
  - Waiting for the client's own leaving after each of the bot's: the leaving the bot hears is the server's, and a wait would slow
    every round to cover a cut that only a pause or a restart makes.

## D564: A transfer from the guild's chest to the bank may first put in the bank all the bags hold but what is worn

*2026-11-25, the same session as D563, the owner's « add l'option » after the lead offered it: the run of 15:41 UTC, after D563, failed
again for the pods, the bags still holding a take of 15:09 that two reconnections and the app's restart left in them. Refines D550.*

- **Found**: once the connection changed, no run can tell a take left in the bags from the character's own objects (D563), so a
  character whose pods that take fills can never take anything; the owner would have to empty the bags by hand.
- **Made**: the profile's « Vider d'abord le sac à la banque » (`EmptyBagsFirst`), off by default and shown only from the chest to
  the bank. The run's first step opens the bank and puts in, by one request, every stack the bags hold but what is worn, as the
  harvest's « All but what is worn » does (D459); what the transfer carried and the bank took counts as moved; then the rounds go
  on. A bank closed under the deposit is opened again, as for the takes (D563). A dry run deposits nothing.
- **Rejected.**
  - The option from the bank to the chest too: it would give the guild the player's own objects (D550).
  - On by default: D550's rule that what the character carried before stays in its bags holds unless the profile says otherwise.
  - Emptying the bags whenever a round finds no room: the character's load does not change during the run, so once is enough.
