# The harvest's audit, the rotation's order and resources grown back on the way (D506, D507)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The rotation is
[D391](d390-d392-step-9.md); the journey's stop on a crossed map, [D392](d390-d392-step-9.md) and
[D439](d439-harvest-on-the-way.md); the stay for a resource grown back, [D495](d495-harvest-floors-and-regrowth.md).

## D506: The harvest's rotation goes to the nearest zone of the round not yet gone to, the zone just left last

*2026-11-24, an audit of the harvest the lead asked for after the owner's words "the harvest's inconsistencies, and correct
them"; the rule chosen by the owner, as the lead recommended it. Refines D391, which rejected the nearest ready zone alone.*

- **Measured** on the owner's four harvests of 2026-11-23 13:46 to 2026-11-24 02:59 UTC, 14 h each, automatic place,
  one profession, no zaap (captures `…49SF`, `SG`, `SI`, `SK`, client 3.7.14.18, mapping 25, pack 13; scratch probes
  printing ids, counts and times, never committed). The journeys between the blocks of harvests in one sub-area took the
  miner 478 minutes and 8,412 map loads, against about 270 minutes harvesting, the lumberjack 171, the alchemist 204,
  the farmer 67. The miner's ten sub-areas lie across the world (Kartonpath's tunnel, the Lac de Cania, Sidimote, Brâkmar,
  the Carrière d'Astrub, Istairameur, the Dopeuls), and it harvested 1,685 times against 4,591 to 7,377 for the others.
- **The cause**, read in the code: a tour done, the activity goes to the zone left longest ago (`HarvestRotation.Next`), the
  order of the draw the first round and the order they were left after, so each round goes through the zones in an order
  that owes nothing to where they lie. On the world graph, the miner's same 124 moves cost 5,480 map changes in that order
  and 3,593 going each time to the nearest zone not yet gone to in the round, 35 % fewer, about 1 h 40 of its 14 h at the
  3.4 s a map change took; the farmer's 1,096 and 488.
- **Made.** Of the zones ready (D128, D145), those not yet gone to since the rotation was drawn (`Done`) come first, the
  nearest by the map changes of the world graph from where the character stands to the zone's maps left to go through
  (`WorldTravel.RulesOf`, zaaps where the profile takes them), then the one left longest ago; a zone no way is known to comes
  after the others. The zone just left comes after every other, so that a round drawn again does not begin where the last
  ended. Once every zone was gone to, the rotation is drawn again as before (D166). Of the zones gone to in the round, the one
  left longest ago comes first, whatever the way, so that a zone left a moment ago still grows back.
- **Not measured in game.** The saving is reckoned on the world graph over the rounds the captures played, not played. The
  rule is tested on `HarvestRotation.Next`; the activity's use of it, the zone just left and the world graph's map changes, is
  read only, the fake game holding one sub-area. A zone dropped for no way there or nothing left to harvest is followed by
  the same choice, the zone just left counting for nothing.
- **Rejected.**
  - The nearest ready zone alone, D391's fear: two neighbours would take turns while the others wait; the round keeps each
    zone once a round.
  - A loop of the round computed once, the shortest through every zone: the zones ready change as resources grow back and
    tours sweep for nothing, and the nearest from where the character stands follows them; the probe measured that.
  - A pool weighed by the distance in the draw (`PlaceRotation`): a change of D391's draw, which the owner chose, for what the
    order alone recovers.

## D507: A journey stops on a map the tour went through already where a resource grew back, unless the tour left that element there

*2026-11-24, the same audit; chosen by the owner, as the lead recommended it. Refines D392 and D439, which kept every map gone
through or crossed out of the journey's stop.*

- **Measured** on the same captures, the bot's state replayed through `CharacterEvents`: of the wanted elements the server
  offered on a map the character stood on, at its level, reachable from its cell, out of a fight and with room, those it never
  asked to use were, for the farmer, about 1,800 element-visits in 14 h (it harvested 7,377), for the lumberjack about 240,
  the alchemist 180 and the miner 165, on maps harvested less than 20 minutes before or crossed again as the journeys went.
- **The cause**: the journey's stop asked only maps not gone through nor crossed in the tour (D392, D439), so that an element
  refused could not call the character back. A resource grown back on such a map was passed by; D495's stay takes only one
  grown back while the character stood there.
- **Made.** The journey stops on any map it crosses where the server offers a wanted element the character can reach, gone
  through or crossed in the tour or not, except an element the tour left on that map: refused, or out of reach, at a visit of
  this tour (`HarvestTour.LeaveElement`, forgotten with the tour). Out of the zone, a map crossed already is harvested again
  where the journey stopped for it, as D495's stay does.
- **Not measured in game.** Tested on the fake game: the tree of the middle map, harvested, grown back as the tour comes back
  through it, stops the journey; refused, it does not.
- **Rejected.**
  - Stopping for any element offered, refused or not: one the server refuses would stop every journey of the tour that
    crosses its map.
  - Stopping only for elements offered as grown back since the tour's visit: the bot does not keep what each map offered when
    it was left; what the tour left there says the same of the loop D392 guarded against.
