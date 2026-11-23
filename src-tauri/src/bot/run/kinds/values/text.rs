//! Text and Join (`docs/bot.md`, "The values (v2.4)"): a template whose `{holes}` take their inputs' words, and a
//! list's elements one after another. A value says its words by its type: a number its digits, a percentage "90 %", a
//! duration "1 min 30", a time "23:00", a text itself; a game thing and yes or no as the run knows them in the player's
//! language (`Knows::words`), else by its id, as JSON.

use std::collections::HashMap;

use serde_json::Value;

use super::super::super::behaviour::{Behaviour, Cx, Fault};
use crate::bot::pins::holes;
use crate::bot::variable::Shape;
use crate::bot::{Type, ValueType};

/// The words of `value`, of `of` if known: a list's elements' words separated by ", ".
fn words(cx: &Cx<'_>, of: Option<Type>, value: &Value) -> String {
    if let (Some(typed), Value::Array(elements)) = (of, value)
        && typed.shape.list
    {
        let one = Type::new(typed.of, Shape::ONE);
        let said: Vec<String> = elements.iter().map(|element| words(cx, Some(one), element)).collect();
        return said.join(", ");
    }
    match (of.map(|typed| typed.of), value) {
        // A text, a time, or a hole's own text, as written; another type held as a string (a route, a bank, items)
        // by its words.
        (None | Some(ValueType::Text | ValueType::Time), Value::String(text)) => text.clone(),
        (Some(ValueType::Percentage), Value::Number(number)) => format!("{number} %"),
        (Some(ValueType::Duration), Value::Number(number)) => {
            number.as_u64().map_or_else(|| number.to_string(), duration)
        }
        (Some(of), _) if of != ValueType::Number => cx.knows().words(of, value).unwrap_or_else(|| match value {
            Value::String(text) => text.clone(),
            _ => value.to_string(),
        }),
        _ => value.to_string(),
    }
}

/// Whole seconds as the player types them: "45 s", "2 min", "1 min 30".
fn duration(seconds: u64) -> String {
    match (seconds / 60, seconds % 60) {
        (0, rest) => format!("{rest} s"),
        (minutes, 0) => format!("{minutes} min"),
        (minutes, rest) => format!("{minutes} min {rest}"),
    }
}

/// Text: its template, each `{hole}` the words of the input of that name, read in one pass so that words a value brings
/// are never read as a hole; other braces are text.
pub(super) struct Text;

impl Behaviour for Text {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let template = cx.template().unwrap_or_default().to_owned();
        let mut said = HashMap::new();
        for hole in holes(&template) {
            let value = cx.input(&hole.0)?;
            said.insert(format!("{{{}}}", hole.0), words(cx, cx.type_of(&hole.0), &value));
        }
        let mut out = String::new();
        let mut rest = template.as_str();
        while let Some(at) = rest.find('{') {
            out.push_str(&rest[..at]);
            let hole = rest[at..].find('}').map(|end| &rest[at..=at + end]);
            if let Some((hole, text)) = hole.and_then(|hole| said.get(hole).map(|text| (hole, text))) {
                out.push_str(text);
                rest = &rest[at + hole.len()..];
            } else {
                out.push('{');
                rest = &rest[at + 1..];
            }
        }
        out.push_str(rest);
        Ok(Value::String(out))
    }
}

/// Join: its list's elements' words, its separator between each two.
pub(super) struct Join;

impl Behaviour for Join {
    fn value(&self, cx: &mut Cx<'_>, _: &str) -> Result<Value, Fault> {
        let list = cx.input("list")?;
        let elements = list
            .as_array()
            .ok_or_else(|| Fault::NotOfType { pin: "list".to_owned() })?;
        let separator = cx.input("separator")?;
        let separator = separator.as_str().unwrap_or_default();
        let of = cx.type_of("list").map(|typed| Type::new(typed.of, Shape::ONE));
        let said: Vec<String> = elements.iter().map(|element| words(cx, of, element)).collect();
        Ok(Value::String(said.join(separator)))
    }
}
