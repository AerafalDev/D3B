//! The problems of a graph's nodes against the catalogue and their pins: a kind unknown or out of its place, values
//! typed on pins that do not take them, wires between pins that do not exist, go the wrong way or carry another type or
//! shape, a wildcard used before its node notes its type, an input fed twice or by nothing, a flow output wired twice.
//! A node whose pins are unknown is reported once, never again through its wires.

use std::collections::{HashMap, HashSet};

use super::blueprint::{Blueprint, Does, End, Graph, Id, Node};
use super::catalogue::Takes;
use super::catalogue::{self, Carries, Family, Side};
use super::pins::{NodePin, has_wildcards, pins, uses_wildcards};
use super::problems::Problem;
use super::variable::{Shape, Type};

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
                    let pure = graph.function.as_ref().is_some_and(|signature| signature.pure);
                    // A pure function's result is its own, in no other graph.
                    if (kind.family == Family::Event && in_function)
                        || (kind.family == Family::Function && !in_function)
                        || (kind.id == "result" && !pure)
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
        of_options(blueprint, graph, node, found);
        // A pure function computes values only: no node of it has a flow pin, as no action or wait has none.
        let pure = graph.function.as_ref().is_some_and(|signature| signature.pure);
        if pure && node_pins.iter().any(|pin| pin.carries == Carries::Exec) {
            let (graph, node) = at(&node.id);
            found.push(Problem::NotPure { graph, node });
        }
        // Wired or given a value, an untyped wildcard is said once for its node; its wires are not looked at. A kind
        // with a type by default has none untyped.
        let untyped = node_pins.iter().any(|pin| matches!(pin.carries, Carries::Wildcard(_)));
        if untyped && uses_wildcards(graph, node) {
            let (graph, node) = at(&node.id);
            found.push(Problem::WildcardWithoutType { graph, node });
        }
        for (pin, value) in &node.inputs {
            let (graph, node) = at(&node.id);
            let pin = pin.clone();
            match find(&node_pins, &pin, Side::In).map(|found| found.carries) {
                None | Some(Carries::Exec) => found.push(Problem::NoSuchInput { graph, node, pin }),
                // No value, `null`, is a variable's default only: a node's input has a value or says none.
                Some(Carries::Value(typed)) if value.is_null() || !typed.holds(value) => {
                    found.push(Problem::ValueNotOfType { graph, node, pin });
                }
                Some(Carries::Value(typed)) if typed.overlaps(value) => {
                    found.push(Problem::RangesOverlap { graph, node, pin });
                }
                // Typed there, a hole's value is text; any other comes by a wire.
                Some(Carries::Any) if !value.is_string() => found.push(Problem::ValueNotOfType { graph, node, pin }),
                // A value on a wildcard of no type is its node's problem, said above.
                Some(Carries::Value(_) | Carries::Wildcard(_) | Carries::Any) => {}
            }
        }
        known.insert(&node.id, node_pins);
    }
    wires(graph, &known, found);
    leaves_outside_loops(graph, &known, found);
    for node in &graph.nodes {
        let Some(node_pins) = known.get(&node.id) else { continue };
        for pin in node_pins
            .iter()
            .filter(|pin| pin.side == Side::In && pin.carries.is_value())
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

/// The kinds whose output `body` is a loop's, which Leave the loop ends.
const LOOPS: [&str; 3] = ["repeat", "while", "for_each"];

/// Each Leave the loop a flow reaches from where it starts (an event, a function's entry: a node without a flow input)
/// without entering a loop's body, the flow output `body`: it would leave no loop. One no flow reaches is no problem;
/// through a Sequence or a node that waits inside a body, it is still inside. A function's flow is its own: its loops
/// are none of its caller's.
fn leaves_outside_loops(graph: &Graph, known: &HashMap<&Id, Vec<NodePin>>, found: &mut Vec<Problem>) {
    let flow_in = |id: &Id| {
        let pins = known.get(id).map(Vec::as_slice).unwrap_or_default();
        pins.iter()
            .any(|pin| pin.side == Side::In && pin.carries == Carries::Exec)
    };
    let mut reached: Vec<&Id> = graph
        .nodes
        .iter()
        .map(|node| &node.id)
        .filter(|id| known.contains_key(id) && !flow_in(id))
        .collect();
    let mut seen: HashSet<&Id> = reached.iter().copied().collect();
    while let Some(at) = reached.pop() {
        // A loop's body only: a call's exit may be named `body` too.
        let looping = graph
            .nodes
            .iter()
            .any(|node| &node.id == at && matches!(&node.does, Does::Kind(kind) if LOOPS.contains(&kind.0.as_str())));
        for wire in graph
            .wires
            .iter()
            .filter(|wire| &wire.from.0 == at && !(looping && wire.from.1.0 == "body"))
        {
            let exec = known.get(at).is_some_and(|pins| {
                pins.iter()
                    .any(|pin| pin.id == wire.from.1.0 && pin.side == Side::Out && pin.carries == Carries::Exec)
            });
            if exec && seen.insert(&wire.to.0) {
                reached.push(&wire.to.0);
            }
        }
    }
    for node in &graph.nodes {
        if matches!(&node.does, Does::Kind(kind) if kind.0 == "leave_loop") && seen.contains(&node.id) {
            found.push(Problem::LeaveOutsideLoop {
                graph: graph.id.clone(),
                node: node.id.clone(),
            });
        }
    }
}

/// The problems of `node`'s options: one its kind does not take, a variable it lacks or does not name, a case of
/// another type than the node's or given twice. A wildcard's type missing is said with its wires.
fn of_options(blueprint: &Blueprint, of_graph: &Graph, node: &Node, found: &mut Vec<Problem>) {
    let graph = &of_graph.id;
    let kind = match &node.does {
        Does::Kind(id) => catalogue::kind(&id.0),
        _ => None,
    };
    if let (Some(kind), Some(noted)) = (kind, node.options.value_type)
        && !kind.types.is_empty()
        && !kind.types.contains(&noted)
    {
        let (graph, node) = (graph.clone(), node.id.clone());
        found.push(Problem::TypeNotTaken { graph, node });
    }
    let takes = |option: fn(&Takes) -> bool| kind.is_some_and(|kind| kind.takes.iter().any(option));
    let options = &node.options;
    let given = [
        ("type", options.value_type.is_some(), has_wildcards(node)),
        (
            "count",
            options.count.is_some(),
            takes(|it| matches!(it, Takes::Count { .. })),
        ),
        ("cases", !options.cases.is_empty(), takes(|it| *it == Takes::Cases)),
        ("text", options.text.is_some(), takes(|it| *it == Takes::Text)),
        (
            "expression",
            options.expression.is_some(),
            takes(|it| *it == Takes::Expression),
        ),
        (
            "variable",
            options.variable.is_some(),
            takes(|it| *it == Takes::Variable),
        ),
        ("event", options.event.is_some(), takes(|it| *it == Takes::Event)),
        ("exit", options.exit.is_some(), takes(|it| *it == Takes::Exit)),
    ];
    let at = || (graph.clone(), node.id.clone());
    for (option, present, taken) in given {
        let option = Id(option.to_owned());
        let (graph, node) = at();
        if present && !taken {
            found.push(Problem::NoSuchOption { graph, node, option });
        } else if !present && taken && matches!(option.0.as_str(), "variable" | "expression" | "event" | "exit") {
            found.push(Problem::OptionMissing { graph, node, option });
        }
    }
    if let Some(variable) = options.variable.as_ref().filter(|_| takes(|it| *it == Takes::Variable))
        && super::pins::variable_of(blueprint, of_graph, variable).is_none()
    {
        let (graph, node) = at();
        let variable = variable.clone();
        found.push(Problem::NoSuchVariable { graph, node, variable });
    }
    if let Some(event) = options.event.as_ref().filter(|_| takes(|it| *it == Takes::Event))
        && !blueprint.events.iter().any(|it| &it.id == event)
    {
        let (graph, node) = at();
        let event = event.clone();
        found.push(Problem::NoSuchEvent { graph, node, event });
    }
    let exits = of_graph.function.iter().flat_map(|signature| &signature.exits);
    if let Some(exit) = options.exit.as_ref().filter(|_| takes(|it| *it == Takes::Exit))
        && !exits.into_iter().any(|it| &it.id == exit)
    {
        let (graph, node) = at();
        let exit = exit.clone();
        found.push(Problem::NoSuchExit { graph, node, exit });
    }
    let Some(of) = options.value_type.filter(|_| takes(|it| *it == Takes::Cases)) else {
        return;
    };
    for (at_case, case) in options.cases.iter().enumerate() {
        let (graph, node) = at();
        let pin = Id(format!("case_{}", at_case + 1));
        if case.is_null() || !Type::new(of, Shape::ONE).holds(case) {
            found.push(Problem::CaseNotOfType { graph, node, pin });
        } else if options.cases[..at_case].contains(case) {
            found.push(Problem::CaseTwice { graph, node, pin });
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
        let wildcard = |carries| matches!(carries, Carries::Wildcard(_));
        if !wildcard(from.carries) && !wildcard(to.carries) && !from.carries.feeds(to.carries) {
            let shapes = match (from.carries, to.carries) {
                (Carries::Value(out), Carries::Value(input)) => out.shape != input.shape,
                _ => false,
            };
            let (graph, from, to) = (graph.id.clone(), wire.from.clone(), wire.to.clone());
            found.push(if shapes {
                Problem::WireOfAnotherShape { graph, from, to }
            } else {
                Problem::WireOfAnotherType { graph, from, to }
            });
        }
        if from.carries == Carries::Exec && !flows.insert(&wire.from) {
            let (graph, node, pin) = (graph.id.clone(), wire.from.0.clone(), wire.from.1.clone());
            found.push(Problem::OutputWiredTwice { graph, node, pin });
        }
        if to.carries.is_value() {
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
            // An event of mine is held once per event it names; one naming none is a problem of its own.
            let mine = (id.0 == "my_event").then_some(node.options.event.as_ref()).flatten();
            let held = mine.map_or(id, |event| event);
            if (once || mine.is_some()) && !seen.insert((id, held)) {
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
    fn a_list_into_a_pin_of_one_value_is_a_wire_of_another_shape() {
        let found = after(|blueprint| blueprint.variables[0].list = true);
        let (from, to) = (End(id("n2"), id("value")), End(id("n3"), id("zone")));
        assert_eq!(
            found,
            [Problem::WireOfAnotherShape {
                graph: id("events"),
                from,
                to
            }]
        );
    }

    #[test]
    fn ranges_typed_whose_rows_overlap_are_a_problem() {
        let found = after(|blueprint| {
            let signature = blueprint.graphs[1].function.as_mut();
            if let Some(signature) = signature {
                signature.inputs[0].ranges = true;
            }
            let row = |from, to| json!({"from": from, "to": to, "value": "nearest"});
            let inputs = &mut blueprint.graphs[0].nodes[3].inputs;
            inputs.insert(id("bank"), json!([row(1, 100), row(100, 200)]));
        });
        let (graph, node, pin) = at("n4", "bank");
        assert_eq!(found, [Problem::RangesOverlap { graph, node, pin }]);
    }

    /// The harvest with an `=`, noting the type `typed` when given.
    fn with_wildcards(typed: Option<&str>, edit: impl FnOnce(&mut Blueprint)) -> Vec<Problem> {
        after(|blueprint| {
            let options = typed
                .map(|it| format!(r#", "options": {{"type": "{it}"}}"#))
                .unwrap_or_default();
            let graph = &mut blueprint.graphs[0];
            graph
                .nodes
                .push(node(&format!(r#"{{"id": "eq", "kind": "test_equal"{options}}}"#)));
            edit(blueprint);
        })
    }

    #[test]
    fn a_wildcard_carries_the_type_its_node_notes_its_wires_and_values_checked_against_it() {
        let wired = |blueprint: &mut Blueprint| {
            let graph = &mut blueprint.graphs[0];
            graph.wires.push(wire("n2", "value", "eq", "a"));
            graph.nodes[5].inputs.insert(id("b"), json!({"sub_area": 445}));
        };
        assert_eq!(with_wildcards(Some("zone"), wired), []);
        let found = with_wildcards(Some("bank"), |blueprint| {
            wired(blueprint);
            blueprint.graphs[0].nodes[5].inputs.insert(id("b"), json!("nearest"));
        });
        let (from, to) = (End(id("n2"), id("value")), End(id("eq"), id("a")));
        let other = Problem::WireOfAnotherType {
            graph: id("events"),
            from,
            to,
        };
        assert_eq!(found, [other]);
    }

    #[test]
    fn a_wildcard_used_before_its_node_has_a_type_is_said_once_and_a_type_on_no_wildcard_is_no_option() {
        let found = with_wildcards(None, |blueprint| {
            let graph = &mut blueprint.graphs[0];
            graph.wires.push(wire("n2", "value", "eq", "a"));
            graph.nodes[5].inputs.insert(id("b"), json!({"sub_area": 445}));
            graph.nodes[2].options.value_type = Some(crate::bot::ValueType::Zone);
        });
        let (graph, node, option) = at("n3", "type");
        let no = Problem::NoSuchOption { graph, node, option };
        let untyped = Problem::WildcardWithoutType {
            graph: id("events"),
            node: id("eq"),
        };
        assert_eq!(found, [no, untyped]);
        // Neither wired nor given a value, a wildcard needs no type: its inputs need a value, as any input.
        let (graph, node, pin) = at("eq", "a");
        let without = Problem::InputWithoutValue { graph, node, pin };
        let (graph, node, pin) = at("eq", "b");
        let alone = with_wildcards(None, |_| {});
        assert_eq!(alone, [without, Problem::InputWithoutValue { graph, node, pin }]);
    }

    #[test]
    fn an_option_the_kind_does_not_take_or_lacks_is_a_problem() {
        let found = after(|blueprint| {
            let graph = &mut blueprint.graphs[0];
            graph.nodes[2].options.count = std::num::NonZeroU8::new(2);
            graph.nodes[2].options.event = Some(id("empty"));
            graph.nodes[3].options.exit = Some(id("full"));
            let more = [
                r#"{"id": "a1", "kind": "test_add_to", "options": {"type": "zone"}}"#,
                r#"{"id": "a2", "kind": "test_add_to", "options": {"variable": "nothing"}}"#,
                r#"{"id": "a3", "kind": "test_add_to", "options": {"variable": "harvests"}, "inputs": {"amount": 1}}"#,
                r#"{"id": "x", "kind": "test_expression"}"#,
                r#"{"id": "q", "kind": "level", "options": {"cases": [1], "text": "", "expression": "", "variable": "pods"}}"#,
            ];
            graph.nodes.extend(more.map(node));
        });
        let option = |node: &str, option: &str, missing: bool| {
            let (graph, node, option) = at(node, option);
            if missing {
                Problem::OptionMissing { graph, node, option }
            } else {
                Problem::NoSuchOption { graph, node, option }
            }
        };
        let (graph, node, variable) = at("a2", "nothing");
        let without = |node: &str| {
            let (graph, node, pin) = at(node, "amount");
            Problem::InputWithoutValue { graph, node, pin }
        };
        let expected = [
            option("n3", "count", false),
            option("n3", "event", false),
            option("n4", "exit", false),
            option("a1", "type", false),
            option("a1", "variable", true),
            Problem::NoSuchVariable { graph, node, variable },
            option("x", "expression", true),
            option("q", "cases", false),
            option("q", "text", false),
            option("q", "expression", false),
            option("q", "variable", false),
            without("a1"),
            without("a2"),
        ];
        assert_eq!(found, expected);
    }

    #[test]
    fn a_case_of_another_type_or_given_twice_is_a_problem() {
        let switch = |options: &str| {
            after(|blueprint| {
                let text =
                    format!(r#"{{"id": "sw", "kind": "switch", "options": {options}, "inputs": {{"value": 24}}}}"#);
                blueprint.graphs[0].nodes.push(node(&text));
            })
        };
        let (graph, node, pin) = at("sw", "case_2");
        let other = Problem::CaseNotOfType { graph, node, pin };
        let (graph, node, pin) = at("sw", "case_3");
        let twice = Problem::CaseTwice { graph, node, pin };
        assert_eq!(
            switch(r#"{"type": "job", "cases": [24, "bûcheron", 24, 28]}"#),
            [other, twice]
        );
        // Its cases are a use of its wildcard: without a type, that is said once.
        let untyped = Problem::WildcardWithoutType {
            graph: id("events"),
            node: id("sw"),
        };
        assert_eq!(switch(r#"{"cases": [24]}"#), [untyped]);
    }

    #[test]
    fn a_hole_takes_any_value_but_none() {
        let found = after(|blueprint| {
            let text = r#"{"id": "tx", "kind": "test_text", "options": {"text": "{count} in {zone} by {who}"}, "inputs": {"zone": 445, "who": "Bob"}}"#;
            blueprint.graphs[0].nodes.push(node(text));
            blueprint.graphs[0].wires.push(wire("n2", "value", "tx", "count"));
        });
        let (graph, node, pin) = at("tx", "zone");
        assert_eq!(found, [Problem::ValueNotOfType { graph, node, pin }]);
    }

    #[test]
    fn a_comparison_reads_numbers_until_it_notes_a_type_it_takes() {
        let compare = |options: &str| {
            after(|blueprint| {
                let text = format!(
                    r#"{{"id": "ge", "kind": "greater_or_equal", "options": {options}, "inputs": {{"b": 50}}}}"#
                );
                blueprint.graphs[0].nodes.push(node(&text));
                blueprint.graphs[0]
                    .nodes
                    .push(node(r#"{"id": "q", "kind": "life_percent"}"#));
                blueprint.graphs[0].wires.push(wire("q", "value", "ge", "a"));
            })
        };
        // A percentage feeds the number it reads by default, as "at least" did.
        assert_eq!(compare("{}"), []);
        assert_eq!(compare(r#"{"type": "percentage"}"#), []);
        let other = compare(r#"{"type": "zone"}"#);
        let not = Problem::TypeNotTaken {
            graph: id("events"),
            node: id("ge"),
        };
        assert_eq!(other.first(), Some(&not), "{other:?}");
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
    fn leave_the_loop_reached_outside_any_loops_body_is_a_problem_and_inside_one_none() {
        let found = after(|blueprint| {
            let events = &mut blueprint.graphs[0];
            events.nodes.extend(
                [
                    r#"{"id": "out", "kind": "leave_loop"}"#,
                    r#"{"id": "rep", "kind": "repeat"}"#,
                    r#"{"id": "seq", "kind": "sequence"}"#,
                    r#"{"id": "inside", "kind": "leave_loop"}"#,
                    r#"{"id": "after", "kind": "leave_loop"}"#,
                    r#"{"id": "alone", "kind": "leave_loop"}"#,
                ]
                .map(node),
            );
            events.wires.extend([
                wire("n3", "done", "out", "run"),
                wire("n3", "failed", "rep", "run"),
                wire("rep", "body", "seq", "run"),
                wire("seq", "then_1", "inside", "run"),
                wire("rep", "completed", "after", "run"),
            ]);
            let function = &mut blueprint.graphs[1];
            function.nodes.extend(
                [
                    r#"{"id": "in", "kind": "entry"}"#,
                    r#"{"id": "back", "kind": "leave_loop"}"#,
                ]
                .map(node),
            );
            function.wires.push(wire("in", "then", "back", "run"));
            // A call's exit named `body` is no loop's body.
            let exits = r#"{"exits": [{"id": "body", "name": "Body"}], "inputs": [{"id": "bank", "name": "Bank", "type": "bank"}]}"#;
            function.function = Some(serde_json::from_str(exits).unwrap_or_else(|error| panic!("{error}")));
            let events = &mut blueprint.graphs[0];
            events.nodes.push(node(r#"{"id": "exit", "kind": "leave_loop"}"#));
            events
                .wires
                .extend([wire("n5", "then", "n4", "run"), wire("n4", "body", "exit", "run")]);
        });
        let outside = |graph: &str, node: &str| Problem::LeaveOutsideLoop {
            graph: id(graph),
            node: id(node),
        };
        assert_eq!(
            found,
            [
                outside("events", "out"),
                outside("events", "after"),
                outside("events", "exit"),
                outside("empty_at_bank", "back")
            ]
        );
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
