//! Randomness (`docs/bot.md`, "The values (v2.4)"): a number between two bounds, "true N % of the time" and a random
//! element of a list, each drawn from the run's random source when read, once per node at work, the value it gives
//! kept in the trace.

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};

/// A random whole number from `low` to `high`, both included, reversed bounds swapped.
pub(super) struct Number;

impl Behaviour for Number {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (low, high) = (cx.number("low")?, cx.number("high")?);
        let drawn = cx.draw(low, high);
        Ok(cx.traced("value", Value::from(drawn)))
    }
}

/// True `percent` % of the time: never at 0, always at 100.
pub(super) struct Chance;

impl Behaviour for Chance {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let percent = cx.number("percent")?;
        let holds = cx.draw(1, 100) <= percent;
        Ok(cx.traced("value", Value::Bool(holds)))
    }
}

/// An element of its list, each as likely; an empty list has none to give.
pub(super) struct Element;

impl Behaviour for Element {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let list = cx.input("list")?;
        let elements = list
            .as_array()
            .ok_or_else(|| Fault::NotOfType { pin: "list".to_owned() })?;
        let highest = i64::try_from(elements.len()).map_err(|_| Fault::TooLarge)? - 1;
        if highest < 0 {
            return Err(Fault::Empty { pin: "list".to_owned() });
        }
        let at = usize::try_from(cx.draw(0, highest)).unwrap_or_default();
        let element = elements.get(at).cloned().unwrap_or_default();
        Ok(cx.traced("element", element))
    }
}
