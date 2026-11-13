//! A blueprint's problems: what makes it unable to run, each where the editor can show it (`docs/bot.md`, "In the
//! app": the toolbar lists them). A blueprint with problems still opens, so that nothing of it is lost; it cannot run.
//! These are the problems of the blueprint itself; the catalogue adds those of its nodes' kinds, pins and types.

use std::collections::HashSet;

use super::blueprint::{Blueprint, Does, Graph, Id};
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
    /// Two inputs, or two outputs, of a function with the same id.
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
    }
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
                if !parameters.insert(&parameter.id) {
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
        let signature = blueprint.graphs[1]
            .function
            .as_mut()
            .unwrap_or_else(|| panic!("a function"));
        signature.inputs.push(signature.inputs[0].clone());
        let twice = Problem::ParameterTwice {
            graph: id("empty_at_bank"),
            parameter: id("bank"),
        };
        assert_eq!(problems(&blueprint), [twice]);
        // An output may share an input's id: they are read on either side of the call.
        let signature = blueprint.graphs[1]
            .function
            .as_mut()
            .unwrap_or_else(|| panic!("a function"));
        let input = signature.inputs.pop().unwrap_or_else(|| panic!("an input"));
        signature.outputs.push(input);
        assert_eq!(problems(&blueprint), []);
    }

    #[test]
    fn a_default_its_type_does_not_hold_is_a_problem() {
        let mut blueprint = harvest();
        blueprint.variables[1].default = json!("ninety");
        assert_eq!(
            problems(&blueprint),
            [Problem::DefaultNotOfType { variable: id("pods") }]
        );
    }

    #[test]
    fn wrong_bounds_are_a_problem() {
        for (variable, bounds) in [(0, [0, 10]), (1, [50, 40]), (1, [-1, 100]), (1, [10, 101])] {
            let mut blueprint = harvest();
            blueprint.variables[variable].bounds = Some(bounds);
            let id = blueprint.variables[variable].id.clone();
            assert_eq!(
                problems(&blueprint),
                [Problem::WrongBounds { variable: id }],
                "{bounds:?}"
            );
        }
    }

    #[test]
    fn a_default_out_of_its_bounds_is_a_problem_at_either_end() {
        for (default, bounds) in [(9, [10, 100]), (100, [10, 99])] {
            let mut blueprint = harvest();
            blueprint.variables[1].default = json!(default);
            blueprint.variables[1].bounds = Some(bounds);
            assert_eq!(
                problems(&blueprint),
                [Problem::DefaultOutOfBounds { variable: id("pods") }],
                "{default}"
            );
        }
        for (default, bounds) in [(10, [10, 100]), (100, [10, 100]), (50, [50, 50]), (0, [0, 100])] {
            let mut blueprint = harvest();
            blueprint.variables[1].default = json!(default);
            blueprint.variables[1].bounds = Some(bounds);
            assert_eq!(
                problems(&blueprint),
                [],
                "the bounds are included: {default} in {bounds:?}"
            );
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
        let found = problems(&blueprint);
        let at = |node: &str| Problem::WireToNoNode {
            graph: id("events"),
            node: id(node),
        };
        assert_eq!(found, [at("n9"), at("n8")]);
    }

    #[test]
    fn the_same_wire_twice_is_a_problem() {
        let mut blueprint = harvest();
        let first = blueprint.graphs[0].wires[0].clone();
        blueprint.graphs[0].wires.push(first);
        assert_eq!(
            problems(&blueprint),
            [Problem::WireTwice {
                graph: id("events"),
                from: id("n1"),
                to: id("n3")
            }]
        );
    }
}
