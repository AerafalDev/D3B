use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;

/// The client's graph of the ways between maps (`PathFindingData`, 3.7.13.17): a vertex per zone of a map.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WorldGraph {
    /// The vertices, in order of map and zone.
    pub vertices: Vec<Vertex>,
    /// The edges, in order of their vertices' uids.
    pub edges: Vec<Edge>,
}

/// A zone of a map: its walkable cells whose `linkedZone` has this zone in its high four bits.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Vertex {
    /// The client's identifier of the vertex, which edges name.
    pub uid: u64,
    pub map_id: i64,
    pub zone: i32,
}

/// The ways from one vertex to another.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Edge {
    pub from: u64,
    pub to: u64,
    pub transitions: Vec<Transition>,
}

/// How a transition is taken, measured on 3.7.12.12 and 3.7.13.17 (`main:docs/client.md`, "World graph").
#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum TransitionType {
    /// Walk off the edge of `direction` onto that neighbour.
    Scroll,
    /// The same, but the server may send the character elsewhere than `map_id`.
    ScrollAction,
    /// Step on `cell_id`.
    MapAction,
    /// Use the element `element_id` with the skill `skill_id`.
    Interactive,
}

/// One way along an edge.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Transition {
    #[serde(rename = "type")]
    pub kind: TransitionType,
    /// A scroll's direction, 0 to 7 clockwise from east, or 255 for none.
    pub direction: i32,
    /// The cell of the source map it is taken from.
    pub cell_id: i32,
    pub skill_id: Option<i32>,
    /// The destination, but for a scroll with an action: the neighbour the client asks for.
    pub map_id: i64,
    pub element_id: Option<i32>,
    /// A criterion in the client's condition syntax, such as `PZ=1`.
    pub criterion: Option<String>,
}

impl WorldGraph {
    /// Reads the graph's two tables that `manifest` lists in `folder`.
    pub fn load(folder: &Path, manifest: &Manifest) -> Result<Self, DataError> {
        Ok(Self {
            vertices: manifest.read(folder, "world-graph-vertices")?,
            edges: manifest.read(folder, "world-graph-edges")?,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Graph/GraphTablesTests.cs).
    const VERTICES: &str = include_str!("../../../tools/extractor/tests/fixtures/world/world-graph-vertices.json");
    const EDGES: &str = include_str!("../../../tools/extractor/tests/fixtures/world/world-graph-edges.json");

    #[test]
    fn reads_the_graph_the_extractor_writes() {
        let vertices: Vec<Vertex> = serde_json::from_str(VERTICES).unwrap_or_else(|error| panic!("{error}"));
        let edges: Vec<Edge> = serde_json::from_str(EDGES).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(vertices[0].map_id, 54_164_787);
        let door = &edges[0].transitions[1];
        assert_eq!(door.kind, TransitionType::Interactive);
        assert_eq!((door.skill_id, door.element_id), (Some(184), Some(465_453)));
        assert_eq!(door.criterion.as_deref(), Some("PZ=1"));
    }

    #[test]
    fn refuses_a_transition_type_it_does_not_know() {
        let text = EDGES.replace("\"scroll\"", "\"teleport\"");
        assert!(serde_json::from_str::<Vec<Edge>>(&text).is_err());
    }
}
