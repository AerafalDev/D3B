//! The values (`docs/bot.md`, "The values (v2.4)"): nodes without flow, computed from their inputs when the node that
//! uses them reads them, each input read only when needed, so Choose, AND and OR never read the side they do not take.
//! N minutes and N seconds live here; each family of values in its own file.

mod arithmetic;
mod compare;
mod lists;
mod logic;
mod random;

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
        "minutes" => &Duration(60, "minutes"),
        "seconds" => &Duration(1, "seconds"),
        "add" => &arithmetic::Arithmetic(arithmetic::Operation::Add),
        "subtract" => &arithmetic::Arithmetic(arithmetic::Operation::Subtract),
        "multiply" => &arithmetic::Arithmetic(arithmetic::Operation::Multiply),
        "divide" => &arithmetic::Arithmetic(arithmetic::Operation::Divide),
        "remainder" => &arithmetic::Arithmetic(arithmetic::Operation::Remainder),
        "min" => &arithmetic::Arithmetic(arithmetic::Operation::Min),
        "max" => &arithmetic::Arithmetic(arithmetic::Operation::Max),
        "clamp" => &arithmetic::Clamp,
        "percent_of" => &arithmetic::PercentOf,
        "random_number" => &random::Number,
        "chance" => &random::Chance,
        "random_element" => &random::Element,
        "make_list" => &lists::Make,
        "length" => &lists::Length,
        "contains" => &lists::Find(false),
        "position_of" => &lists::Find(true),
        "element_at" => &lists::At(false),
        "first" => &lists::At(true),
        "equal_lists" => &lists::Same,
        _ => return None,
    })
}

/// N minutes or N seconds: a duration in seconds of its input times `.0`, none below 0, one too long blocking rather
/// than wrapping.
struct Duration(u64, &'static str);

impl Behaviour for Duration {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let count = u64::try_from(cx.number(self.1)?).map_err(|_| Fault::NotOfType { pin: self.1.to_owned() })?;
        Ok(Value::from(count.checked_mul(self.0).ok_or(Fault::TooLarge)?))
    }
}
