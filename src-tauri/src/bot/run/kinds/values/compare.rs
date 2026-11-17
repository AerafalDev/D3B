//! The comparisons: `=` and `≠` of any one type, the order of numbers, percentages, durations and times, and between.

use std::cmp::Ordering;

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};

/// Which order a comparison asks for.
#[derive(Clone, Copy)]
pub(super) enum Order {
    Below,
    AtMost,
    Above,
    AtLeast,
}

/// The order of `a` and `b`: numbers (a percentage, a duration's seconds) by their value, times "HH:MM" through the
/// day, which their text keeps; `None` for values of no order or of two kinds.
fn order(a: &Value, b: &Value) -> Option<Ordering> {
    match (a, b) {
        (Value::String(a), Value::String(b)) => Some(a.cmp(b)),
        _ => Some(a.as_i64()?.cmp(&b.as_i64()?)),
    }
}

/// The two inputs `a` and `b` and their order, or which of them holds no ordered value.
fn ordered(cx: &mut Cx<'_>) -> Result<Ordering, Fault> {
    let (a, b) = (cx.input("a")?, cx.input("b")?);
    order(&a, &b).ok_or_else(|| Fault::NotOfType {
        pin: if a.is_string() || a.is_i64() { "b" } else { "a" }.to_owned(),
    })
}

/// `<`, `≤`, `>`, `≥`: whether `a` is in that order to `b`.
pub(super) struct Compare(pub(super) Order);

impl Behaviour for Compare {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let found = ordered(cx)?;
        Ok(Value::Bool(match self.0 {
            Order::Below => found.is_lt(),
            Order::AtMost => found.is_le(),
            Order::Above => found.is_gt(),
            Order::AtLeast => found.is_ge(),
        }))
    }
}

/// `=` (or `≠` with `false`): whether `a` and `b` are the same value.
pub(super) struct Equal(pub(super) bool);

impl Behaviour for Equal {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (a, b) = (cx.input("a")?, cx.input("b")?);
        // Whole numbers only reach a run (their types hold no other), so the JSON compares them exactly.
        Ok(Value::Bool((a == b) == self.0))
    }
}

/// Between: whether `value` lies from `low` to `high`, both included; numbers with their bounds the other way round
/// swapped, times from a later `low` to an earlier `high` past midnight ("23:00" to "08:00").
pub(super) struct Between;

impl Behaviour for Between {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (value, low, high) = (cx.input("value")?, cx.input("low")?, cx.input("high")?);
        let not_of_type = |pin: &str| Fault::NotOfType { pin: pin.to_owned() };
        if order(&value, &value).is_none() {
            return Err(not_of_type("value"));
        }
        let from_low = order(&value, &low).ok_or_else(|| not_of_type("low"))?;
        let to_high = order(&value, &high).ok_or_else(|| not_of_type("high"))?;
        let bounds = order(&low, &high).ok_or_else(|| not_of_type("high"))?;
        let inside = match bounds {
            Ordering::Greater if value.is_string() => from_low.is_ge() || to_high.is_le(),
            Ordering::Greater => to_high.is_ge() && from_low.is_le(),
            _ => from_low.is_ge() && to_high.is_le(),
        };
        Ok(Value::Bool(inside))
    }
}
