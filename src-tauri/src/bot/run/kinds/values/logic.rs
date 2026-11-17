//! The logic: AND and OR of N inputs, read in order up to the first that decides, NOT, and Choose, which reads only the
//! side it takes, so a value that cannot be computed, or a draw, on the other side neither blocks nor draws.

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};

/// AND (`true`: all of them) or OR (`false`: any of them) of its inputs `in_1` to `in_n`.
pub(super) struct All(pub(super) bool);

impl Behaviour for All {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let all = self.0;
        for pin in cx.inputs() {
            // The first that differs from what all of them would give decides.
            if cx.condition(&pin)? != all {
                return Ok(Value::Bool(!all));
            }
        }
        Ok(Value::Bool(all))
    }
}

/// NOT: the other of its input.
pub(super) struct Not;

impl Behaviour for Not {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        Ok(Value::Bool(!cx.condition("value")?))
    }
}

/// Choose: `if_true` when its condition holds, else `if_false`, the other never read.
pub(super) struct Choose;

impl Behaviour for Choose {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let side = if cx.condition("condition")? {
            "if_true"
        } else {
            "if_false"
        };
        cx.input(side)
    }
}
