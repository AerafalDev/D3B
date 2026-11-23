//! The journey from map to map over the client's world graph.

use std::cmp::Reverse;
use std::collections::{BinaryHeap, HashMap};

use crate::data::{Transition, WorldGraph};

/// Where a character stands in the world graph: a map, and the zone of its cell (its `linkedZone`'s high four bits).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(crate) struct Place {
    pub map: i64,
    pub zone: i32,
}

/// One map change of a journey: the way taken, from a place to the next.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Hop {
    pub from: Place,
    /// Where the graph says the transition leads; a scroll with an action leads there, not to the map it asks for
    /// (verified on 3.7.13.17, study `map-change`: 924 of 924).
    pub to: Place,
    pub transition: Transition,
}

/// Why no journey was found, met in normal use: the kind reading it says so.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum WorldPathError {
    /// The character's map and zone have no vertex: a zone with no way out (186 maps of 3.7.12.12 hold one, `main`
    /// client.md), or a map of another build.
    #[error("map {} zone {} is not in the world graph", .0.map, .0.zone)]
    Unknown(Place),
    #[error("no journey from map {} zone {} reaches the maps asked", from.map, from.zone)]
    OutOfReach { from: Place },
}

/// The world graph, read once for its journeys: each vertex's ways out that a character may take.
#[derive(Debug, Clone)]
pub(crate) struct WorldPaths {
    places: Vec<Place>,
    index: HashMap<Place, usize>,
    /// For each vertex, the vertex each way leads to and how, in the graph's order.
    ways: Vec<Vec<(usize, Transition)>>,
}

/// What a map change costs: every one the same, the character walks across a map or the server moves it (inferred,
/// the C# version's `WorldPathfinder`, `main`).
const CHANGE: u32 = 1;

impl WorldPaths {
    /// The journeys of `graph`. A transition with a criterion is left out until criteria are answered (a later
    /// slice): the C# version followed one only when its criterion was met (`main` D71); D3B's first slice takes no
    /// risk on a door it may not open. An edge naming a vertex the graph lacks is left out too.
    pub(crate) fn new(graph: &WorldGraph) -> Self {
        let places: Vec<Place> = graph
            .vertices
            .iter()
            .map(|vertex| Place {
                map: vertex.map_id,
                zone: vertex.zone,
            })
            .collect();
        let index = places.iter().enumerate().map(|(at, place)| (*place, at)).collect();
        let by_uid: HashMap<u64, usize> = graph
            .vertices
            .iter()
            .enumerate()
            .map(|(at, vertex)| (vertex.uid, at))
            .collect();
        let mut ways = vec![Vec::new(); places.len()];
        for edge in &graph.edges {
            let (Some(&from), Some(&to)) = (by_uid.get(&edge.from), by_uid.get(&edge.to)) else {
                continue;
            };
            if let Some(transition) = edge.transitions.iter().find(|transition| free(transition)) {
                ways[from].push((to, transition.clone()));
            }
        }
        Self { places, index, ways }
    }

    /// The fewest map changes from `from` to any zone of the nearest of `maps`: none when it stands on one already.
    pub(crate) fn journey(&self, from: Place, maps: &[i64]) -> Result<Vec<Hop>, WorldPathError> {
        if maps.contains(&from.map) {
            return Ok(Vec::new());
        }
        let &start = self.index.get(&from).ok_or(WorldPathError::Unknown(from))?;
        let mut costs = vec![u32::MAX; self.places.len()];
        let mut previous: Vec<Option<(usize, usize)>> = vec![None; self.places.len()];
        let mut open = BinaryHeap::new();
        costs[start] = 0;
        open.push(Reverse((0, start)));
        while let Some(Reverse((spent, at))) = open.pop() {
            if spent > costs[at] {
                continue;
            }
            if maps.contains(&self.places[at].map) {
                return Ok(self.hops(&previous, at));
            }
            for (way, (to, _)) in self.ways[at].iter().enumerate() {
                let reached = spent + CHANGE;
                if reached < costs[*to] {
                    costs[*to] = reached;
                    previous[*to] = Some((at, way));
                    open.push(Reverse((reached, *to)));
                }
            }
        }
        Err(WorldPathError::OutOfReach { from })
    }

    fn hops(&self, previous: &[Option<(usize, usize)>], end: usize) -> Vec<Hop> {
        let mut hops = Vec::new();
        let mut at = end;
        while let Some((before, way)) = previous[at] {
            let (_, transition) = &self.ways[before][way];
            hops.push(Hop {
                from: self.places[before],
                to: self.places[at],
                transition: transition.clone(),
            });
            at = before;
        }
        hops.reverse();
        hops
    }
}

fn free(transition: &Transition) -> bool {
    transition
        .criterion
        .as_deref()
        .is_none_or(|criterion| criterion.trim().is_empty())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::data::{Edge, TransitionType, Vertex};

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Graph/GraphTablesTests.cs).
    const VERTICES: &str = include_str!("../../../../tools/extractor/tests/fixtures/world/world-graph-vertices.json");
    const EDGES: &str = include_str!("../../../../tools/extractor/tests/fixtures/world/world-graph-edges.json");

    fn graph(vertices: &str, edges: &str) -> WorldGraph {
        let vertices: Vec<Vertex> = serde_json::from_str(vertices).unwrap_or_else(|error| panic!("{error}"));
        let edges: Vec<Edge> = serde_json::from_str(edges).unwrap_or_else(|error| panic!("{error}"));
        WorldGraph { vertices, edges }
    }

    fn way(kind: &str, cell: i32, map: i64, criterion: &str) -> String {
        let criterion = if criterion.is_empty() {
            "null".to_owned()
        } else {
            format!("\"{criterion}\"")
        };
        let (skill, element) = if kind == "interactive" {
            ("184", "9")
        } else {
            ("null", "null")
        };
        format!(
            r#"{{"type":"{kind}","direction":255,"cellId":{cell},"skillId":{skill},"mapId":{map},"elementId":{element},"criterion":{criterion}}}"#
        )
    }

    /// Maps 1 to 6, zone 1, and map 1's zone 2, which no way leaves: 1 → 2 by a scroll (a door with a criterion
    /// first), 2 → 3 by a map action, 3 → 4 by a door, 1 → 5 by a scroll asking for map 7, 3 → 6 by a door with a
    /// criterion only, and an edge to a vertex the graph lacks.
    fn world() -> WorldPaths {
        let vertices = r#"[{"uid":0,"mapId":1,"zone":1},{"uid":1,"mapId":2,"zone":1},{"uid":2,"mapId":3,"zone":1},
            {"uid":3,"mapId":4,"zone":1},{"uid":4,"mapId":5,"zone":1},{"uid":5,"mapId":1,"zone":2},
            {"uid":6,"mapId":6,"zone":1}]"#;
        let edge = |from: u64, to: u64, ways: &[String]| {
            format!(r#"{{"from":{from},"to":{to},"transitions":[{}]}}"#, ways.join(","))
        };
        let edges = [
            edge(0, 1, &[way("interactive", 300, 2, "PZ=1"), way("scroll", 13, 2, "")]),
            edge(0, 4, &[way("scrollAction", 6, 7, "")]),
            edge(1, 2, &[way("mapAction", 300, 3, "")]),
            edge(2, 3, &[way("interactive", 301, 4, "")]),
            edge(2, 6, &[way("interactive", 302, 6, "PZ=1")]),
            edge(3, 42, &[way("scroll", 13, 9, "")]),
        ];
        WorldPaths::new(&graph(vertices, &format!("[{}]", edges.join(","))))
    }

    const HOME: Place = Place { map: 1, zone: 1 };

    fn kinds(hops: &[Hop]) -> Vec<(TransitionType, i64)> {
        hops.iter().map(|hop| (hop.transition.kind, hop.to.map)).collect()
    }

    #[test]
    fn goes_by_a_scroll_a_map_action_and_a_door() {
        let hops = world().journey(HOME, &[4]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            kinds(&hops),
            [
                (TransitionType::Scroll, 2),
                (TransitionType::MapAction, 3),
                (TransitionType::Interactive, 4)
            ]
        );
        assert_eq!(hops[0].from, HOME);
        assert_eq!(hops[1].transition.cell_id, 300);
        assert!(hops.windows(2).all(|pair| pair[0].to == pair[1].from));
    }

    #[test]
    fn lands_where_a_scroll_with_an_action_leads() {
        let hops = world().journey(HOME, &[5, 4]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            kinds(&hops),
            [(TransitionType::ScrollAction, 5)],
            "the nearest of the maps asked"
        );
        assert_eq!(hops[0].transition.map_id, 7, "the map the client asks for");
    }

    #[test]
    fn skips_a_transition_with_a_criterion() {
        let hops = world().journey(HOME, &[2]).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(kinds(&hops), [(TransitionType::Scroll, 2)]);
        assert_eq!(
            world().journey(HOME, &[6]),
            Err(WorldPathError::OutOfReach { from: HOME }),
            "its only way has a criterion"
        );
    }

    #[test]
    fn has_no_map_change_on_a_map_asked() {
        assert_eq!(world().journey(HOME, &[3, 1]), Ok(Vec::new()));
    }

    #[test]
    fn types_a_map_out_of_reach_and_a_place_the_graph_lacks() {
        let closed = Place { map: 1, zone: 2 };
        assert_eq!(
            world().journey(closed, &[2]),
            Err(WorldPathError::OutOfReach { from: closed })
        );
        assert_eq!(
            world().journey(Place { map: 4, zone: 1 }, &[1]),
            Err(WorldPathError::OutOfReach {
                from: Place { map: 4, zone: 1 }
            })
        );
        assert_eq!(
            world().journey(HOME, &[9]),
            Err(WorldPathError::OutOfReach { from: HOME }),
            "an edge to no vertex"
        );
        let nowhere = Place { map: 99, zone: 1 };
        assert_eq!(world().journey(nowhere, &[1]), Err(WorldPathError::Unknown(nowhere)));
    }

    #[test]
    fn crosses_the_extractors_graph_by_its_scroll() {
        let paths = WorldPaths::new(&graph(VERTICES, EDGES));
        let from = Place {
            map: 54_164_787,
            zone: 1,
        };
        let hops = paths
            .journey(from, &[54_164_788])
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(kinds(&hops), [(TransitionType::Scroll, 54_164_788)]);
        assert_eq!((hops[0].transition.direction, hops[0].transition.cell_id), (6, 6));
        let back = Place {
            map: 54_164_788,
            zone: 1,
        };
        assert_eq!(
            paths.journey(back, &[54_164_787]),
            Err(WorldPathError::OutOfReach { from: back })
        );
    }
}
