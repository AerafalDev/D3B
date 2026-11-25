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
