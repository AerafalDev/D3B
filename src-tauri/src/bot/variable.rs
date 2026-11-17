//! A blueprint's variables (`docs/bot.md`, "The concepts"): a type, a default, bounds for a number, a help line, set
//! per character or not (Unreal's Instance Editable: the blueprint holds the default, each character its own value),
//! and kept for one run or as memory across sessions. The owner allowed both at once on 2026-11-13, as Unreal does: a
//! value the player sets that the bot then carries on. v2's types (ADR 0016): a map, an item, a duration, a time of day
//! and an NPC beside v1's, each with its list and its ranges ("from level N to M: a value").

use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};
use serde_json::Value;

use super::blueprint::Id;

/// A variable of a blueprint.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[expect(
    clippy::struct_excessive_bools,
    reason = "a variable's list, ranges, setting and memory, each the player's yes or no, independent of the others"
)]
#[serde(deny_unknown_fields)]
pub(crate) struct Variable {
    pub(crate) id: Id,
    pub(crate) name: String,
    #[serde(rename = "type")]
    pub(crate) value_type: ValueType,
    /// A list of values of its type, rather than one.
    #[serde(default, skip_serializing_if = "is_false")]
    pub(crate) list: bool,
    /// Ranges whose rows hold a value of its type (or a list of them, with `list`).
    #[serde(default, skip_serializing_if = "is_false")]
    pub(crate) ranges: bool,
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

impl Variable {
    /// Its whole type: its type, as a list or ranges or neither.
    pub(crate) fn of_type(&self) -> Type {
        Type::new(self.value_type, Shape::new(self.list, self.ranges))
    }

    /// Whether a character may hold `value` as its own: a value of its type, within its bounds.
    pub(crate) fn accepts(&self, value: &Value) -> bool {
        let within =
            |[smallest, largest]: [i64; 2]| value.as_i64().is_none_or(|whole| (smallest..=largest).contains(&whole));
        !value.is_null() && self.of_type().holds(value) && self.bounds.is_none_or(within)
    }
}

/// A character's settings once it runs a blueprint of the variables `to` instead of one of `from` (the owner,
/// 2026-11-16): a value is kept when `to` sets per character a variable of the same id and type that accepts it
/// (within its bounds) and of another default, so a Zone goes from one harvest to another, and the others are dropped. `from` is none when that blueprint is gone:
/// the type the value had is then unknown, and a value `to`'s variable accepts is kept.
pub(crate) fn carried(
    settings: &BTreeMap<String, Value>,
    from: Option<&[Variable]>,
    to: &[Variable],
) -> BTreeMap<String, Value> {
    let had = |id: &str| from.map(|from| from.iter().find(|variable| variable.id.0 == id).map(Variable::of_type));
    settings
        .iter()
        .filter(|(id, value)| {
            to.iter().any(|variable| {
                variable.id.0 == **id
                    && variable.per_character
                    && variable.accepts(value)
                    // A value that is the new default is no longer the character's own, as when set back to it.
                    && **value != variable.default
                    && had(id).is_none_or(|had| had == Some(variable.of_type()))
            })
        })
        .map(|(id, value)| (id.clone(), value.clone()))
        .collect()
}

#[expect(
    clippy::trivially_copy_pass_by_ref,
    reason = "serde's skip_serializing_if passes a reference"
)]
fn is_false(value: &bool) -> bool {
    !value
}

/// What a variable, a pin or a function's parameter holds.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
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
    /// A profession, by the game's job id: `28`.
    Job,
    /// A map, by the game's map id: `191104002`.
    Map,
    /// One item, by the game's item id: `289`.
    Item,
    /// A duration, in whole seconds: `90`.
    Duration,
    /// A time of day, `"HH:MM"` from `"00:00"` to `"23:59"`: `"23:00"`.
    Time,
    /// A non-player character, by the game's NPC id: `774`.
    Npc,
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
            (Self::Job | Self::Map | Self::Item | Self::Duration | Self::Npc, Value::Number(whole)) => whole.is_u64(),
            (Self::Time, Value::String(time)) => is_time(time),
            _ => false,
        }
    }

    /// Whether this type has bounds: a number or a percentage.
    pub(crate) fn is_numeric(self) -> bool {
        matches!(self, Self::Number | Self::Percentage)
    }
}

/// `"HH:MM"`, from `"00:00"` to `"23:59"`.
fn is_time(text: &str) -> bool {
    let two = |part: &str, below: u8| part.len() == 2 && part.parse::<u8>().is_ok_and(|whole| whole < below);
    text.split_once(':')
        .is_some_and(|(hours, minutes)| two(hours, 24) && two(minutes, 60))
}

/// How many values of a type a value holds: one, a list of them, or ranges whose rows hold one or a list.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Hash)]
pub(crate) struct Shape {
    pub(crate) list: bool,
    pub(crate) ranges: bool,
}

impl Shape {
    /// One value.
    pub(crate) const ONE: Self = Self::new(false, false);

    pub(crate) const fn new(list: bool, ranges: bool) -> Self {
        Self { list, ranges }
    }
}

/// What a variable, a parameter or a pin holds, whole: a type and its shape.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub(crate) struct Type {
    pub(crate) of: ValueType,
    pub(crate) shape: Shape,
}

impl Type {
    pub(crate) const fn new(of: ValueType, shape: Shape) -> Self {
        Self { of, shape }
    }

    /// Whether `value` holds as this type; `null`, no value yet, holds as any. A list is an array of values, never
    /// `null`; ranges are rows `{"from": 1, "to": 49, "value": …}`, from no greater than to, in any order.
    pub(crate) fn holds(self, value: &Value) -> bool {
        let one = |value: &Value| !value.is_null() && self.of.holds(value);
        let some = |value: &Value| {
            if self.shape.list {
                value.as_array().is_some_and(|values| values.iter().all(one))
            } else {
                one(value)
            }
        };
        if value.is_null() {
            return true;
        }
        if !self.shape.ranges {
            return some(value);
        }
        value.as_array().is_some_and(|rows| {
            rows.iter().all(|row| {
                let ends = row.as_object().filter(|row| row.len() == 3).and_then(|row| {
                    let end = |key| row.get(key).and_then(Value::as_u64);
                    Some((end("from")?, end("to")?, row.get("value")?))
                });
                ends.is_some_and(|(from, to, value)| from <= to && some(value))
            })
        })
    }

    /// Whether ranges hold two rows that a number reaches at once; `false` for a value that holds no ranges.
    pub(crate) fn overlaps(self, value: &Value) -> bool {
        if !self.shape.ranges || !self.holds(value) {
            return false;
        }
        let end = |row: &Value, key| row.get(key).and_then(Value::as_u64).unwrap_or_default();
        let mut rows: Vec<(u64, u64)> = value
            .as_array()
            .into_iter()
            .flatten()
            .map(|row| (end(row, "from"), end(row, "to")))
            .collect();
        rows.sort_unstable();
        rows.windows(2).any(|pair| pair[1].0 <= pair[0].1)
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
            (ValueType::Job, json!(28)),
            (ValueType::Map, json!(191_104_002)),
            (ValueType::Item, json!(289)),
            (ValueType::Duration, json!(0)),
            (ValueType::Duration, json!(90)),
            (ValueType::Time, json!("00:00")),
            (ValueType::Time, json!("23:59")),
            (ValueType::Npc, json!(774)),
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
            (ValueType::Job, json!(-1)),
            (ValueType::Job, json!("farmer")),
            (ValueType::Map, json!(-1)),
            (ValueType::Map, json!({"map": 191_104_002})),
            (ValueType::Item, json!([289])),
            (ValueType::Duration, json!(1.5)),
            (ValueType::Duration, json!("1 min 30")),
            (ValueType::Time, json!("24:00")),
            (ValueType::Time, json!("23:60")),
            (ValueType::Time, json!("9:00")),
            (ValueType::Time, json!("09:00:00")),
            (ValueType::Time, json!(900)),
            (ValueType::Npc, json!("Otomaï")),
        ];
        for (value_type, value) in refused {
            assert!(!value_type.holds(&value), "{value_type:?} {value}");
        }
    }

    #[test]
    fn a_list_holds_values_of_its_type_and_never_none() {
        let maps = Type::new(ValueType::Map, Shape::new(true, false));
        for holding in [json!([]), json!([191_104_002, 88_080_385]), Value::Null] {
            assert!(maps.holds(&holding), "{holding}");
        }
        for refused in [json!(191_104_002), json!([191_104_002, null]), json!(["5,-18"])] {
            assert!(!maps.holds(&refused), "{refused}");
        }
    }

    #[test]
    fn ranges_hold_rows_from_a_level_to_another_of_a_value_or_a_list() {
        let zones = Type::new(ValueType::Zone, Shape::new(false, true));
        let rows = json!([
            {"from": 1, "to": 49, "value": {"sub_area": 445}},
            {"from": 50, "to": 200, "value": {"zone": "my-wheat"}}
        ]);
        assert!(zones.holds(&rows) && zones.holds(&json!([])));
        let refused = [
            json!({"from": 1, "to": 49, "value": {"sub_area": 445}}),
            json!([{"from": 50, "to": 49, "value": {"sub_area": 445}}]),
            json!([{"from": 1, "value": {"sub_area": 445}}]),
            json!([{"from": 1, "to": 49, "value": null}]),
            json!([{"from": 1, "to": 49, "value": {"sub_area": 445}, "note": ""}]),
            json!([{"from": 1, "to": 49, "value": [{"sub_area": 445}]}]),
        ];
        for refused in refused {
            assert!(!zones.holds(&refused), "{refused}");
        }
        let lists = Type::new(ValueType::Zone, Shape::new(true, true));
        assert!(lists.holds(&json!([{"from": 1, "to": 49, "value": [{"sub_area": 445}]}])));
    }

    #[test]
    fn ranges_overlap_when_a_level_reaches_two_rows() {
        let numbers = Type::new(ValueType::Number, Shape::new(false, true));
        let row = |from: u64, to: u64| json!({"from": from, "to": to, "value": 1});
        assert!(!numbers.overlaps(&json!([row(50, 200), row(1, 49)])), "in any order");
        assert!(
            !numbers.overlaps(&json!([row(1, 9), row(20, 30)])),
            "a gap is no overlap"
        );
        assert!(numbers.overlaps(&json!([row(40, 60), row(1, 40)])), "a level shared");
        assert!(
            numbers.overlaps(&json!([row(1, 100), row(20, 30)])),
            "one inside another"
        );
        let one = Type::new(ValueType::Number, Shape::ONE);
        assert!(!one.overlaps(&json!(5)) && !numbers.overlaps(&json!("rows")));
    }

    fn variable(id: &str, value_type: ValueType, bounds: Option<[i64; 2]>) -> Variable {
        Variable {
            id: Id(id.into()),
            name: id.into(),
            value_type,
            list: false,
            ranges: false,
            default: Value::Null,
            bounds,
            per_character: true,
            memory: false,
            help: String::new(),
        }
    }

    #[test]
    fn a_setting_accepts_a_value_of_its_type_within_its_bounds_and_never_none() {
        let pods = variable("pods", ValueType::Percentage, Some([10, 100]));
        assert!(pods.accepts(&json!(10)) && pods.accepts(&json!(100)), "both ends");
        for refused in [json!(9), json!(101), json!("90"), Value::Null] {
            assert!(!pods.accepts(&refused), "{refused}");
        }
        assert!(variable("zone", ValueType::Zone, None).accepts(&json!({"sub_area": 445})));
    }

    #[test]
    fn another_blueprint_keeps_the_values_of_its_variables_of_the_same_id_and_type() {
        let settings = BTreeMap::from([
            ("zone".to_owned(), json!({"sub_area": 445})),
            ("pods".to_owned(), json!(80)),
            ("goal".to_owned(), json!(5000)),
            ("job".to_owned(), json!(24)),
            ("keep".to_owned(), json!("all")),
        ]);
        let from = [
            variable("zone", ValueType::Zone, None),
            variable("pods", ValueType::Percentage, Some([10, 100])),
            variable("goal", ValueType::Number, None),
            variable("job", ValueType::Job, None),
            variable("keep", ValueType::Items, None),
        ];
        let mut keep = variable("keep", ValueType::Items, None);
        keep.default = json!("all");
        let mut not_set = variable("job", ValueType::Job, None);
        not_set.per_character = false;
        let to = [
            variable("zone", ValueType::Zone, None),
            // A number now: the same id, another type.
            variable("pods", ValueType::Number, None),
            variable("goal", ValueType::Number, Some([0, 1000])),
            not_set,
            // Its new default: no longer the character's own.
            keep,
        ];
        let kept = carried(&settings, Some(&from), &to);
        assert_eq!(kept.keys().collect::<Vec<_>>(), ["zone"]);
        // A list of items now, which holds `[289]` as Items did: another type.
        let picked = BTreeMap::from([("keep".to_owned(), json!([289]))]);
        let mut items = variable("keep", ValueType::Item, None);
        items.list = true;
        assert!(items.accepts(&json!([289])));
        assert!(carried(&picked, Some(&from), &[items]).is_empty());
        let gone = carried(&settings, None, &to);
        assert_eq!(
            gone.keys().collect::<Vec<_>>(),
            ["pods", "zone"],
            "its type unknown, what holds is kept"
        );
    }

    #[test]
    fn a_variable_omits_what_it_leaves_at_its_default() {
        let variable: Variable = serde_json::from_str(r#"{"id": "zone", "name": "Zone", "type": "zone"}"#)
            .unwrap_or_else(|error| panic!("{error}"));
        assert!(variable.default.is_null() && !variable.per_character && !variable.memory);
        assert!(!variable.list && !variable.ranges);
        let written = serde_json::to_string(&variable).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(written, r#"{"id":"zone","name":"Zone","type":"zone"}"#);
    }
}
