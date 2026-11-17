//! A blueprint's problems: what makes it unable to run, each where the editor can show it (`docs/bot.md`, "In the
//! app": the toolbar lists them). A blueprint with problems still opens, so that nothing of it is lost; it cannot run.
//! Those of its nodes against the catalogue and their pins are `wiring`'s.

use std::collections::HashSet;

use super::blueprint::{Blueprint, Does, End, Graph, Id};
use super::catalogue::RESERVED;
use super::variable::Variable;

/// What keeps a blueprint from running.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Problem {
    /// An id that is not 1 to 64 lowercase letters, digits, `_` or `-`.
    MalformedId { id: Id },
    /// Two variables with the same id.
    VariableTwice { variable: Id },
    /// Two graphs with the same id.
    GraphTwice { graph: Id },
    /// Two nodes of a graph with the same id.
    NodeTwice { graph: Id, node: Id },
    /// Two inputs, outputs, exits or locals of a function with the same id, or two inputs of an event of mine (its id
    /// as `graph`); one with the id of a pin its entry, returns and calls have (`run`, `then`, `done`, `failed`,
    /// `reason`); an exit with an output's id; a local with a variable's.
    ParameterTwice { graph: Id, parameter: Id },
    /// A variable's default that its type does not hold.
    DefaultNotOfType { variable: Id },
    /// A variable's default of ranges two of whose rows a level reaches at once.
    DefaultRangesOverlap { variable: Id },
    /// Bounds on a type that is no single number, or whose smallest is above its largest, or a percentage's outside 0
    /// to 100.
    WrongBounds { variable: Id },
    /// A variable's default outside its bounds.
    DefaultOutOfBounds { variable: Id },
    /// A node that reads or writes a variable the blueprint does not have.
    NoSuchVariable { graph: Id, node: Id, variable: Id },
    /// A node that calls a function the blueprint does not have, or a graph that is no function.
    NoSuchFunction { graph: Id, node: Id, function: Id },
    /// A wire from or to a node its graph does not have.
    WireToNoNode { graph: Id, node: Id },
    /// The same wire twice.
    WireTwice { graph: Id, from: Id, to: Id },
    /// A node of a kind this version does not know: made by a newer D3B, or removed. It is kept as it is.
    UnknownKind { graph: Id, node: Id, kind: Id },
    /// An event in a function, or a function's entry or return in an event graph.
    OutOfPlace { graph: Id, node: Id },
    /// A priority and a mode on a node that takes none.
    EventSettingsOnNoEvent { graph: Id, node: Id },
    /// A value typed on a pin that is no value input of the node.
    NoSuchInput { graph: Id, node: Id, pin: Id },
    /// A value typed that its pin's type does not hold.
    ValueNotOfType { graph: Id, node: Id, pin: Id },
    /// Ranges typed two of whose rows a level reaches at once.
    RangesOverlap { graph: Id, node: Id, pin: Id },
    /// An option the node does not take: a type on a node without wildcards, cases on a node that is no Switch…
    NoSuchOption { graph: Id, node: Id, option: Id },
    /// An option the node's kind needs and it lacks: the variable it changes, an Expression's formula.
    OptionMissing { graph: Id, node: Id, option: Id },
    /// A Switch's case that is no value of its type.
    CaseNotOfType { graph: Id, node: Id, pin: Id },
    /// A Switch's case equal to one before it.
    CaseTwice { graph: Id, node: Id, pin: Id },
    /// A type noted on a node whose kind takes others only: `≥` of zones.
    TypeNotTaken { graph: Id, node: Id },
    /// A wildcard wired or given a value while its node notes no type.
    WildcardWithoutType { graph: Id, node: Id },
    /// A wire from or to a pin its node does not have.
    WireToNoPin { graph: Id, node: Id, pin: Id },
    /// A wire from an input or to an output.
    WireTheWrongWay { graph: Id, node: Id, pin: Id },
    /// A wire between the flow and a value, or between values of two types.
    WireOfAnotherType { graph: Id, from: End, to: End },
    /// A wire between a list or ranges and a pin that takes another shape: one value, a list, ranges.
    WireOfAnotherShape { graph: Id, from: End, to: End },
    /// A flow output wired twice: the flow goes one way.
    OutputWiredTwice { graph: Id, node: Id, pin: Id },
    /// A value input fed by two wires, or by a wire and a value typed.
    InputFedTwice { graph: Id, node: Id, pin: Id },
    /// A value input that no wire feeds, with no value typed and no default.
    InputWithoutValue { graph: Id, node: Id, pin: Id },
    /// An event a blueprint holds once at most (on start, a forced one, each event of mine) held again.
    EventTwice { graph: Id, node: Id },
    /// Two events of mine with the same id.
    OwnEventTwice { event: Id },
    /// A node naming an event of mine the blueprint does not have.
    NoSuchEvent { graph: Id, node: Id, event: Id },
    /// A return by an exit its function does not have.
    NoSuchExit { graph: Id, node: Id, exit: Id },
    /// A node with a flow pin in a pure function: an action, a wait, a Set, a return, a call of a function not pure.
    NotPure { graph: Id, node: Id },
    /// A Leave the loop a flow reaches outside any loop's body.
    LeaveOutsideLoop { graph: Id, node: Id },
    /// A 0 typed as a ÷'s or a remainder's divisor: it would block the character.
    ZeroDivisor { graph: Id, node: Id, pin: Id },
}

/// Every problem of `blueprint`: its own in the order of its file, then each graph's nodes and wires, then the events
/// held twice.
pub(crate) fn problems(blueprint: &Blueprint) -> Vec<Problem> {
    let mut found = Vec::new();
    well_formed(&blueprint.id, &mut found);
    let mut variables = HashSet::new();
    for variable in &blueprint.variables {
        well_formed(&variable.id, &mut found);
        if !variables.insert(&variable.id) {
            found.push(Problem::VariableTwice {
                variable: variable.id.clone(),
            });
        }
        found.extend(of_variable(variable));
    }
    let mut events = HashSet::new();
    for event in &blueprint.events {
        well_formed(&event.id, &mut found);
        if !events.insert(&event.id) {
            found.push(Problem::OwnEventTwice {
                event: event.id.clone(),
            });
        }
        // Its inputs are its node's outputs, beside its `then` only.
        parameters(&event.id, [ids(&event.inputs)], &[], &["then"], &mut found);
    }
    let mut graphs = HashSet::new();
    for graph in &blueprint.graphs {
        well_formed(&graph.id, &mut found);
        if !graphs.insert(&graph.id) {
            found.push(Problem::GraphTwice {
                graph: graph.id.clone(),
            });
        }
    }
    for graph in &blueprint.graphs {
        of_graph(blueprint, graph, &mut found);
        super::wiring::of_graph(blueprint, graph, &mut found);
    }
    super::wiring::once_twice(blueprint, &mut found);
    found
}

/// The ids of `parameters`.
fn ids(parameters: &[super::blueprint::Parameter]) -> Vec<&Id> {
    parameters.iter().map(|it| &it.id).collect()
}

/// The problems of each list of ids in `lists`: malformed, twice in its list, a pin's that it would clash with
/// (`reserved`), or among `taken`.
fn parameters<'a>(
    graph: &Id,
    lists: impl IntoIterator<Item = impl IntoIterator<Item = &'a Id>>,
    taken: &[&Id],
    reserved: &[&str],
    found: &mut Vec<Problem>,
) {
    for list in lists {
        let mut seen = HashSet::new();
        for id in list {
            well_formed(id, found);
            if !seen.insert(id) || reserved.contains(&id.0.as_str()) || taken.contains(&id) {
                found.push(Problem::ParameterTwice {
                    graph: graph.clone(),
                    parameter: id.clone(),
                });
            }
        }
    }
}

fn well_formed(id: &Id, found: &mut Vec<Problem>) {
    if !id.is_well_formed() {
        found.push(Problem::MalformedId { id: id.clone() });
    }
}

fn of_variable(variable: &Variable) -> Option<Problem> {
    let id = variable.id.clone();
    let typed = variable.of_type();
    if !typed.holds(&variable.default) {
        return Some(Problem::DefaultNotOfType { variable: id });
    }
    if typed.overlaps(&variable.default) {
        return Some(Problem::DefaultRangesOverlap { variable: id });
    }
    let [smallest, largest] = variable.bounds?;
    let percentage = variable.value_type == super::variable::ValueType::Percentage;
    let numeric = variable.value_type.is_numeric() && typed.shape == super::variable::Shape::ONE;
    if !numeric || smallest > largest || percentage && (smallest < 0 || largest > 100) {
        return Some(Problem::WrongBounds { variable: id });
    }
    let outside = variable
        .default
        .as_i64()
        .is_some_and(|default| !(smallest..=largest).contains(&default));
    outside.then_some(Problem::DefaultOutOfBounds { variable: id })
}

fn of_graph(blueprint: &Blueprint, graph: &Graph, found: &mut Vec<Problem>) {
    let at = || graph.id.clone();
    if let Some(signature) = &graph.function {
        parameters(
            &graph.id,
            [ids(&signature.inputs), ids(&signature.outputs)],
            &[],
            &RESERVED,
            found,
        );
        // An exit is a flow output of its calls, beside their outputs.
        let exits: Vec<&Id> = signature.exits.iter().map(|it| &it.id).collect();
        parameters(&graph.id, [exits], &ids(&signature.outputs), &RESERVED, found);
        let variables: Vec<&Id> = blueprint.variables.iter().map(|it| &it.id).collect();
        let locals: Vec<&Id> = signature.locals.iter().map(|it| &it.id).collect();
        // A local is no pin: only a variable's id would make a Get mean two things.
        parameters(&graph.id, [locals], &variables, &[], found);
        found.extend(signature.locals.iter().filter_map(of_variable));
    }
    let mut nodes = HashSet::new();
    for node in &graph.nodes {
        well_formed(&node.id, found);
        if !nodes.insert(&node.id) {
            found.push(Problem::NodeTwice {
                graph: at(),
                node: node.id.clone(),
            });
        }
        match &node.does {
            Does::Get(variable) | Does::Set(variable)
                if super::pins::variable_of(blueprint, graph, variable).is_none() =>
            {
                found.push(Problem::NoSuchVariable {
                    graph: at(),
                    node: node.id.clone(),
                    variable: variable.clone(),
                });
            }
            Does::Call(function)
                if !blueprint
                    .graphs
                    .iter()
                    .any(|known| &known.id == function && known.function.is_some()) =>
            {
                found.push(Problem::NoSuchFunction {
                    graph: at(),
                    node: node.id.clone(),
                    function: function.clone(),
                });
            }
            _ => {}
        }
    }
    let mut wires = HashSet::new();
    for wire in &graph.wires {
        for end in [&wire.from, &wire.to] {
            if !nodes.contains(&end.0) {
                found.push(Problem::WireToNoNode {
                    graph: at(),
                    node: end.0.clone(),
                });
            }
        }
        if !wires.insert((&wire.from, &wire.to)) {
            found.push(Problem::WireTwice {
                graph: at(),
                from: wire.from.0.clone(),
                to: wire.to.0.clone(),
            });
        }
    }
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;
    use crate::bot::blueprint::{End, Wire};
    use crate::bot::json::{read, tests::HARVEST};

    fn id(text: &str) -> Id {
        Id(text.to_owned())
    }

    fn harvest() -> Blueprint {
        read(HARVEST).unwrap_or_else(|error| panic!("{error}"))
    }

    fn found_in(blueprint: &Blueprint, expected: impl IntoIterator<Item = Problem>) {
        let found = problems(blueprint);
        for problem in expected {
            assert!(found.contains(&problem), "{problem:?} not in {found:?}");
        }
    }

    #[test]
    fn a_whole_blueprint_has_no_problem() {
        assert_eq!(problems(&harvest()), []);
    }

    #[test]
    fn a_malformed_id_is_a_problem_wherever_it_is() {
        let mut blueprint = harvest();
        blueprint.id = id("Automatic harvest");
        blueprint.variables[0].id = id("Zone");
        blueprint.graphs[1].id = id("bank!");
        blueprint.graphs[0].nodes[0].id = id("N1");
        let bad = ["Automatic harvest", "Zone", "bank!", "N1"];
        found_in(&blueprint, bad.map(|bad| Problem::MalformedId { id: id(bad) }));
    }

    #[test]
    fn an_id_given_twice_is_a_problem() {
        let mut blueprint = harvest();
        blueprint.variables[1].id = id("zone");
        blueprint.graphs[1].id = id("events");
        blueprint.graphs[0].nodes[1].id = id("n1");
        let node = Problem::NodeTwice {
            graph: id("events"),
            node: id("n1"),
        };
        let graph = Problem::GraphTwice { graph: id("events") };
        found_in(
            &blueprint,
            [Problem::VariableTwice { variable: id("zone") }, graph, node],
        );
    }

    /// Events of mine, a pure function with exits and locals, and another function, each with problems.
    const MINE: &str = r#"{"schema": 1, "id": "b", "name": "B", "version": 1,
      "variables": [{"id": "n", "name": "N", "type": "number", "default": 0}],
      "events": [{"id": "empty", "name": "Empty", "inputs": [{"id": "zone", "name": "Zone", "type": "zone"},
                                                             {"id": "zone", "name": "Zone", "type": "zone"},
                                                             {"id": "failed", "name": "Failed", "type": "bool"},
                                                             {"id": "then", "name": "Then", "type": "bool"}]},
                 {"id": "empty", "name": "Empty again"}, {"id": "full", "name": "Full"}],
      "graphs": [
        {"id": "events", "name": "Events", "nodes": [
          {"id": "e1", "kind": "my_event", "options": {"event": "empty"}},
          {"id": "e2", "kind": "my_event", "options": {"event": "empty"}},
          {"id": "e3", "kind": "my_event", "options": {"event": "gone"}}, {"id": "e4", "kind": "my_event"},
          {"id": "e5", "kind": "my_event", "options": {"event": "full"}},
          {"id": "g", "get": "tries"}]},
        {"id": "f", "name": "F", "function": {"outputs": [{"id": "y", "name": "Y", "type": "number"}],
          "exits": [{"id": "full", "name": "Full"}, {"id": "full", "name": "Full"}, {"id": "done", "name": "Done"},
                    {"id": "y", "name": "Y"}],
          "locals": [{"id": "tries", "name": "Tries", "type": "number", "default": "x"},
                     {"id": "n", "name": "N", "type": "number"}, {"id": "done", "name": "Done", "type": "bool"}],
          "pure": true},
         "nodes": [{"id": "in", "kind": "entry"}, {"id": "r", "kind": "result", "inputs": {"y": 1}},
                   {"id": "say", "kind": "notify", "inputs": {"text": ""}},
                   {"id": "x1", "kind": "return_exit", "options": {"exit": "gone"}}, {"id": "t", "get": "tries"}]},
        {"id": "f2", "name": "F2", "function": {}, "nodes": [{"id": "r2", "kind": "result"}, {"id": "x2", "kind": "return_exit"}]}
      ]}"#;

    #[test]
    fn events_of_mine_exits_locals_and_pure_functions_have_their_problems() {
        let blueprint = read(MINE).unwrap_or_else(|error| panic!("{error}"));
        let twice = |graph: &str, parameter: &str| Problem::ParameterTwice {
            graph: id(graph),
            parameter: id(parameter),
        };
        let on = |graph: &str, node: &str| (id(graph), id(node));
        let ((g1, e2), (g2, e3), (g3, e4), (g4, g), (g5, say), (g6, x1), (g7, r2), (g8, x2)) = (
            on("events", "e2"),
            on("events", "e3"),
            on("events", "e4"),
            on("events", "g"),
            on("f", "say"),
            on("f", "x1"),
            on("f2", "r2"),
            on("f2", "x2"),
        );
        let expected = [
            twice("empty", "zone"),
            Problem::OwnEventTwice { event: id("empty") },
            twice("f", "full"),
            twice("f", "done"),
            twice("f", "y"),
            twice("f", "n"),
            Problem::DefaultNotOfType { variable: id("tries") },
            Problem::EventTwice { graph: g1, node: e2 },
            Problem::NoSuchEvent {
                graph: g2,
                node: e3,
                event: id("gone"),
            },
            Problem::OptionMissing {
                graph: g3,
                node: e4,
                option: id("event"),
            },
            Problem::NoSuchVariable {
                graph: g4,
                node: g,
                variable: id("tries"),
            },
            Problem::NotPure { graph: g5, node: say },
            Problem::NotPure {
                graph: g6.clone(),
                node: x1.clone(),
            },
            Problem::NoSuchExit {
                graph: g6,
                node: x1,
                exit: id("gone"),
            },
            Problem::OutOfPlace { graph: g7, node: r2 },
            Problem::OptionMissing {
                graph: g8,
                node: x2,
                option: id("exit"),
            },
        ];
        found_in(&blueprint, expected);
        let found = problems(&blueprint);
        let not = [
            twice("empty", "failed"),
            twice("f", "done"),
            Problem::EventTwice {
                graph: id("events"),
                node: id("e3"),
            },
            Problem::EventTwice {
                graph: id("events"),
                node: id("e5"),
            },
        ];
        // An event's input clashes with its node's `then` only, a local with no pin; each event is held once.
        assert_eq!(
            found.iter().filter(|it| not[1..].contains(it)).count(),
            1,
            "the exit `done`, not the local"
        );
        assert!(
            !found.contains(&not[0]) && !found.contains(&not[2]) && !found.contains(&not[3]),
            "{found:?}"
        );
        assert!(found.contains(&twice("empty", "then")));
        let (graph, node) = on("f", "in");
        assert!(
            !found.contains(&Problem::NotPure { graph, node }),
            "a pure function's entry has no flow"
        );
        let (graph, node) = on("f", "t");
        let variable = id("tries");
        assert!(
            !found.contains(&Problem::NoSuchVariable { graph, node, variable }),
            "its own local"
        );
    }

    #[test]
    fn a_function_parameter_given_twice_is_a_problem() {
        let mut blueprint = harvest();
        let bank = |blueprint: &mut Blueprint| blueprint.graphs[1].function.take().unwrap_or_else(|| panic!("a bank"));
        let mut signature = bank(&mut blueprint);
        signature.inputs.push(signature.inputs[0].clone());
        blueprint.graphs[1].function = Some(signature);
        let twice = Problem::ParameterTwice {
            graph: id("empty_at_bank"),
            parameter: id("bank"),
        };
        assert_eq!(problems(&blueprint), [twice]);
        // An output may share an input's id: they are read on either side of the call.
        let mut signature = bank(&mut blueprint);
        signature.outputs.extend(signature.inputs.pop());
        blueprint.graphs[1].function = Some(signature);
        assert_eq!(problems(&blueprint), []);
        // Nor the id of a pin the function's entry, returns and calls have.
        let mut signature = bank(&mut blueprint);
        signature.outputs[0].id = id("reason");
        blueprint.graphs[1].function = Some(signature);
        let reserved = Problem::ParameterTwice {
            graph: id("empty_at_bank"),
            parameter: id("reason"),
        };
        assert_eq!(problems(&blueprint), [reserved]);
    }

    #[test]
    fn a_default_its_type_does_not_hold_is_a_problem() {
        let mut blueprint = harvest();
        blueprint.variables[1].default = json!("ninety");
        let problem = Problem::DefaultNotOfType { variable: id("pods") };
        assert_eq!(problems(&blueprint), [problem]);
    }

    #[test]
    fn a_default_list_or_ranges_its_type_does_not_hold_or_whose_rows_overlap_is_a_problem() {
        let mut blueprint = harvest();
        let harvests = &mut blueprint.variables[2];
        harvests.list = true;
        let not = Problem::DefaultNotOfType {
            variable: id("harvests"),
        };
        assert_eq!(problems(&blueprint), [not]);
        blueprint.variables[2].default = json!([0, 5000]);
        assert_eq!(problems(&blueprint), []);
        blueprint.variables[2].ranges = true;
        let row = |from, to| json!({"from": from, "to": to, "value": [5000]});
        blueprint.variables[2].default = json!([row(1, 50), row(50, 200)]);
        let overlap = Problem::DefaultRangesOverlap {
            variable: id("harvests"),
        };
        assert_eq!(problems(&blueprint), [overlap]);
        blueprint.variables[2].default = json!([row(1, 49), row(50, 200)]);
        assert_eq!(problems(&blueprint), []);
    }

    #[test]
    fn bounds_on_a_list_of_numbers_are_wrong() {
        let mut blueprint = harvest();
        blueprint.variables[1].list = true;
        blueprint.variables[1].default = json!([90]);
        assert_eq!(problems(&blueprint), [Problem::WrongBounds { variable: id("pods") }]);
    }

    #[test]
    fn wrong_bounds_are_a_problem() {
        for (variable, bounds) in [(0, [0, 10]), (1, [50, 40]), (1, [-1, 100]), (1, [10, 101])] {
            let mut blueprint = harvest();
            blueprint.variables[variable].bounds = Some(bounds);
            let problem = Problem::WrongBounds {
                variable: blueprint.variables[variable].id.clone(),
            };
            assert_eq!(problems(&blueprint), [problem], "{bounds:?}");
        }
    }

    #[test]
    fn a_default_out_of_its_bounds_is_a_problem_at_either_end_and_the_bounds_are_included() {
        let out = || vec![Problem::DefaultOutOfBounds { variable: id("pods") }];
        let cases = [
            (9, [10, 100], out()),
            (100, [10, 99], out()),
            (10, [10, 100], vec![]),
            (100, [10, 100], vec![]),
        ];
        for (default, bounds, expected) in cases.into_iter().chain([(50, [50, 50], vec![]), (0, [0, 100], vec![])]) {
            let mut blueprint = harvest();
            blueprint.variables[1].default = json!(default);
            blueprint.variables[1].bounds = Some(bounds);
            assert_eq!(problems(&blueprint), expected, "{default} in {bounds:?}");
        }
    }

    #[test]
    fn a_node_on_a_variable_the_blueprint_lacks_is_a_problem() {
        let mut blueprint = harvest();
        blueprint.graphs[0].nodes[1].does = Does::Set(id("bank"));
        let problem = Problem::NoSuchVariable {
            graph: id("events"),
            node: id("n2"),
            variable: id("bank"),
        };
        assert_eq!(problems(&blueprint), [problem]);
    }

    #[test]
    fn a_call_to_no_function_is_a_problem() {
        for callee in ["sit_down", "events"] {
            let mut blueprint = harvest();
            blueprint.graphs[0].nodes[3].does = Does::Call(id(callee));
            let problem = Problem::NoSuchFunction {
                graph: id("events"),
                node: id("n4"),
                function: id(callee),
            };
            assert_eq!(problems(&blueprint), [problem], "{callee}");
        }
    }

    #[test]
    fn a_wire_to_no_node_is_a_problem_at_either_end() {
        let mut blueprint = harvest();
        let wire = |from: &str, to: &str| Wire {
            from: End(id(from), id("then")),
            to: End(id(to), id("run")),
        };
        blueprint.graphs[0].wires.extend([wire("n9", "n3"), wire("n1", "n8")]);
        let at = |node: &str| Problem::WireToNoNode {
            graph: id("events"),
            node: id(node),
        };
        assert_eq!(problems(&blueprint), [at("n9"), at("n8")]);
    }

    #[test]
    fn the_same_wire_twice_is_a_problem() {
        let mut blueprint = harvest();
        let first = blueprint.graphs[0].wires[0].clone();
        blueprint.graphs[0].wires.push(first);
        let problem = Problem::WireTwice {
            graph: id("events"),
            from: id("n1"),
            to: id("n3"),
        };
        assert_eq!(problems(&blueprint), [problem]);
    }
}
