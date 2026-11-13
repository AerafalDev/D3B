//! A blueprint's variables (`docs/bot.md`, "The concepts"): a type, a default, bounds for a number, a help line, set
//! per character or not (Unreal's Instance Editable: the blueprint holds the default, each character its own value),
//! and kept for one run or as memory across sessions. The owner allowed both at once on 2026-11-13, as Unreal does: a
//! value the player sets that the bot then carries on.

use serde::{Deserialize, Serialize};
use serde_json::Value;

use super::blueprint::Id;

/// A variable of a blueprint.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Variable {
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(rename = "type")]
    pub(crate) value_type: ValueType,
    /// `null` when the blueprint gives none, so that each character must set its own before it runs.
    #[serde(default, skip_serializing_if = "Value::is_null")]
    pub(crate) default: Value,
    /// The smallest and largest value of a number or a percentage, both included.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) bounds: Option<[i64; 2]>,
    #[serde(default, skip_serializing_if = "is_false")]
    pub(crate) per_character: bool,
    /// Kept per character across sessions, rather than for one run.
    #[serde(default, skip_serializing_if = "is_false")]
    pub(crate) memory: bool,
    #[serde(default, skip_serializing_if = "String::is_empty")]
    pub(crate) help: String,
}

#[expect(
    clippy::trivially_copy_pass_by_ref,
    reason = "serde's skip_serializing_if passes a reference"
)]
fn is_false(value: &bool) -> bool {
    !value
}

/// What a variable, a pin or a function's parameter holds.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub(crate) enum ValueType {
    /// Yes or no: `true`, `false`.
    Bool,
    /// A whole number: `5000`.
    Number,
    /// A whole percentage, 0 to 100: `90`.
    Percentage,
    /// A line of text: `"Goal reached"`.
    Text,
    /// A game's sub-area, `{"sub_area": 445}`, or a zone of mine, `{"zone": "<id>"}`.
    Zone,
    /// A route of mine, by its id: `"<id>"`.
    Route,
    /// The nearest bank, `"nearest"`, or the one on a map, `{"map": 191104002}`.
    Bank,
    /// Every item, `"all"`, none, `"none"`, or these, by the game's item ids: `[289, 400]`.
    Items,
}

impl ValueType {
    /// Whether `value` holds as this type; `null`, no value yet, holds as any.
    pub(crate) fn holds(self, value: &Value) -> bool {
        match (self, value) {
            (_, Value::Null) | (Self::Bool, Value::Bool(_)) | (Self::Text, Value::String(_)) => true,
            (Self::Number, Value::Number(number)) => number.is_i64(),
            (Self::Percentage, Value::Number(number)) => {
                number.as_i64().is_some_and(|whole| (0..=100).contains(&whole))
            }
            (Self::Zone, Value::Object(object)) => match object.iter().next() {
                Some((key, Value::Number(id))) if object.len() == 1 && key == "sub_area" => id.is_u64(),
                Some((key, Value::String(id))) if object.len() == 1 && key == "zone" => Id(id.clone()).is_well_formed(),
                _ => false,
            },
            (Self::Route, Value::String(id)) => Id(id.clone()).is_well_formed(),
            (Self::Bank, Value::String(word)) => word == "nearest",
            (Self::Bank, Value::Object(object)) => object.len() == 1 && object.get("map").is_some_and(Value::is_u64),
            (Self::Items, Value::String(word)) => word == "all" || word == "none",
            (Self::Items, Value::Array(ids)) => ids.iter().all(Value::is_u64),
            _ => false,
        }
    }

    /// Whether this type has bounds: a number or a percentage.
    pub(crate) fn is_numeric(self) -> bool {
        matches!(self, Self::Number | Self::Percentage)
    }
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::*;

    #[test]
    fn each_type_holds_its_shape() {
        let holding = [
            (ValueType::Bool, json!(true)),
            (ValueType::Number, json!(-5000)),
            (ValueType::Percentage, json!(0)),
            (ValueType::Percentage, json!(100)),
            (ValueType::Text, json!("Goal reached")),
            (ValueType::Zone, json!({"sub_area": 445})),
            (ValueType::Zone, json!({"zone": "my-wheat"})),
            (ValueType::Route, json!("bonta-loop")),
            (ValueType::Bank, json!("nearest")),
            (ValueType::Bank, json!({"map": 191_104_002})),
            (ValueType::Items, json!("all")),
            (ValueType::Items, json!("none")),
            (ValueType::Items, json!([289, 400])),
        ];
        for (value_type, value) in holding {
            assert!(value_type.holds(&value), "{value_type:?} {value}");
        }
    }

    #[test]
    fn no_value_yet_holds_as_any_type() {
        assert!(ValueType::Zone.holds(&Value::Null));
        assert!(ValueType::Number.holds(&Value::Null));
    }

    #[test]
    fn a_type_refuses_another_shape() {
        let refused = [
            (ValueType::Bool, json!("yes")),
            (ValueType::Number, json!(1.5)),
            (ValueType::Percentage, json!(101)),
            (ValueType::Percentage, json!(-1)),
            (ValueType::Text, json!(3)),
            (ValueType::Zone, json!({"sub_area": -1})),
            (ValueType::Zone, json!({"zone": "My Zone"})),
            (ValueType::Zone, json!({"sub_area": 445, "zone": "my-wheat"})),
            (ValueType::Route, json!("Bonta loop")),
            (ValueType::Bank, json!("farthest")),
            (ValueType::Bank, json!({"map": "bonta"})),
            (ValueType::Items, json!("some")),
            (ValueType::Items, json!([289, "wheat"])),
        ];
        for (value_type, value) in refused {
            assert!(!value_type.holds(&value), "{value_type:?} {value}");
        }
    }

    #[test]
    fn a_variable_omits_what_it_leaves_at_its_default() {
        let variable: Variable = serde_json::from_str(r#"{"id": "zone", "name": "Zone", "type": "zone"}"#)
            .unwrap_or_else(|error| panic!("{error}"));
        assert!(variable.default.is_null() && !variable.per_character && !variable.memory);
        let written = serde_json::to_string(&variable).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(written, r#"{"id":"zone","name":"Zone","type":"zone"}"#);
    }
}
