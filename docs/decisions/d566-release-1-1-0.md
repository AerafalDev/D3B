# The app's 1.1.0 (D566)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The release before
it is [D562](d561-release-1-0-0.md#d562-the-apps-101-shows-the-profiles-menu-again-which-opened-empty-in-100).

## D566: The app's release after 1.0.1 is 1.1.0, holding the bank and chest transfer fixed and the capture gear worn, its mapping of format 26 published first

*2026-11-25, the capture gear session, the owner's « push / pr / merge / publish / release » once both were done, then
« 1.1.0 » among 1.1.0 and 1.0.2. Builds D561, D562, D563, D564, D565.*

- **What it holds.** The transfer between the bank and the guild's chest fixed (D563, D564) and the hunt wearing capture
  nets and soul stones, with the archmonsters' souls captured (D565): a feature, so a minor version.
- **Published first.** `mapping-26.binpb` and its `.sha256` on `client-3.7.14.18` (`release.cs --publish`), which `main`
  reads from D565 on; the pack of format 15 and the schema 1 were there already.
- **Tagged.** `v1.1.0` on `main` once both pull requests are merged and its CI is green.
- **Rejected.** 1.0.2: the release brings a new option, not only fixes. Two releases, one for each pull request: the owner
  wants them in one.
