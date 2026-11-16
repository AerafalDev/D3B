//! Values pulled when the node that uses them runs (`docs/bot.md`, "How a blueprint runs"): an input's wire leads to a
//! node that ran and gave it, or to one without flow whose output is computed now; each value the node at work used is
//! kept, a bounded number of them, for the debugger.

use std::collections::VecDeque;
use std::time::Instant;

use serde_json::Value;

use super::behaviour::{Cx, Fault};
use super::program::{Doing, Source};
use crate::bot::Id;

/// How deep values may read one another (settled alone): deeper is taken for a circle, as no blueprint chains so many.
const DEEPEST: u32 = 64;

/// The value of input `pin` of the node `cx` is at.
pub(super) fn pull(cx: &mut Cx<'_>, pin: &str) -> Result<Value, Fault> {
    let code = &cx.program.graphs[cx.graph].nodes[cx.node];
    let source = code
        .inputs
        .iter()
        .find(|(id, _)| id == pin)
        .map(|(_, source)| source.clone());
    match source {
        Some(Source::Value(value)) => Ok(value),
        Some(Source::Wire(node, output)) => given(cx, node, &output),
        Some(Source::Missing) | None => Err(Fault::Missing { pin: pin.to_owned() }),
    }
}

/// Output `pin` of `node`: what it gave when it ran, or, without flow, computed now.
fn given(cx: &mut Cx<'_>, node: usize, pin: &str) -> Result<Value, Fault> {
    if let Some(value) = cx.call.outputs.get(&(node, pin.to_owned())) {
        return Ok(value.clone());
    }
    let program = cx.program;
    let code = &program.graphs[cx.graph].nodes[node];
    if !code.pure {
        return Err(Fault::NotGiven {
            node: code.id.0.clone(),
            pin: pin.to_owned(),
        });
    }
    let key = (node, pin.to_owned());
    if let Some(value) = cx.read.get(&key) {
        return Ok(value.clone());
    }
    let value = match &code.doing {
        Doing::Get(variable) => cx
            .variables
            .get(variable)
            .cloned()
            .ok_or_else(|| Fault::Missing { pin: variable.clone() }),
        Doing::Kind(behaviour) if cx.depth < DEEPEST => {
            let at = std::mem::replace(&mut cx.node, node);
            cx.depth += 1;
            let value = behaviour.value(cx, pin);
            cx.depth -= 1;
            cx.node = at;
            value
        }
        Doing::Kind(_) => Err(Fault::Circle),
        Doing::Set(_) | Doing::Call(_) => Err(Fault::NoValue { pin: pin.to_owned() }),
    };
    match &value {
        Ok(value) => {
            cx.read.insert(key, value.clone());
        }
        // The deepest is where it failed: those above only passed it on.
        Err(_) => {
            cx.failed_at.get_or_insert(node);
        }
    }
    value
}

/// A value a node used.
#[derive(Debug, Clone, PartialEq)]
pub(crate) struct Traced {
    pub(crate) graph: Id,
    pub(crate) node: Id,
    pub(crate) pin: String,
    pub(crate) value: Value,
    pub(crate) at: Instant,
}

/// The values used last, oldest first.
#[derive(Debug, Default)]
pub(crate) struct Trace(VecDeque<Traced>);

impl Trace {
    /// How many are kept: enough for the debugger's last minutes (settled alone).
    pub(crate) const KEPT: usize = 1_000;

    pub(super) fn keep(&mut self, graph: &Id, node: &Id, pin: &str, value: &Value, at: Instant) {
        if self.0.len() == Self::KEPT {
            self.0.pop_front();
        }
        self.0.push_back(Traced {
            graph: graph.clone(),
            node: node.clone(),
            pin: pin.to_owned(),
            value: value.clone(),
            at,
        });
    }

    pub(crate) fn iter(&self) -> impl Iterator<Item = &Traced> {
        self.0.iter()
    }
}
