//! A node's pins: its kind's from the catalogue, a variable's for a get or a set, a function's inputs and outputs for
//! a call, and those a function's entry and done return take from its signature.

use serde_json::Value;

use super::blueprint::{Blueprint, Does, Graph, Node};
use super::catalogue::{self, Carries, DONE, FAILED, Pin, REASON, RUN, Side, THEN};

/// A pin of a node of a blueprint.
#[derive(Debug, Clone, PartialEq)]
pub(crate) struct NodePin {
    pub(crate) id: String,
    pub(crate) side: Side,
    pub(crate) carries: Carries,
    pub(crate) default: Option<Value>,
}

impl From<&Pin> for NodePin {
    fn from(pin: &Pin) -> Self {
        // The catalogue's tests read every default as JSON.
        let default = pin.default.and_then(|text| serde_json::from_str(text).ok());
        Self {
            id: pin.id.to_owned(),
            side: pin.side,
            carries: pin.carries,
            default,
        }
    }
}

fn value(id: &str, side: Side, carries: Carries) -> NodePin {
    NodePin {
        id: id.to_owned(),
        side,
        carries,
        default: None,
    }
}

/// The pins of `node` in `graph`, or `None` when what it does is unknown: a kind this version lacks, or a variable or a
/// function the blueprint lacks, each its own problem.
pub(crate) fn pins(blueprint: &Blueprint, graph: &Graph, node: &Node) -> Option<Vec<NodePin>> {
    let mut pins: Vec<NodePin>;
    match &node.does {
        Does::Kind(kind) => {
            let kind = catalogue::kind(&kind.0)?;
            pins = kind.pins.iter().map(NodePin::from).collect();
            let signature = graph.function.as_ref();
            let parameters = match kind.id {
                "entry" => signature.map(|signature| (&signature.inputs, Side::Out)),
                "return_done" => signature.map(|signature| (&signature.outputs, Side::In)),
                _ => None,
            };
            if let Some((parameters, side)) = parameters {
                pins.extend(
                    parameters
                        .iter()
                        .map(|it| value(&it.id.0, side, Carries::Value(it.value_type))),
                );
            }
        }
        Does::Get(variable) | Does::Set(variable) => {
            let variable = blueprint.variables.iter().find(|known| &known.id == variable)?;
            let carries = Carries::Value(variable.value_type);
            pins = vec![value("value", Side::Out, carries)];
            if matches!(node.does, Does::Set(_)) {
                pins.extend([(&RUN).into(), (&THEN).into(), value("value", Side::In, carries)]);
            }
        }
        Does::Call(function) => {
            let graph = blueprint.graphs.iter().find(|known| &known.id == function)?;
            let signature = graph.function.as_ref()?;
            pins = [RUN, DONE, FAILED, REASON].iter().map(NodePin::from).collect();
            let typed = |it: &super::blueprint::Parameter| Carries::Value(it.value_type);
            pins.extend(signature.inputs.iter().map(|it| value(&it.id.0, Side::In, typed(it))));
            pins.extend(signature.outputs.iter().map(|it| value(&it.id.0, Side::Out, typed(it))));
        }
    }
    Some(pins)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::blueprint::Id;
    use crate::bot::json::{read, tests::HARVEST};
    use crate::bot::variable::ValueType;

    fn pins_of(node: usize) -> Vec<(String, Side, Carries)> {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let graph = &blueprint.graphs[0];
        let pins = pins(&blueprint, graph, &graph.nodes[node]).unwrap_or_default();
        pins.into_iter().map(|pin| (pin.id, pin.side, pin.carries)).collect()
    }

    #[test]
    fn a_catalogue_node_has_its_kinds_pins_with_their_defaults() {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let graph = &blueprint.graphs[0];
        let harvest = pins(&blueprint, graph, &graph.nodes[2]).unwrap_or_default();
        let pods = harvest
            .iter()
            .find(|pin| pin.id == "pods")
            .and_then(|pin| pin.default.clone());
        assert_eq!(pods, Some(serde_json::json!(90)));
        assert_eq!(harvest.len(), 8);
    }

    #[test]
    fn a_get_reads_its_variable_and_a_set_writes_it() {
        let zone = Carries::Value(ValueType::Zone);
        assert_eq!(pins_of(1), [("value".to_owned(), Side::Out, zone)]);
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        blueprint.graphs[0].nodes[1].does = Does::Set(Id("zone".to_owned()));
        let graph = &blueprint.graphs[0];
        let set = pins(&blueprint, graph, &graph.nodes[1]).unwrap_or_default();
        let ids: Vec<_> = set.iter().map(|pin| (pin.id.as_str(), pin.side)).collect();
        assert_eq!(
            ids,
            [
                ("value", Side::Out),
                ("run", Side::In),
                ("then", Side::Out),
                ("value", Side::In)
            ]
        );
    }

    #[test]
    fn a_call_has_its_functions_inputs_and_outputs() {
        let bank = ("bank".to_owned(), Side::In, Carries::Value(ValueType::Bank));
        let pins = pins_of(3);
        assert_eq!(pins.len(), 5);
        assert!(pins.contains(&bank) && pins.iter().any(|pin| pin.0 == "failed"));
    }

    #[test]
    fn a_functions_entry_and_done_return_take_its_signature() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let entry: Node = serde_json::from_str(r#"{"id": "e", "kind": "entry"}"#).unwrap_or_else(|e| panic!("{e}"));
        blueprint.graphs[1].nodes.push(entry);
        let graph = &blueprint.graphs[1];
        let entry = pins(&blueprint, graph, &graph.nodes[0]).unwrap_or_default();
        assert!(
            entry.iter().any(|pin| pin.id == "bank" && pin.side == Side::Out),
            "{entry:?}"
        );
    }

    #[test]
    fn a_node_doing_what_is_unknown_has_no_pins() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let unknown = [
            Does::Kind(Id("teleport".into())),
            Does::Get(Id("bank".into())),
            Does::Call(Id("events".into())),
        ];
        for does in unknown {
            blueprint.graphs[0].nodes[0].does = does.clone();
            let graph = &blueprint.graphs[0];
            assert_eq!(pins(&blueprint, graph, &graph.nodes[0]), None, "{does:?}");
        }
    }
}
