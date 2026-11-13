//! The problems of a graph's nodes against the catalogue and their pins: a kind unknown or out of its place, values
//! typed on pins that do not take them, wires between pins that do not exist, go the wrong way or carry another type,
//! an input fed twice or by nothing, a flow output wired twice. A node whose pins are unknown is reported once, never
//! again through its wires.

use std::collections::{HashMap, HashSet};

use super::blueprint::{Blueprint, Does, End, Graph, Id};
use super::catalogue::{self, Carries, Family, Side};
use super::pins::{NodePin, pins};
use super::problems::Problem;

/// The problems of `graph`'s nodes and wires, after those `problems` finds itself.
pub(crate) fn of_graph(blueprint: &Blueprint, graph: &Graph, found: &mut Vec<Problem>) {
    let at = |node: &Id| (graph.id.clone(), node.clone());
    let mut known: HashMap<&Id, Vec<NodePin>> = HashMap::new();
    let mut ids = HashSet::new();
    // A node whose id another holds is its own problem: which of them a wire means is unknown.
    let twice: HashSet<&Id> = graph
        .nodes
        .iter()
        .map(|node| &node.id)
        .filter(|id| !ids.insert(*id))
        .collect();
    for node in &graph.nodes {
        let takes_settings = match &node.does {
            Does::Kind(id) => match catalogue::kind(&id.0) {
                None => {
                    let (graph, node) = at(&node.id);
                    found.push(Problem::UnknownKind {
                        graph,
                        node,
                        kind: id.clone(),
                    });
                    continue;
                }
                Some(kind) => {
                    let in_function = graph.function.is_some();
                    if (kind.family == Family::Event && in_function)
                        || (kind.family == Family::Function && !in_function)
                    {
                        let (graph, node) = at(&node.id);
                        found.push(Problem::OutOfPlace { graph, node });
                    }
                    kind.event.is_some_and(|role| role.settings)
                }
            },
            _ => false,
        };
        if node.event.is_some() && !takes_settings {
            let (graph, node) = at(&node.id);
            found.push(Problem::EventSettingsOnNoEvent { graph, node });
        }
        let Some(node_pins) = pins(blueprint, graph, node).filter(|_| !twice.contains(&node.id)) else {
            continue;
        };
        for (pin, value) in &node.inputs {
            let typed = find(&node_pins, pin, Side::In).and_then(|found| match found.carries {
                Carries::Value(value_type) => Some(value_type),
                Carries::Exec => None,
            });
            let (graph, node) = at(&node.id);
            match typed {
                None => found.push(Problem::NoSuchInput {
                    graph,
                    node,
                    pin: pin.clone(),
                }),
                // No value, `null`, is a variable's default only: a node's input has a value or says none.
                Some(value_type) if value.is_null() || !value_type.holds(value) => {
                    found.push(Problem::ValueNotOfType {
                        graph,
                        node,
                        pin: pin.clone(),
                    });
                }
                Some(_) => {}
            }
        }
        known.insert(&node.id, node_pins);
    }
    wires(graph, &known, found);
    for node in &graph.nodes {
        let Some(node_pins) = known.get(&node.id) else { continue };
        for pin in node_pins
            .iter()
            .filter(|pin| pin.side == Side::In && matches!(pin.carries, Carries::Value(_)))
        {
            let id = Id(pin.id.clone());
            // Fed by any wire, even one whose other end is a problem of its own.
            let wired = graph.wires.iter().any(|wire| wire.to.0 == node.id && wire.to.1 == id);
            if !wired && !node.inputs.contains_key(&id) && pin.default.is_none() {
                let (graph, node) = at(&node.id);
                found.push(Problem::InputWithoutValue { graph, node, pin: id });
            }
        }
    }
}

/// The wires' problems.
fn wires(graph: &Graph, known: &HashMap<&Id, Vec<NodePin>>, found: &mut Vec<Problem>) {
    let (mut seen, mut flows, mut fed) = (HashSet::new(), HashSet::new(), HashSet::new());
    for wire in &graph.wires {
        // A wire given twice is its own problem; a node not in the graph, or whose pins are unknown, already one.
        let (Some(from_pins), Some(to_pins)) = (known.get(&wire.from.0), known.get(&wire.to.0)) else {
            continue;
        };
        if !seen.insert((&wire.from, &wire.to)) {
            continue;
        }
        // One problem per wire: its far end is not looked at once its near end is one.
        let Some(from) = end(graph, from_pins, &wire.from, Side::Out, found) else {
            continue;
        };
        let Some(to) = end(graph, to_pins, &wire.to, Side::In, found) else {
            continue;
        };
        if !from.carries.feeds(to.carries) {
            let problem = Problem::WireOfAnotherType {
                graph: graph.id.clone(),
                from: wire.from.clone(),
                to: wire.to.clone(),
            };
            found.push(problem);
        }
        if from.carries == Carries::Exec && !flows.insert(&wire.from) {
            let (graph, node, pin) = (graph.id.clone(), wire.from.0.clone(), wire.from.1.clone());
            found.push(Problem::OutputWiredTwice { graph, node, pin });
        }
        if matches!(to.carries, Carries::Value(_)) {
            let typed = graph
                .nodes
                .iter()
                .any(|node| node.id == wire.to.0 && node.inputs.contains_key(&wire.to.1));
            // Fed twice, said once: by its first wire when a value is typed, else by its second.
            let first = fed.insert(&wire.to);
            if first == typed {
                let (graph, node, pin) = (graph.id.clone(), wire.to.0.clone(), wire.to.1.clone());
                found.push(Problem::InputFedTwice { graph, node, pin });
            }
        }
    }
}

/// The pin `end` names on its `side`, or its problem: a pin of the other side, or none.
fn end<'p>(graph: &Graph, pins: &'p [NodePin], end: &End, side: Side, found: &mut Vec<Problem>) -> Option<&'p NodePin> {
    let pin = find(pins, &end.1, side);
    if pin.is_none() {
        let (graph, node, pin) = (graph.id.clone(), end.0.clone(), end.1.clone());
        let other = if side == Side::In { Side::Out } else { Side::In };
        found.push(if find(pins, &pin, other).is_some() {
            Problem::WireTheWrongWay { graph, node, pin }
        } else {
            Problem::WireToNoPin { graph, node, pin }
        });
    }
    pin
}

fn find<'p>(pins: &'p [NodePin], id: &Id, side: Side) -> Option<&'p NodePin> {
    pins.iter().find(|pin| pin.side == side && pin.id == id.0)
}

/// Each event a blueprint may hold once that it holds again.
pub(crate) fn once_twice(blueprint: &Blueprint, found: &mut Vec<Problem>) {
    let mut seen = HashSet::new();
    // An event in a function is out of place, a problem of its own.
    for graph in blueprint.graphs.iter().filter(|graph| graph.function.is_none()) {
        for node in &graph.nodes {
            let Does::Kind(id) = &node.does else { continue };
            let once = catalogue::kind(&id.0)
                .and_then(|kind| kind.event)
                .is_some_and(|role| role.once);
            if once && !seen.insert(&id.0) {
                found.push(Problem::EventTwice {
                    graph: graph.id.clone(),
                    node: node.id.clone(),
                });
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;
    use crate::bot::blueprint::{Node, Wire};
    use crate::bot::json::{read, tests::HARVEST};
    use crate::bot::problems::problems;

    fn id(text: &str) -> Id {
        Id(text.to_owned())
    }

    /// The problems of the hand-written harvest once `edit` changed it.
    fn after(edit: impl FnOnce(&mut Blueprint)) -> Vec<Problem> {
        let mut blueprint = read(HARVEST).unwrap_or_else(|error| panic!("{error}"));
        edit(&mut blueprint);
        problems(&blueprint)
    }

    fn node(text: &str) -> Node {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn wire(from: &str, out: &str, to: &str, input: &str) -> Wire {
        Wire {
            from: End(id(from), id(out)),
            to: End(id(to), id(input)),
        }
    }

    /// The event graph's `[node, pin]`, the place of most of these problems.
    fn at(node: &str, pin: &str) -> (Id, Id, Id) {
        (id("events"), id(node), id(pin))
    }

    #[test]
    fn a_kind_this_version_does_not_know_is_one_problem_and_no_more() {
        let found = after(|blueprint| blueprint.graphs[0].nodes[0] = node(r#"{"id": "n1", "kind": "teleport"}"#));
        let (graph, node, kind) = at("n1", "teleport");
        assert_eq!(found, [Problem::UnknownKind { graph, node, kind }]);
    }

    #[test]
    fn an_event_in_a_function_or_an_entry_in_an_event_graph_is_out_of_place() {
        let found = after(|blueprint| {
            blueprint.graphs[1]
                .nodes
                .push(node(r#"{"id": "f1", "kind": "on_start"}"#));
            blueprint.graphs[0].nodes.extend(
                [
                    r#"{"id": "e1", "kind": "entry"}"#,
                    r#"{"id": "e2", "kind": "return_done"}"#,
                ]
                .map(node),
            );
        });
        let out = |graph: &str, node: &str| Problem::OutOfPlace {
            graph: id(graph),
            node: id(node),
        };
        // The `on_start` in the function is out of place, not held twice.
        assert_eq!(
            found,
            [out("events", "e1"), out("events", "e2"), out("empty_at_bank", "f1")]
        );
    }

    #[test]
    fn a_priority_on_a_node_that_takes_none_is_a_problem() {
        let found = after(|blueprint| {
            blueprint.graphs[0].nodes[0] = node(r#"{"id": "n1", "kind": "on_start", "event": {"priority": 1}}"#);
            blueprint.graphs[0].nodes[1] = node(r#"{"id": "n2", "get": "zone", "event": {"priority": 1}}"#);
        });
        let on = |node: &str| Problem::EventSettingsOnNoEvent {
            graph: id("events"),
            node: id(node),
        };
        assert_eq!(found, [on("n1"), on("n2")]);
    }

    #[test]
    fn a_value_on_no_value_input_or_not_of_its_type_is_a_problem() {
        let found = after(|blueprint| {
            let harvest = &mut blueprint.graphs[0].nodes[2].inputs;
            harvest.extend([
                (id("colour"), json!(1)),
                (id("run"), json!(1)),
                (id("what"), json!("some")),
            ]);
            blueprint.graphs[0].nodes[3].inputs.insert(id("keep"), json!("all"));
        });
        let no = |node, pin| {
            let (graph, node, pin) = at(node, pin);
            Problem::NoSuchInput { graph, node, pin }
        };
        let (graph, node, pin) = at("n3", "what");
        assert_eq!(
            found,
            [
                no("n3", "colour"),
                no("n3", "run"),
                Problem::ValueNotOfType { graph, node, pin },
                no("n4", "keep")
            ]
        );
    }

    #[test]
    fn no_value_typed_on_an_input_is_not_its_value() {
        let found = after(|blueprint| {
            blueprint.graphs[0].wires.pop();
            blueprint.graphs[0].nodes[2]
                .inputs
                .insert(id("zone"), serde_json::Value::Null);
        });
        let (graph, node, pin) = at("n3", "zone");
        assert_eq!(found, [Problem::ValueNotOfType { graph, node, pin }]);
    }

    #[test]
    fn a_wire_to_no_pin_or_the_wrong_way_is_one_problem() {
        let found = after(|blueprint| {
            let wires = [
                wire("n1", "go", "n4", "run"),
                wire("n3", "zone", "n4", "bank"),
                wire("n3", "run", "n1", "then"),
            ];
            blueprint.graphs[0].wires.extend(wires);
        });
        let ((graph, node, pin), wrong) = (at("n1", "go"), |node, pin| {
            let (graph, node, pin) = at(node, pin);
            Problem::WireTheWrongWay { graph, node, pin }
        });
        assert_eq!(
            found,
            [
                Problem::WireToNoPin { graph, node, pin },
                wrong("n3", "zone"),
                wrong("n3", "run")
            ]
        );
    }

    #[test]
    fn a_wire_carries_one_type_and_a_percentage_feeds_a_number() {
        let found = after(|blueprint| {
            let graph = &mut blueprint.graphs[0];
            graph.nodes[3].inputs.clear();
            graph.nodes.extend(
                [
                    r#"{"id": "q", "kind": "life_percent"}"#,
                    r#"{"id": "s", "kind": "add", "inputs": {"b": 1}}"#,
                ]
                .map(node),
            );
            graph
                .wires
                .extend([wire("n2", "value", "n4", "bank"), wire("q", "value", "s", "a")]);
        });
        let (from, to) = (End(id("n2"), id("value")), End(id("n4"), id("bank")));
        assert_eq!(
            found,
            [Problem::WireOfAnotherType {
                graph: id("events"),
                from,
                to
            }]
        );
    }

    #[test]
    fn the_flow_goes_one_way_and_an_input_has_one_value() {
        let found = after(|blueprint| {
            let graph = &mut blueprint.graphs[0];
            let more = [
                r#"{"id": "n6", "get": "zone"}"#,
                r#"{"id": "q", "kind": "pods_percent"}"#,
                r#"{"id": "r", "kind": "pods_percent"}"#,
            ];
            graph.nodes.extend(more.map(node));
            graph.nodes[3].inputs.clear();
            graph.nodes[2].inputs.insert(id("pods"), json!(80));
            graph
                .wires
                .extend([wire("n1", "then", "n4", "run"), wire("n6", "value", "n3", "zone")]);
            graph
                .wires
                .extend([wire("q", "value", "n3", "pods"), wire("r", "value", "n3", "pods")]);
        });
        let fed = |pin| {
            let (graph, node, pin) = at("n3", pin);
            Problem::InputFedTwice { graph, node, pin }
        };
        let ((graph, node, pin), (in_graph, in_node, in_pin)) = (at("n1", "then"), at("n4", "bank"));
        let twice = Problem::OutputWiredTwice { graph, node, pin };
        let without = Problem::InputWithoutValue {
            graph: in_graph,
            node: in_node,
            pin: in_pin,
        };
        // A value typed and two wires on `pods`: fed twice, said once.
        assert_eq!(found, [twice, fed("zone"), fed("pods"), without]);
    }

    #[test]
    fn an_input_fed_by_a_wire_whose_other_end_is_a_problem_is_not_without_value() {
        let found = after(|blueprint| blueprint.graphs[0].nodes[1] = node(r#"{"id": "n2", "get": "bank"}"#));
        let (graph, node, variable) = at("n2", "bank");
        assert_eq!(found, [Problem::NoSuchVariable { graph, node, variable }]);
    }

    #[test]
    fn a_node_whose_id_another_holds_is_one_problem() {
        let found = after(|blueprint| blueprint.graphs[0].nodes[4].id = id("n3"));
        assert_eq!(
            found,
            [Problem::NodeTwice {
                graph: id("events"),
                node: id("n3")
            }]
        );
    }

    #[test]
    fn an_event_held_once_at_most_is_a_problem_the_second_time() {
        let found = after(|blueprint| {
            let more = [
                ("n6", "on_start"),
                ("n7", "life_below"),
                ("d1", "died"),
                ("d2", "died"),
                ("a1", "action_failed"),
            ];
            for (id, kind) in
                more.into_iter()
                    .chain([("a2", "action_failed"), ("f1", "fight_starts"), ("f2", "fight_starts")])
            {
                blueprint.graphs[0]
                    .nodes
                    .push(node(&format!(r#"{{"id": "{id}", "kind": "{kind}"}}"#)));
            }
        });
        let twice = |node: &str| Problem::EventTwice {
            graph: id("events"),
            node: id(node),
        };
        assert_eq!(found, [twice("n6"), twice("d2"), twice("a2"), twice("f2")]);
    }
}
