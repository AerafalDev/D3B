//! Values without flow, computed when read: `≥`, add and N minutes.

use serde_json::Value;

use super::super::behaviour::{Behaviour, Cx, Fault};

/// `≥`: whether `a` ≥ `b`, numbers (a percentage, a duration's seconds) by their value, times "HH:MM" in their
/// order through the day, which their text keeps.
pub(super) struct GreaterOrEqual;

impl Behaviour for GreaterOrEqual {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (a, b) = (cx.input("a")?, cx.input("b")?);
        let at_least = match (&a, &b) {
            (Value::String(a), Value::String(b)) => a >= b,
            _ => match (a.as_i64(), b.as_i64()) {
                (Some(a), Some(b)) => a >= b,
                (None, _) => return Err(Fault::NotOfType { pin: "a".to_owned() }),
                (_, None) => return Err(Fault::NotOfType { pin: "b".to_owned() }),
            },
        };
        Ok(Value::Bool(at_least))
    }
}

/// N minutes: their duration in seconds, none below 0, one too long blocking rather than wrapping.
pub(super) struct Minutes;

impl Behaviour for Minutes {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let minutes = u64::try_from(cx.number("minutes")?).map_err(|_| Fault::NotOfType {
            pin: "minutes".to_owned(),
        })?;
        Ok(Value::from(minutes.checked_mul(60).ok_or(Fault::TooLarge)?))
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
