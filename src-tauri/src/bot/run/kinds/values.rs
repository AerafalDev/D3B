//! v1's values without flow: at least and add, computed when read.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault};

/// At least: whether `a` ≥ `b`.
pub(super) struct AtLeast;

impl Behaviour for AtLeast {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        Ok(Value::Bool(cx.number("a")? >= cx.number("b")?))
    }
}

/// Add: `a` + `b`, a sum too large for a whole number blocking rather than wrapping.
pub(super) struct Add;

impl Behaviour for Add {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let sum = cx.number("a")?.checked_add(cx.number("b")?).ok_or(Fault::TooLarge)?;
        Ok(Value::from(sum))
    }
}
