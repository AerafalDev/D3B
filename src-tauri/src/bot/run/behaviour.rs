//! What a kind of node does when the flow reaches it, behind one trait (ADR 0017), and what it may use while it runs:
//! its inputs, pulled through their wires, its outputs, its own counter, the clock and the observed state. The engine
//! knows no kind; each brings its behaviour.

use std::collections::BTreeMap;
use std::time::Instant;

use serde_json::Value;

use super::frame::{Call, Slot};
use super::program::Program;
use super::values::{self, Trace};
use super::variables::Variables;
use crate::game::View;

/// The behaviour of a kind of node.
pub(crate) trait Behaviour: Sync {
    /// Whether a flow starts at its node: the main one, or a function's.
    fn starts(&self) -> Option<Starts> {
        None
    }

    /// The flow reached its node, or comes back to it.
    fn run(&self, cx: &mut Cx<'_>, resume: Resume) -> Result<Step, Fault> {
        let _ = (cx, resume);
        Err(Fault::NoFlow)
    }

    /// The value of its output `pin`, for a node without flow, read when the node that uses it runs.
    fn value(&self, cx: &mut Cx<'_>, pin: &str) -> Result<Value, Fault> {
        let _ = cx;
        Err(Fault::NoValue { pin: pin.to_owned() })
    }
}

/// Where a flow starts.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Starts {
    /// The main flow, "On start".
    Main,
    /// A function's flow, when it is called.
    Call,
}

/// Why the flow is at a node.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Resume {
    /// It came in through its flow input `pin` (empty where a flow starts), or comes back to the node at work after an
    /// interruption: it starts over, as actions are goal-shaped.
    Enter { pin: String },
    /// The node waits, and the clock or the observed state may have changed.
    Tick,
    /// The chain it ran ended.
    ChainEnded,
    /// Leave the loop was reached in the chain it ran.
    Left,
}

/// What the node does next.
#[derive(Debug, Clone, PartialEq)]
pub(crate) enum Step {
    /// The flow goes out of `pin`; one wired to nothing ends the chain.
    Go(String),
    /// It failed: the flow goes out of `failed` with the reason, and blocks the character if nothing is wired there.
    Failed(String),
    /// It runs the chain out of `pin` and is resumed when that ends; a breakable one is what Leave the loop ends.
    Chain { pin: String, breakable: bool },
    /// It waits on the observed state, or on the clock until then at the latest.
    Wait { until: Option<Instant> },
    /// The function ends by this exit, giving back its outputs.
    Return {
        exit: Exit,
        outputs: BTreeMap<String, Value>,
    },
    /// Leave the nearest loop.
    Leave,
    /// The run is finished.
    Finish,
    /// The character is blocked with this reason.
    Block(String),
}

impl Step {
    pub(crate) fn go(pin: &str) -> Self {
        Self::Go(pin.to_owned())
    }
}

/// How a function ended, which its call follows.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Exit {
    Done,
    Failed(String),
    /// One of its named exits.
    #[cfg_attr(
        test,
        expect(dead_code, reason = "v2's functions bring their named exits (ADR 0016)")
    )]
    Named(String),
}

/// A value that cannot be had, or a node used against its kind: the character blocks with it, never a panic.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
pub(crate) enum Fault {
    #[error("the input {pin} has no value")]
    Missing { pin: String },
    #[error("the output {pin} of {node} is read before its node ran")]
    NotGiven { node: String, pin: String },
    #[error("the node has no value {pin}")]
    NoValue { pin: String },
    #[error("the node takes no flow")]
    NoFlow,
    #[error("the input {pin} is not of its type")]
    NotOfType { pin: String },
    #[error("a number too large")]
    TooLarge,
    #[error("values that read one another")]
    Circle,
    #[error("Leave the loop outside a loop")]
    NotInALoop,
}

/// What a node may use while it runs.
pub(crate) struct Cx<'a> {
    pub(super) program: &'a Program,
    pub(super) graph: usize,
    pub(super) node: usize,
    /// How many values deep the read is: those the node at work reads are traced.
    pub(super) depth: u32,
    pub(super) call: &'a mut Call,
    pub(super) variables: &'a mut Variables,
    pub(super) trace: &'a mut Trace,
    #[cfg_attr(
        test,
        expect(dead_code, reason = "the queries and the actions' checks read it, in (25)'s next PR")
    )]
    pub(super) view: &'a View,
    pub(super) now: Instant,
}

impl Cx<'_> {
    /// The value of input `pin`, through its wire, its typed value or its pin's default; what the node at work reads
    /// is kept in the trace.
    pub(crate) fn input(&mut self, pin: &str) -> Result<Value, Fault> {
        let value = values::pull(self, pin)?;
        if self.depth == 0 {
            let code = &self.program.graphs[self.graph];
            self.trace
                .keep(&code.id, &code.nodes[self.node].id, pin, &value, self.now);
        }
        Ok(value)
    }

    /// Input `pin` as a whole number.
    pub(crate) fn number(&mut self, pin: &str) -> Result<i64, Fault> {
        let value = self.input(pin)?;
        value.as_i64().ok_or_else(|| Fault::NotOfType { pin: pin.to_owned() })
    }

    /// Input `pin` as yes or no.
    pub(crate) fn condition(&mut self, pin: &str) -> Result<bool, Fault> {
        let value = self.input(pin)?;
        value.as_bool().ok_or_else(|| Fault::NotOfType { pin: pin.to_owned() })
    }

    /// The node's value inputs, in its pins' order.
    pub(crate) fn inputs(&self) -> Vec<String> {
        let code = &self.program.graphs[self.graph].nodes[self.node];
        code.inputs.iter().map(|(pin, _)| pin.clone()).collect()
    }

    /// Gives output `pin`, which the nodes it feeds read from now on.
    pub(crate) fn give(&mut self, pin: &str, value: Value) {
        self.call.outputs.insert((self.node, pin.to_owned()), value);
    }

    /// The node's own counter, kept for this call (the whole run in an event graph), empty at first.
    pub(crate) fn slot(&mut self) -> &mut Slot {
        self.call.slots.entry(self.node).or_default()
    }

    pub(crate) fn now(&self) -> Instant {
        self.now
    }

    #[cfg_attr(
        test,
        expect(dead_code, reason = "the queries and the actions' checks read it, in (25)'s next PR")
    )]
    pub(crate) fn view(&self) -> &View {
        self.view
    }
}
