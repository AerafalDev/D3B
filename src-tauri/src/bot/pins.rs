//! A node's pins: its kind's from the catalogue, its wildcards of the type it notes, a variable's for a get or a set, a
//! function's inputs and outputs for a call, and those a function's entry and done return take from its signature.

use serde_json::Value;

use super::blueprint::{Blueprint, Does, End, Graph, Node, Parameter};
use super::catalogue::{self, Carries, DONE, FAILED, Pin, REASON, RUN, Side, THEN};
use super::variable::Type;

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
            if let Some(of) = node.options.value_type {
                for pin in &mut pins {
                    if let Carries::Wildcard(shape) = pin.carries {
                        pin.carries = Carries::Value(Type::new(of, shape));
                    }
                }
            }
            let signature = graph.function.as_ref();
            let parameters = match kind.id {
                "entry" => signature.map(|signature| (&signature.inputs, Side::Out)),
                "return_done" => signature.map(|signature| (&signature.outputs, Side::In)),
                _ => None,
            };
            if let Some((parameters, side)) = parameters {
                pins.extend(usable(parameters).map(|it| value(&it.id.0, side, Carries::Value(it.of_type()))));
            }
        }
        Does::Get(variable) | Does::Set(variable) => {
            let variable = blueprint.variables.iter().find(|known| &known.id == variable)?;
            let carries = Carries::Value(variable.of_type());
            pins = vec![value("value", Side::Out, carries)];
            if matches!(node.does, Does::Set(_)) {
                pins.extend([(&RUN).into(), (&THEN).into(), value("value", Side::In, carries)]);
            }
        }
        Does::Call(function) => {
            let graph = blueprint.graphs.iter().find(|known| &known.id == function)?;
            let signature = graph.function.as_ref()?;
            pins = [RUN, DONE, FAILED, REASON].iter().map(NodePin::from).collect();
            let typed = |it: &Parameter| Carries::Value(it.of_type());
            pins.extend(usable(&signature.inputs).map(|it| value(&it.id.0, Side::In, typed(it))));
            pins.extend(usable(&signature.outputs).map(|it| value(&it.id.0, Side::Out, typed(it))));
        }
    }
    Some(pins)
}

/// Whether a wire or a value typed uses one of `node`'s wildcards, from its kind: what its type is noted for.
pub(crate) fn uses_wildcards(graph: &Graph, node: &Node) -> bool {
    let at = |end: &End, pin: &str| end.0 == node.id && end.1.0 == pin;
    wildcards(node).any(|pin| {
        node.inputs.keys().any(|id| id.0 == pin)
            || graph.wires.iter().any(|wire| at(&wire.from, pin) || at(&wire.to, pin))
    })
}

/// Whether `node`'s kind has a wildcard pin, which a type noted on it is for.
pub(crate) fn has_wildcards(node: &Node) -> bool {
    wildcards(node).next().is_some()
}

/// The ids of the wildcard pins of `node`'s kind.
fn wildcards(node: &Node) -> impl Iterator<Item = &'static str> {
    let kind = match &node.does {
        Does::Kind(kind) => catalogue::kind(&kind.0),
        _ => None,
    };
    let pins = kind.into_iter().flat_map(|kind| kind.pins);
    pins.filter(|pin| matches!(pin.carries, Carries::Wildcard(_)))
        .map(|pin| pin.id)
}

/// A function's parameters that become pins: the first of an id, none with a reserved one, each already a problem.
fn usable(parameters: &[Parameter]) -> impl Iterator<Item = &Parameter> {
    let mut ids = std::collections::HashSet::new();
    parameters
        .iter()
        .filter(move |it| !catalogue::RESERVED.contains(&it.id.0.as_str()) && ids.insert(&it.id))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::blueprint::Id;
    use crate::bot::json::{read, tests::HARVEST};
    use crate::bot::variable::{Shape, ValueType};

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
    fn a_wildcard_takes_the_type_its_node_notes_in_its_shape_and_none_until_then() {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let graph = &blueprint.graphs[0];
        let mut each: Node =
            serde_json::from_str(r#"{"id": "e", "kind": "test_for_each"}"#).unwrap_or_else(|error| panic!("{error}"));
        let carried = |node: &Node| {
            let pins = pins(&blueprint, graph, node).unwrap_or_default();
            let of = |id: &str| pins.iter().find(|pin| pin.id == id).map(|pin| pin.carries);
            (of("list"), of("element"), of("body"))
        };
        let (one, list) = (Shape::ONE, Shape::new(true, false));
        let exec = Some(Carries::Exec);
        assert_eq!(
            carried(&each),
            (Some(Carries::Wildcard(list)), Some(Carries::Wildcard(one)), exec)
        );
        each.options.value_type = Some(ValueType::Map);
        let map = |shape| Some(Carries::Value(Type::new(ValueType::Map, shape)));
        assert_eq!(carried(&each), (map(list), map(one), exec));
    }

    #[test]
    fn a_list_variable_and_parameter_carry_lists() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        blueprint.variables[0].list = true;
        if let Some(signature) = blueprint.graphs[1].function.as_mut() {
            signature.inputs[0].ranges = true;
        }
        let graph = &blueprint.graphs[0];
        let carried = |node: usize, pin: &str| {
            let pins = pins(&blueprint, graph, &graph.nodes[node]).unwrap_or_default();
            pins.into_iter().find(|it| it.id == pin).map(|it| it.carries)
        };
        let zones = Carries::Value(Type::new(ValueType::Zone, Shape::new(true, false)));
        let banks = Carries::Value(Type::new(ValueType::Bank, Shape::new(false, true)));
        assert_eq!((carried(1, "value"), carried(3, "bank")), (Some(zones), Some(banks)));
    }

    #[test]
    fn a_get_reads_its_variable_and_a_set_writes_it() {
        let zone = Carries::one(ValueType::Zone);
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
        let bank = ("bank".to_owned(), Side::In, Carries::one(ValueType::Bank));
        let pins = pins_of(3);
        assert_eq!(pins.len(), 5);
        assert!(pins.contains(&bank) && pins.iter().any(|pin| pin.0 == "failed"));
    }

    #[test]
    fn a_function_gives_its_entry_and_calls_its_inputs_and_its_done_return_and_calls_its_outputs() {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let signature = blueprint.graphs[1]
            .function
            .as_mut()
            .unwrap_or_else(|| panic!("a function"));
        let deposited = Parameter {
            id: Id("deposited".into()),
            name: String::new(),
            value_type: ValueType::Number,
            list: false,
            ranges: false,
        };
        // A reserved id and an id given twice are problems of their own, never pins.
        let reserved = Parameter {
            id: Id("run".into()),
            ..deposited.clone()
        };
        signature.outputs.extend([deposited.clone(), deposited, reserved]);
        for kind in ["entry", "return_done"] {
            let node = serde_json::from_str(&format!(r#"{{"id": "{kind}", "kind": "{kind}"}}"#));
            blueprint.graphs[1]
                .nodes
                .push(node.unwrap_or_else(|error| panic!("{error}")));
        }
        let (events, function) = (&blueprint.graphs[0], &blueprint.graphs[1]);
        let sides = |graph, node| {
            let pins = pins(&blueprint, graph, node).unwrap_or_default();
            let named = |id: &str| {
                pins.iter()
                    .filter(|pin| pin.id == id)
                    .map(|pin| pin.side)
                    .collect::<Vec<_>>()
            };
            (named("bank"), named("deposited"), named("run"))
        };
        assert_eq!(sides(function, &function.nodes[0]), (vec![Side::Out], vec![], vec![]));
        assert_eq!(
            sides(function, &function.nodes[1]),
            (vec![], vec![Side::In], vec![Side::In])
        );
        assert_eq!(
            sides(events, &events.nodes[3]),
            (vec![Side::In], vec![Side::Out], vec![Side::In])
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
