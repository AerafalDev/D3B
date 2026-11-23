//! A blueprint compiled before it runs (ADR 0017): each node's behaviour, each flow output to the node and pin it leads
//! to, each value input to its wire, its typed value or its pin's default, by index. The file's schema stops here, so
//! a new one changes this compilation and not the engine.

use std::collections::HashMap;

use serde_json::Value;

use super::behaviour::{Behaviour, Starts};
use super::kinds;
use crate::bot::{Blueprint, Carries, Does, Graph, Id, Node, NodePin, Problem, Side, Type, Variable, pins, problems};

/// A blueprint ready to run.
pub(crate) struct Program {
    pub(super) graphs: Vec<Code>,
    /// The main flow's start: its graph and node.
    pub(super) main: (usize, usize),
}

/// A graph compiled.
pub(super) struct Code {
    pub(super) id: Id,
    pub(super) nodes: Vec<NodeCode>,
    /// A function's entry, where its calls start.
    pub(super) entry: Option<usize>,
    /// A function's locals, each from its default at a call.
    pub(super) locals: Vec<Variable>,
}

/// A node compiled.
pub(super) struct NodeCode {
    pub(super) id: Id,
    pub(super) doing: Doing,
    /// Each flow output wired, to the node and the flow input it leads to.
    pub(super) flows: Vec<(String, (usize, String))>,
    /// Each value input, in its pins' order.
    pub(super) inputs: Vec<(String, Source)>,
    /// Without flow: its outputs are computed when read.
    pub(super) pure: bool,
    /// The exit it returns by.
    pub(super) named: Option<String>,
    /// Its flow outputs, in its pins' order: Sequence's `then_1` to `then_n`.
    pub(super) outs: Vec<String>,
    /// Switch's cases, each the value of its output `case_<n>`.
    pub(super) cases: Vec<Value>,
    /// The type of each value input: its wire's source pin's, else its own; none for a hole typed, which is text.
    pub(super) types: HashMap<String, Type>,
    /// A Text's template, its `{holes}` its inputs.
    pub(super) text: Option<String>,
}

/// What a node does: a kind's behaviour, or the engine's own Get, Set and Call.
pub(super) enum Doing {
    Kind(&'static dyn Behaviour),
    Get(String),
    Set(String),
    Call(usize),
}

/// Where a value input's value comes from.
#[derive(Debug, Clone, PartialEq)]
pub(super) enum Source {
    /// An output of a node, by node and pin.
    Wire(usize, String),
    Value(Value),
    Missing,
}

/// Why a blueprint cannot run.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum Refused {
    #[error("{} problems", .0.len())]
    Problems(Vec<Problem>),
    #[error("no On start")]
    NoStart,
    #[error("the kind {kind} cannot run in this version")]
    NotRunnable { kind: String },
    #[error("the variable {variable} has no value")]
    Unset { variable: String },
}

impl Program {
    /// Where the flow out of `pin` of `node` leads.
    pub(super) fn next(&self, graph: usize, node: usize, pin: &str) -> Option<(usize, &str)> {
        let flows = &self.graphs[graph].nodes[node].flows;
        flows
            .iter()
            .find(|(out, _)| out == pin)
            .map(|(_, (to, input))| (*to, input.as_str()))
    }
}

/// What a node doing `does` is at run time, refused for a kind that cannot run yet.
fn doing(blueprint: &Blueprint, functions: &HashMap<&Id, usize>, does: &Does) -> Result<Doing, Refused> {
    Ok(match does {
        Does::Kind(kind) => {
            Doing::Kind(kinds::behaviour(&kind.0).ok_or_else(|| Refused::NotRunnable { kind: kind.0.clone() })?)
        }
        Does::Get(variable) => Doing::Get(variable.0.clone()),
        Does::Set(variable) => Doing::Set(variable.0.clone()),
        Does::Call(function) => {
            let at = functions.get(function).copied().unwrap_or_default();
            // The engine reads no pure call yet: its result cannot run.
            if blueprint.graphs[at]
                .function
                .as_ref()
                .is_some_and(|signature| signature.pure)
            {
                return Err(Refused::NotRunnable {
                    kind: "result".to_owned(),
                });
            }
            Doing::Call(at)
        }
    })
}

/// The type each value input of `node` carries: the one its wire brings, from its source's pin, else its own pin's.
fn input_types(
    graph: &Graph,
    node: &Node,
    its: &[NodePin],
    all: &[Vec<NodePin>],
    index: &HashMap<&Id, usize>,
) -> HashMap<String, Type> {
    let typed = |carries: Carries| match carries {
        Carries::Value(typed) => Some(typed),
        _ => None,
    };
    let inputs = its.iter().filter(|pin| pin.side == Side::In && pin.carries.is_value());
    inputs
        .filter_map(|pin| {
            let wire = graph
                .wires
                .iter()
                .find(|wire| wire.to.0 == node.id && wire.to.1.0 == pin.id);
            let brought = wire.and_then(|wire| {
                let source = all.get(*index.get(&wire.from.0)?)?;
                let out = source
                    .iter()
                    .find(|it| it.side == Side::Out && it.id == wire.from.1.0)?;
                typed(out.carries)
            });
            brought.or_else(|| typed(pin.carries)).map(|of| (pin.id.clone(), of))
        })
        .collect()
}

/// A node's flow outputs, in its pins' order.
fn flow_outputs(pins: &[NodePin]) -> Vec<String> {
    let outs = pins
        .iter()
        .filter(|pin| pin.side == Side::Out && pin.carries == Carries::Exec);
    outs.map(|pin| pin.id.clone()).collect()
}

/// `blueprint` compiled, refused when it has a problem or holds a kind that cannot run yet.
pub(crate) fn compile(blueprint: &Blueprint) -> Result<Program, Refused> {
    let found = problems(blueprint);
    if !found.is_empty() {
        return Err(Refused::Problems(found));
    }
    let functions: HashMap<&Id, usize> = blueprint
        .graphs
        .iter()
        .enumerate()
        .map(|(at, graph)| (&graph.id, at))
        .collect();
    let mut graphs = Vec::new();
    let mut main = None;
    for (at, graph) in blueprint.graphs.iter().enumerate() {
        let index: HashMap<&Id, usize> = graph
            .nodes
            .iter()
            .enumerate()
            .map(|(at, node)| (&node.id, at))
            .collect();
        // Without problems, every node has its pins and every wire its two nodes.
        let all: Vec<_> = graph
            .nodes
            .iter()
            .map(|node| pins(blueprint, graph, node).unwrap_or_default())
            .collect();
        let mut nodes = Vec::new();
        for (node, its) in graph.nodes.iter().zip(&all) {
            let doing = doing(blueprint, &functions, &node.does)?;
            let source = |pin: &str| {
                let wire = graph
                    .wires
                    .iter()
                    .find(|wire| wire.to.0 == node.id && wire.to.1.0 == pin);
                if let Some(wire) = wire {
                    Source::Wire(
                        index.get(&wire.from.0).copied().unwrap_or_default(),
                        wire.from.1.0.clone(),
                    )
                } else if let Some(value) = node.inputs.get(&Id(pin.to_owned())) {
                    Source::Value(value.clone())
                } else {
                    let default = its.iter().find(|it| it.id == pin && it.side == Side::In);
                    default
                        .and_then(|it| it.default.clone())
                        .map_or(Source::Missing, Source::Value)
                }
            };
            let inputs = its
                .iter()
                .filter(|pin| pin.side == Side::In && pin.carries != Carries::Exec)
                .map(|pin| (pin.id.clone(), source(&pin.id)))
                .collect();
            let flows = graph
                .wires
                .iter()
                .filter(|wire| wire.from.0 == node.id)
                .filter(|wire| {
                    its.iter()
                        .any(|it| it.id == wire.from.1.0 && it.carries == Carries::Exec)
                })
                .map(|wire| {
                    let to = index.get(&wire.to.0).copied().unwrap_or_default();
                    (wire.from.1.0.clone(), (to, wire.to.1.0.clone()))
                })
                .collect();
            let pure = its.iter().all(|pin| pin.carries != Carries::Exec);
            nodes.push(NodeCode {
                id: node.id.clone(),
                doing,
                flows,
                inputs,
                pure,
                named: node.options.exit.as_ref().map(|exit| exit.0.clone()),
                outs: flow_outputs(its),
                cases: node.options.cases.clone(),
                types: input_types(graph, node, its, &all, &index),
                text: node.options.text.clone(),
            });
        }
        let starting = |starts| {
            nodes
                .iter()
                .position(|node| matches!(node.doing, Doing::Kind(kind) if kind.starts() == Some(starts)))
        };
        let entry = graph.function.as_ref().and_then(|_| starting(Starts::Call));
        if graph.function.is_none() && main.is_none() {
            main = starting(Starts::Main).map(|node| (at, node));
        }
        graphs.push(Code {
            id: graph.id.clone(),
            nodes,
            entry,
            locals: graph.function.iter().flat_map(|it| it.locals.clone()).collect(),
        });
    }
    Ok(Program {
        graphs,
        main: main.ok_or(Refused::NoStart)?,
    })
}
