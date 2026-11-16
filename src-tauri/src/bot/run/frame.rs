//! The explicit stack a flow runs on (ADR 0017): a frame per call, and in it a chain per body a node runs (a loop's,
//! Sequence's outputs), each where its flow is. A waiting run is this data, so an interrupted flow keeps its place, its
//! loops' counters and its calls.

use std::collections::HashMap;
use std::time::Instant;

use serde_json::Value;

use super::behaviour::Resume;

/// A graph at work: the main flow's event graph, or a function called.
#[derive(Debug)]
pub(super) struct Call {
    pub(super) graph: usize,
    /// The node of the caller's graph that called it; none for an event graph.
    pub(super) caller: Option<usize>,
    /// The call's own chain first, then each body run inside it, the one at work last.
    pub(super) chains: Vec<Chain>,
    pub(super) slots: HashMap<usize, Slot>,
    /// What its nodes gave, by node and pin.
    pub(super) outputs: HashMap<(usize, String), Value>,
}

impl Call {
    /// `graph` called from node `caller`, its flow at `start`.
    pub(super) fn new(graph: usize, caller: Option<usize>, start: usize) -> Self {
        Self {
            graph,
            caller,
            chains: vec![Chain::new(None, false, start, String::new())],
            slots: HashMap::new(),
            outputs: HashMap::new(),
        }
    }
}

/// A chain of nodes the flow follows: the call's own, or a body a node runs.
#[derive(Debug)]
pub(super) struct Chain {
    /// The node that ran it, resumed when it ends; none for the call's own.
    pub(super) owner: Option<usize>,
    /// Whether Leave the loop ends it.
    pub(super) breakable: bool,
    /// The node at work.
    pub(super) at: usize,
    /// The flow input it came in by, to enter it again after an interruption.
    pub(super) entered: String,
    pub(super) resume: Resume,
}

impl Chain {
    pub(super) fn new(owner: Option<usize>, breakable: bool, at: usize, entered: String) -> Self {
        let resume = Resume::Enter { pin: entered.clone() };
        Self {
            owner,
            breakable,
            at,
            entered,
            resume,
        }
    }
}

/// What a node keeps between its runs: a loop's index, a wait's deadline.
#[derive(Debug, Clone, Default, PartialEq)]
pub(crate) struct Slot {
    pub(crate) count: u64,
    pub(crate) at: Option<Instant>,
    pub(crate) value: Option<Value>,
}
