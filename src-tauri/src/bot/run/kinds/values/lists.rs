//! The lists (`docs/bot.md`, "The values (v2.4)"): one made of its inputs, its length, whether it holds an element, its
//! element n° from 1, its first, an element's position (0 when absent), and two lists compared by their order. An
//! element out of the list, or the first of an empty one, blocks on its node.

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};

/// The input `pin`, a list.
fn list(cx: &mut Cx<'_>, pin: &str) -> Result<Vec<Value>, Fault> {
    match cx.input(pin)? {
        Value::Array(elements) => Ok(elements),
        _ => Err(Fault::NotOfType { pin: pin.to_owned() }),
    }
}

/// A list of its inputs `in_1` to `in_n`, in their order.
pub(super) struct Make;

impl Behaviour for Make {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let elements = cx
            .inputs()
            .iter()
            .map(|pin| cx.input(pin))
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Value::Array(elements))
    }
}

/// How many elements its list holds.
pub(super) struct Length;

impl Behaviour for Length {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        Ok(Value::from(list(cx, "list")?.len()))
    }
}

/// Contains, or its element's position from 1 (`.0`, 0 when absent).
pub(super) struct Find(pub(super) bool);

impl Behaviour for Find {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let (elements, element) = (list(cx, "list")?, cx.input("element")?);
        let at = elements.iter().position(|it| *it == element);
        Ok(if self.0 {
            Value::from(at.map_or(0, |at| at + 1))
        } else {
            Value::Bool(at.is_some())
        })
    }
}

/// Element n° `index`, from 1, or the first (`.0`); one the list does not hold blocks.
pub(super) struct At(pub(super) bool);

impl Behaviour for At {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let elements = list(cx, "list")?;
        if self.0 {
            return elements
                .first()
                .cloned()
                .ok_or_else(|| Fault::Empty { pin: "list".to_owned() });
        }
        let index = cx.number("index")?;
        let at = usize::try_from(index).ok().and_then(|index| index.checked_sub(1));
        at.and_then(|at| elements.get(at).cloned()).ok_or(Fault::OutOfList {
            index,
            length: elements.len(),
        })
    }
}

/// `=` of two lists: the same elements in the same order.
pub(super) struct Same;

impl Behaviour for Same {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        Ok(Value::Bool(list(cx, "a")? == list(cx, "b")?))
    }
}
