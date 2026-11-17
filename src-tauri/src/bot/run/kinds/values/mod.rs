//! The values (`docs/bot.md`, "The values (v2.4)"): nodes without flow, computed from their inputs when the node that
//! uses them reads them, each input read only when needed, so Choose, AND and OR never read the side they do not take.
//! N minutes and add live here; each family of values in its own file.

mod compare;
mod logic;

#[cfg(test)]
mod tests;

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault};

/// The behaviour of the value kind `kind`, if it is one.
pub(super) fn behaviour(kind: &str) -> Option<&'static dyn Behaviour> {
    Some(match kind {
        "greater_or_equal" => &compare::Compare(compare::Order::AtLeast),
        "greater" => &compare::Compare(compare::Order::Above),
        "less" => &compare::Compare(compare::Order::Below),
        "less_or_equal" => &compare::Compare(compare::Order::AtMost),
        "equal" => &compare::Equal(true),
        "not_equal" => &compare::Equal(false),
        "between" => &compare::Between,
        "and" => &logic::All(true),
        "or" => &logic::All(false),
        "not" => &logic::Not,
        "choose" => &logic::Choose,
        "minutes" => &Minutes,
        "add" => &Add,
        _ => return None,
    })
}

/// N minutes: their duration in seconds, none below 0, one too long blocking rather than wrapping.
struct Minutes;

impl Behaviour for Minutes {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let minutes = u64::try_from(cx.number("minutes")?).map_err(|_| Fault::NotOfType {
            pin: "minutes".to_owned(),
        })?;
        Ok(Value::from(minutes.checked_mul(60).ok_or(Fault::TooLarge)?))
    }
}

/// Add: `a` + `b`, a sum too large for a whole number blocking rather than wrapping.
struct Add;

impl Behaviour for Add {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let sum = cx.number("a")?.checked_add(cx.number("b")?).ok_or(Fault::TooLarge)?;
        Ok(Value::from(sum))
    }
}
