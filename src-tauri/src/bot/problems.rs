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
    /// Two inputs, or two outputs, of a function with the same id, or one with the id of a pin its entry, returns and
    /// calls have (`run`, `then`, `done`, `failed`, `reason`).
    ParameterTwice { graph: Id, parameter: Id },
    /// A variable's default that its type does not hold.
    DefaultNotOfType { variable: Id },
    /// Bounds on a type that is no number, or whose smallest is above its largest, or a percentage's outside 0 to 100.
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
    /// A wire from or to a pin its node does not have.
    WireToNoPin { graph: Id, node: Id, pin: Id },
    /// A wire from an input or to an output.
    WireTheWrongWay { graph: Id, node: Id, pin: Id },
    /// A wire between the flow and a value, or between values of two types.
    WireOfAnotherType { graph: Id, from: End, to: End },
    /// A flow output wired twice: the flow goes one way.
    OutputWiredTwice { graph: Id, node: Id, pin: Id },
    /// A value input fed by two wires, or by a wire and a value typed.
    InputFedTwice { graph: Id, node: Id, pin: Id },
    /// A value input that no wire feeds, with no value typed and no default.
    InputWithoutValue { graph: Id, node: Id, pin: Id },
    /// An event a blueprint holds once at most (on start, a forced one) held again.
    EventTwice { graph: Id, node: Id },
}

/// Every problem of `blueprint`, in the order of its file.
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

fn well_formed(id: &Id, found: &mut Vec<Problem>) {
    if !id.is_well_formed() {
        found.push(Problem::MalformedId { id: id.clone() });
    }
}

fn of_variable(variable: &Variable) -> Option<Problem> {
    let id = variable.id.clone();
    if !variable.value_type.holds(&variable.default) {
        return Some(Problem::DefaultNotOfType { variable: id });
    }
    let [smallest, largest] = variable.bounds?;
    let percentage = variable.value_type == super::variable::ValueType::Percentage;
    if !variable.value_type.is_numeric() || smallest > largest || percentage && (smallest < 0 || largest > 100) {
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
        for side in [&signature.inputs, &signature.outputs] {
            let mut parameters = HashSet::new();
            for parameter in side {
                well_formed(&parameter.id, found);
                if !parameters.insert(&parameter.id) || RESERVED.contains(&parameter.id.0.as_str()) {
                    found.push(Problem::ParameterTwice {
                        graph: at(),
                        parameter: parameter.id.clone(),
                    });
                }
            }
        }
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
                if !blueprint.variables.iter().any(|known| &known.id == variable) =>
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
        let reserved = Problem::ParameterTwice { graph: id("empty_at_bank"), parameter: id("reason") };
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
