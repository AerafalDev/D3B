# Groups, the bank's rounds and the food (D459)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The group's bank is
in [D442](d442-step-10-the-groups-bank.md), [D452](d452-group-bank-master-empties.md), [D457](d457-groups-invite-and-bank-refusal.md)
and [D458](d458-group-exchange-pace.md).

## D459: A stack an exchange leaves is offered no more in that unloading, and a profile banking all but what is worn banks the group's food too

*2026-11-19, the owner's test in game of main a752a945 ("ça marche": every exchange concluded, D458 seen working), then two
asks: "y'a plein de fois où il échange avec une mule qu'il vient déjà d'échanger", and the profile's deposit of all but
what is worn to empty the healing consumables too, the mules' as the master's, the owner's choice between that option and
a new one; the lead's fix, local and reversible. Refines D441, D442, D452.*

- **Measured** on the master's capture of that run (`scripts/capture.cs --messages`, type URLs and times only): three
  mules emptied, 38, 22 and 86 stacks in 28, 20 and 54 s, each exchange concluding at once. After each, two more exchanges
  with the same mule, 8.1 s each, 49 s in all: the mule's capture shows its first exchange moving 39 stacks told 38
  changes, then each next one moving that one stack, told nothing, and concluding with nothing given.
- **Made**:
  - `GroupBank`: once an exchange concluded, a stack it was to take whose quantity did not go down is left for the rest
    of that unloading; one given in part, the master's pods full, is taken again once the master deposited, and an
    exchange that did not conclude still has the master deposit and take the rest (D452). The round tells the follower
    those stacks and whether the food goes (`GroupCollect`), so that both reckon the same gifts; a stack a bank or an
    exchange refused one day is tried again at the next unloading, as D457's.
  - `BotActivity.DepositsAll`, the fight's and the harvest's "All but what is worn": the master deposits its food too,
    and its followers give theirs; with "The loot", each keeps the food it eats, as D441 and D452 had it.
- **Not measured**: the owner looks at it in game: one exchange a mule, and the food banked with "All but what is worn".
- **Rejected.**
  - A new setting for the food: the owner chose the profile's deposit.
  - Leaving out the stacks an exchange refuses by their kind, such as bound to the account (D153): the server's own
    answer says it for every kind.
  - Each side keeping its own list, as first made: the review found the two reckoning different gifts, and a stack left
    by the follower for good.
  - Ending a follower's rounds at an exchange that gave nothing: what its pods could not hold before may follow.
