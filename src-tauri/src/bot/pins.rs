//! A node's pins: its kind's from the catalogue, its wildcards of the type it notes, a variable's for a get or a set, a
//! function's inputs and outputs for a call, and those a function's entry and done return take from its signature.

use serde_json::Value;

use super::blueprint::{Blueprint, Does, End, Graph, Node, Parameter};
use super::blueprint::{Id, Options, Signature};
use super::catalogue::{self, Carries, DONE, FAILED, Pin, REASON, RUN, Side, THEN, Takes};
use super::variable::Type;
use super::variable::Variable;

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
    built(blueprint, graph, node, true)
}

/// `node`'s pins as the editor's check sees them: an untyped wildcard stays one, though its kind reads its first type
/// meanwhile (`≥`'s numbers), so a wire or a value of another type it takes can still type it; but only while no
/// other wire or value uses its wildcards as that first type, which a new type would change under them. `replacing`
/// is the input an edit replaces the wire and value of, which do not count.
pub(crate) fn open_pins(
    blueprint: &Blueprint,
    graph: &Graph,
    node: &Node,
    replacing: Option<&Id>,
) -> Option<Vec<NodePin>> {
    let mut alone = node.clone();
    let mut graph_alone = graph.clone();
    if let Some(pin) = replacing {
        alone.inputs.remove(pin);
        graph_alone
            .wires
            .retain(|wire| !(wire.to.0 == node.id && &wire.to.1 == pin));
    }
    built(blueprint, graph, node, uses_wildcards(&graph_alone, &alone))
}

/// Whether `node`'s kind lets its wildcards take `of`: any type, unless it lists those it takes.
pub(crate) fn takes_type(node: &Node, of: super::variable::ValueType) -> bool {
    let Does::Kind(kind) = &node.does else {
        return true;
    };
    catalogue::kind(&kind.0).is_none_or(|kind| kind.types.is_empty() || kind.types.contains(&of))
}

fn built(blueprint: &Blueprint, graph: &Graph, node: &Node, defaulted: bool) -> Option<Vec<NodePin>> {
    let mut pins: Vec<NodePin>;
    match &node.does {
        Does::Kind(kind) => {
            let kind = catalogue::kind(&kind.0)?;
            pins = kind.pins.iter().map(NodePin::from).collect();
            pins.extend(kind.takes.iter().flat_map(|takes| optioned(*takes, &node.options)));
            // A kind that changes a variable types its wildcards by it, else by the type the node notes.
            let typed = if kind.takes.contains(&Takes::Variable) {
                let named = node.options.variable.as_ref();
                named.and_then(|id| variable_of(blueprint, graph, id).map(|it| it.value_type))
            } else {
                node.options
                    .value_type
                    .or(kind.types.first().copied().filter(|_| defaulted))
            };
            if let Some(of) = typed {
                for pin in &mut pins {
                    if let Carries::Wildcard(shape) = pin.carries {
                        pin.carries = Carries::Value(Type::new(of, shape));
                    }
                }
            }
            let signature = graph.function.as_ref();
            let parameters = match kind.id {
                "entry" => signature.map(|signature| (&signature.inputs, Side::Out)),
                "return_done" | "return_exit" | "result" => signature.map(|signature| (&signature.outputs, Side::In)),
                "my_event" => {
                    let named = node.options.event.as_ref();
                    let event = named.and_then(|id| blueprint.events.iter().find(|it| &it.id == id));
                    event.map(|event| (&event.inputs, Side::Out))
                }
                _ => None,
            };
            // A pure function's entry starts no flow.
            if kind.id == "entry" && signature.is_some_and(|signature| signature.pure) {
                pins.retain(|pin| pin.carries != Carries::Exec);
            }
            if let Some((parameters, side)) = parameters {
                pins.extend(usable(parameters).map(|it| value(&it.id.0, side, Carries::Value(it.of_type()))));
            }
        }
        Does::Get(variable) | Does::Set(variable) => {
            let variable = variable_of(blueprint, graph, variable)?;
            let carries = Carries::Value(variable.of_type());
            pins = vec![value("value", Side::Out, carries)];
            if matches!(node.does, Does::Set(_)) {
                pins.extend([(&RUN).into(), (&THEN).into(), value("value", Side::In, carries)]);
            }
        }
        Does::Call(function) => {
            let graph = blueprint.graphs.iter().find(|known| &known.id == function)?;
            let signature = graph.function.as_ref()?;
            // A pure function's call has no flow: a value node, read when used.
            pins = if signature.pure {
                Vec::new()
            } else {
                [RUN, DONE, FAILED, REASON].iter().map(NodePin::from).collect()
            };
            if !signature.pure {
                pins.extend(exits(signature).map(|it| value(&it.0, Side::Out, Carries::Exec)));
            }
            let typed = |it: &Parameter| Carries::Value(it.of_type());
            pins.extend(usable(&signature.inputs).map(|it| value(&it.id.0, Side::In, typed(it))));
            pins.extend(usable(&signature.outputs).map(|it| value(&it.id.0, Side::Out, typed(it))));
        }
    }
    Some(pins)
}

/// The variable `id` that a node of `graph` reads or writes: the blueprint's, or a local of its function.
pub(crate) fn variable_of<'b>(blueprint: &'b Blueprint, graph: &'b Graph, id: &Id) -> Option<&'b Variable> {
    // A local first, as the runtime reads it; no id is both, which is a problem of its own.
    let locals = graph.function.iter().flat_map(|signature| &signature.locals);
    locals.chain(&blueprint.variables).find(|it| &it.id == id)
}

/// The ids of a function's exits that become its calls' flow outputs: the first of an id, none with a reserved one or
/// an output's, each already a problem.
pub(crate) fn exits(signature: &Signature) -> impl Iterator<Item = &Id> {
    let mut ids = std::collections::HashSet::new();
    signature.exits.iter().map(|exit| &exit.id).filter(move |id| {
        !catalogue::RESERVED.contains(&id.0.as_str())
            && !signature.outputs.iter().any(|it| &it.id == *id)
            && ids.insert(*id)
    })
}

/// The pins `takes` makes from a node's `options`.
fn optioned(takes: Takes, options: &Options) -> Vec<NodePin> {
    match takes {
        Takes::Count { pin, least } => {
            let count = options.count.map_or(least, std::num::NonZeroU8::get);
            let numbered = |at| NodePin {
                id: format!("{}_{at}", pin.id),
                ..NodePin::from(&pin)
            };
            (1..=count).map(numbered).collect()
        }
        Takes::Cases => (1..=options.cases.len())
            .map(|at| value(&format!("case_{at}"), Side::Out, Carries::Exec))
            .collect(),
        Takes::Text => holes(options.text.as_deref().unwrap_or_default())
            .iter()
            .map(|hole| value(&hole.0, Side::In, Carries::Any))
            .collect(),
        // What they name is the blueprint's: `pins` reads it.
        Takes::Expression | Takes::Variable | Takes::Event | Takes::Exit => Vec::new(),
    }
}

/// The holes of a Text's template, each once in their order: a well-formed id between braces, `{count}`. Other braces
/// are text.
pub(crate) fn holes(template: &str) -> Vec<Id> {
    let mut holes: Vec<Id> = Vec::new();
    for (at, _) in template.match_indices('{') {
        let rest = &template[at + 1..];
        let Some(end) = rest.find('}') else { break };
        let hole = Id(rest[..end].to_owned());
        if hole.is_well_formed() && !holes.contains(&hole) {
            holes.push(hole);
        }
    }
    holes
}

/// Whether a wire or a value typed uses one of `node`'s wildcards, from its kind: what its type is noted for.
pub(crate) fn uses_wildcards(graph: &Graph, node: &Node) -> bool {
    let at = |end: &End, pin: &str| end.0 == node.id && end.1.0 == pin;
    let cases = match &node.does {
        Does::Kind(kind) => catalogue::kind(&kind.0).is_some_and(|kind| kind.takes.contains(&Takes::Cases)),
        _ => false,
    };
    (cases && !node.options.cases.is_empty())
        || wildcards(node).any(|pin| {
            node.inputs.keys().any(|id| id.0 == pin)
                || graph.wires.iter().any(|wire| at(&wire.from, pin) || at(&wire.to, pin))
        })
}

/// Whether `node`'s kind has a wildcard pin, which a type noted on it is for.
pub(crate) fn has_wildcards(node: &Node) -> bool {
    wildcards(node).next().is_some()
}

/// The ids of the wildcard pins of `node`'s kind that take the type it notes: none for a kind that changes a variable,
/// whose wildcards take the variable's.
fn wildcards(node: &Node) -> impl Iterator<Item = &'static str> {
    let kind = match &node.does {
        Does::Kind(kind) => catalogue::kind(&kind.0).filter(|kind| !kind.takes.contains(&Takes::Variable)),
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

    fn optioned_pins(kind: &str, options: &str) -> Vec<(String, Side, Carries)> {
        let blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        let text = format!(r#"{{"id": "n", "kind": "{kind}", "options": {options}}}"#);
        let node: Node = serde_json::from_str(&text).unwrap_or_else(|error| panic!("{error}"));
        let pins = pins(&blueprint, &blueprint.graphs[0], &node).unwrap_or_default();
        pins.into_iter().map(|pin| (pin.id, pin.side, pin.carries)).collect()
    }

    #[test]
    fn options_make_numbered_outputs_a_case_each_and_an_input_per_hole() {
        let ids = |pins: Vec<(String, Side, Carries)>| pins.into_iter().map(|pin| pin.0).collect::<Vec<_>>();
        assert_eq!(ids(optioned_pins("test_sequence", "{}")), ["run", "then_1", "then_2"]);
        let three = optioned_pins("test_sequence", r#"{"count": 3}"#);
        assert_eq!(three.last(), Some(&("then_3".to_owned(), Side::Out, Carries::Exec)));
        let switch = optioned_pins("test_switch", r#"{"type": "job", "cases": [24, 28]}"#);
        assert_eq!(ids(switch), ["run", "value", "otherwise", "case_1", "case_2"]);
        let text = optioned_pins("test_text", r#"{"text": "{count} in {zone}, {count} {Zone} {} {"}"#);
        let any = |id: &str| (id.to_owned(), Side::In, Carries::Any);
        assert_eq!(text[1..], [any("count"), any("zone")]);
    }

    #[test]
    fn a_kind_that_changes_a_variable_types_its_wildcards_by_it() {
        let amount = |options| optioned_pins("test_add_to", options).pop().map(|pin| pin.2);
        assert_eq!(
            amount(r#"{"variable": "pods"}"#),
            Some(Carries::one(ValueType::Percentage))
        );
        assert_eq!(
            amount(r#"{"variable": "nothing"}"#),
            Some(Carries::Wildcard(Shape::ONE))
        );
        assert_eq!(
            amount(r#"{"type": "zone"}"#),
            Some(Carries::Wildcard(Shape::ONE)),
            "its type is its variable's"
        );
    }

    #[test]
    fn an_event_of_mine_gives_its_inputs_and_a_call_has_its_functions_exits_and_none_when_pure() {
        let text = r#"{"schema": 1, "id": "b", "name": "B", "version": 1,
          "events": [{"id": "empty", "name": "Empty", "inputs": [{"id": "zone", "name": "Zone", "type": "zone"}]}],
          "graphs": [{"id": "events", "name": "Events", "nodes": [{"id": "e", "kind": "my_event", "options": {"event": "empty"}},
                       {"id": "c", "call": "f"}, {"id": "p", "call": "half"}]},
            {"id": "f", "name": "F", "function": {"exits": [{"id": "full", "name": "Full"}],
               "locals": [{"id": "tries", "name": "Tries", "type": "map"}]},
             "nodes": [{"id": "t", "get": "tries"}, {"id": "x", "kind": "return_exit", "options": {"exit": "full"}}]},
            {"id": "half", "name": "Half", "function": {"inputs": [{"id": "a", "name": "A", "type": "number"}],
               "outputs": [{"id": "b", "name": "B", "type": "number"}], "pure": true},
             "nodes": [{"id": "in", "kind": "entry"}]}]}"#;
        let blueprint = read(text).unwrap_or_else(|error| panic!("{error}"));
        let ids = |graph: usize, node: usize| {
            let graph = &blueprint.graphs[graph];
            let pins = pins(&blueprint, graph, &graph.nodes[node]).unwrap_or_default();
            pins.into_iter()
                .map(|pin| (pin.id, pin.side, pin.carries))
                .collect::<Vec<_>>()
        };
        let exec = |id: &str, side| (id.to_owned(), side, Carries::Exec);
        let one = |id: &str, side, of| (id.to_owned(), side, Carries::one(of));
        assert_eq!(
            ids(0, 0),
            [exec("then", Side::Out), one("zone", Side::Out, ValueType::Zone)]
        );
        assert!(
            ids(0, 1).contains(&exec("full", Side::Out)),
            "the exit, a flow output of its call"
        );
        assert_eq!(
            ids(0, 2),
            [
                one("a", Side::In, ValueType::Number),
                one("b", Side::Out, ValueType::Number)
            ]
        );
        assert_eq!(
            ids(1, 0),
            [one("value", Side::Out, ValueType::Map)],
            "a local, read in its function"
        );
        assert_eq!(ids(1, 1), [exec("run", Side::In)]);
        assert_eq!(
            ids(2, 0),
            [one("a", Side::Out, ValueType::Number)],
            "a pure function's entry starts no flow"
        );
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
