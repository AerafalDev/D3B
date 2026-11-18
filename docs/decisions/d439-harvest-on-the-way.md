# Bug reports, the harvest on the way (D439)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md).

## D439: The harvest's journey stops on any map it crosses where the server offers a resource of the profile, of the zone or not

*2026-11-18, outside the roadmap's steps, a player's report relayed by the owner; the behavior chosen by the owner, as the lead
recommended it. Supersedes D392's rejection of "a stop on a map of another zone"; builds D128 and D392.*

- **The report.** "When an automatic harvest (alchemist and lumberjack, say) goes to a zone, it harvests nothing until it gets
  there, and misses many resources."
- **Found by reading the code, nothing measured in game.** D392's stop asked for a map of the zone being toured
  (`zone.Maps.ContainsKey` in the journey's predicate), and the step harvested only there. Every journey off the zone, back from
  the bank or a fight, from the start, or from one sub-area of the rotation (D391) to the next, went past the profile's
  resources.
- **Made.** The predicate of `HarvestActivity.GoOnAsync` no longer asks for the zone: the journey ends on the first map it crosses
  where the server offers an element of the profile's resources (`HarvestZone.Targets`, its professions or its list, at the
  character's levels, ordered as on a map of the zone). There the step harvests as anywhere, then marks the map
  `HarvestTour.Cross`: a set apart from the maps gone through, so the tour's progress counts the zone's maps alone, emptied with
  them at the next tour. A harvest on the way does not count as the zone's (`HasHarvested`), so a zone that gave nothing still
  waits to grow back (D128); it counts in the balance of the professions (D391), as it is the profession's.
- **No loop**, as D392: the predicate is never asked of the map the journey starts from, nor of a map gone through or crossed in
  this tour, so an element refused out of the zone is left and cannot call the character back. The haven bag is never harvested
  in. The journey to the bank keeps no stop (`Banking` passes none): full, the character has no room.
- **Rejected.**
  - A profile setting, "Harvest on the way": one more box for a behavior nobody turns off, when the C# version's profiles are
    already too many settings (`next`'s lessons); the resources wanted out of the zone are the same as in it.
  - Only the zone's own resources out of it: they are the profile's already (`HarvestZone.Targets` is not cut to the zone), so
    there was nothing narrower to keep, and the player asked for the opposite.
