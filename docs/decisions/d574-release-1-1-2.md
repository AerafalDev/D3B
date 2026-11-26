# The app's 1.1.2 (D574)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The release before
it is [D570](d569-group-keys.md#d570-the-apps-release-after-110-is-111-holding-the-profiles-that-restart-and-the-masters-spare-keys-given).

## D574: The app's release after 1.1.1 is 1.1.2, holding the harvest that no longer seeks the maps no way leads to and the transfer's lasting refusals

*2026-11-26, the session of the farmer standing still, the owner's « vu que ce bug est bloquant je compte en publier une autre »,
then « Oui, la 1.1.2 » on the session's recommendation. Builds D570, D571, D572, D573.*

- **What it holds.** The harvest's tours keep the maps no way leads to out, instead of seeking them every second while the
  character stands still until attacked (D571); the transfer keeps an item the destination refuses for good out of the next
  runs (D572), and a run begun in a storage, a dialog or an exchange a stopped run left open leaves it first (D573). Two pull
  requests merged into `main`, both fixes.
- **Published first.** Nothing: neither the pack nor the mapping changes format, and `client-3.7.14.18` holds format 15 and 26.
- **Tagged.** `v1.1.2` on `main` once this entry is merged and its CI is green.
- **Not measured.** Neither fix has been seen in game yet: the farmer in sub-areas 809 and 519, a transfer started right after a
  stop, a mule's bound object.
- **Rejected.** 1.2.0: no new option or behavior, only fixes. Waiting for the fixes to be seen in game first: the owner's
  farmer is blocked now, and a fix that misbehaves is undone by the next patch.
