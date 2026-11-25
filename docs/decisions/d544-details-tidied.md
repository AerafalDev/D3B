# The studio's Details tidied, a character played from them, every character at once (D544)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). After the empty states of
[D543](d543-empty-states.md).

## D544: The Details play, pause and stop the character, the Characters view plays every one connected and lists only those played through D3B, and what is not in the app leaves the Details

*2026-11-25, the owner's asks while checking D540 to D543 in the running app: « l'icon male femelle est moche », the profile
field « plus arrondi » in a character's tab than in the Details, Planning, Combat and Variables « faut qu'on le vire c'est pas
encore sur l'app », « refaire un peu plus au propre l'onglet détails », « start/pause/arrêt depuis détails », « start all persos
connectés », and « que les persos qu'on a déjà joué ». Refines D324, D345, D397 and D514.*

- **The Details' header plays the character**: play, pause and stop beside its name, as its tab's (`CharacterPropertiesViewModel.Play`,
  `Pause`, `Stop`, `ShowsPlay`…), through the run block so that F5 and F6 follow and the Details stay as they are (D514); only
  while it is connected.
- **Play every character connected**: a button in the Characters view's header and a command of the palette
  (`RunBlockViewModel.PlayAll`), each character started or resumed as its own play would, those that cannot play left alone.
- **The Characters view lists the characters played through D3B** (`KnownCharacter.LastPlayed`, set when the relay sees a
  character chosen), not every character the server listed for an account. A connected character shows whatever it is.
- **What is not in the app leaves the Details**: Planning and Combat (7f and 7h) go; Variables show only once a behavior declared
  one for the character.
- **One look**: the breed without the gender sign, which Inter lacks and Windows drew as a coloured emoji (the portrait says it);
  the character's tab takes the Details' profile field, `Button.Pick` and `Border.Kind` now in `Themes/Styles.axaml`.
- **Seen** by the owner in the running app before the change; after it, in the tests only.
- **Rejected.**
  - A word for the sex (« femme ») after the breed: the portrait already draws the class's head by sex.
  - A stop for every character beside play all: not asked, and stopping them all is one click each in the palette.
