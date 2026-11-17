//! A value typed on a node's input that no wire feeds (the owner's choices of 2026-11-17): in Details, by the pin's
//! type, a Set's within its variable's bounds; one back at the pin's default, or none, is dropped, so the node reads
//! its default again and its file holds what the player changed only.

use serde_json::Value;

use super::{Refusal, node, pin_of};
use crate::bot::blueprint::{Blueprint, Does, Id};
use crate::bot::catalogue::{Carries, Side};
use crate::bot::pins::has_wildcards;
use crate::bot::variable::{Shape, Type, ValueType};

/// Types `value` on the input `pin` of `node` in the graph at `at`, or drops the one typed there for `None`.
pub(super) fn typed(
    blueprint: &mut Blueprint,
    at: usize,
    id: &Id,
    pin: &Id,
    value: Option<Value>,
    of: Option<ValueType>,
) -> Result<(), Refusal> {
    let graph = &blueprint.graphs[at];
    let found = node(graph, id)?;
    let input = pin_of(blueprint, graph, found, pin, Side::In)?;
    let open = crate::bot::pins::open_pins(blueprint, graph, found)
        .and_then(|pins| pins.into_iter().find(|it| it.side == Side::In && it.id == pin.0))
        .map_or(input.carries, |it| it.carries);
    // A wildcard of a node with no type takes the type its first value is given as, of its own shape and among those
    // its kind takes; given none, one its kind reads by default holds the value as that.
    let (value_type, typing) = match (open, input.carries) {
        (Carries::Wildcard(shape), Carries::Value(read)) => match of {
            Some(of) if crate::bot::pins::takes_type(found, of) => (Some(Type::new(of, shape)), Some(of)),
            Some(_) => (None, None),
            None => (Some(read), None),
        },
        (_, carries) => match carries {
            Carries::Value(typed) => (Some(typed), None),
            // A Text's hole is fed any value by a wire; one typed there is text, which says how it reads.
            Carries::Any => (Some(Type::new(ValueType::Text, Shape::ONE)), None),
            // A kind that changes a variable takes its type from it alone.
            Carries::Wildcard(_) if !has_wildcards(found) => (None, None),
            Carries::Wildcard(shape) => (of.map(|of| Type::new(of, shape)), of),
            Carries::Exec => return Err(Refusal::NotItsType),
        },
    };
    if graph.wires.iter().any(|wire| &wire.to.0 == id && &wire.to.1 == pin) {
        return Err(Refusal::Wired);
    }
    // A Set out of its variable's bounds would block the run, and memory would lose it at the next.
    let variable = match &found.does {
        Does::Set(variable) => crate::bot::pins::variable_of(blueprint, graph, variable),
        _ => None,
    };
    let value = value.filter(|value| Some(value) != input.default.as_ref());
    if let Some(value) = &value {
        let holds = variable.map_or_else(
            || !value.is_null() && value_type.is_some_and(|typed| typed.holds(value)),
            |variable| variable.accepts(value),
        );
        if !holds {
            return Err(Refusal::NotItsType);
        }
    }
    let Some(found) = blueprint.graphs[at].nodes.iter_mut().find(|it| &it.id == id) else {
        return Err(Refusal::NoSuchNode);
    };
    if value.is_some() && typing.is_some() {
        found.options.value_type = typing;
    }
    match value {
        Some(value) => found.inputs.insert(pin.clone(), value),
        None => found.inputs.remove(pin),
    };
    Ok(())
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::super::{Edit, edit};
    use super::*;
    use crate::bot::json::{read, tests::HARVEST};

    fn id(text: &str) -> Id {
        Id(text.into())
    }

    fn harvest() -> Blueprint {
        read(HARVEST).unwrap_or_else(|error| panic!("{error}"))
    }

    fn value(node: &str, pin: &str, value: Option<Value>) -> Edit {
        Edit::Value {
            node: id(node),
            pin: id(pin),
            value,
            of: None,
        }
    }

    fn typed_on(blueprint: &Blueprint, node: &str) -> Vec<(String, Value)> {
        let node = blueprint.graphs[0].nodes.iter().find(|it| it.id.0 == node);
        let inputs = node.map(|node| node.inputs.clone()).unwrap_or_default();
        inputs.into_iter().map(|(pin, value)| (pin.0, value)).collect()
    }

    fn done(blueprint: &mut Blueprint, change: Edit) {
        let said = format!("{change:?}");
        assert_eq!(edit(blueprint, &id("events"), change), Ok(()), "{said}");
    }

    #[test]
    fn a_value_typed_on_an_input_is_kept_and_one_back_at_its_default_dropped() {
        let mut blueprint = harvest();
        done(
            &mut blueprint,
            Edit::Add {
                does: crate::bot::Does::Kind(id("notify")),
                position: [0, 0],
                wired_to: None,
            },
        );
        done(&mut blueprint, value("notify", "text", Some(json!("Objectif atteint"))));
        assert_eq!(
            typed_on(&blueprint, "notify"),
            [("text".to_owned(), json!("Objectif atteint"))]
        );
        done(&mut blueprint, value("notify", "text", Some(json!(""))));
        assert_eq!(
            typed_on(&blueprint, "notify"),
            [("text".to_owned(), json!(""))],
            "no default to go back to"
        );
        done(&mut blueprint, value("notify", "text", None));
        assert_eq!(typed_on(&blueprint, "notify"), []);
        done(&mut blueprint, value("n3", "pods", Some(json!(80))));
        assert!(typed_on(&blueprint, "n3").contains(&("pods".to_owned(), json!(80))));
        done(&mut blueprint, value("n3", "pods", Some(json!(90))));
        done(&mut blueprint, value("n3", "what", Some(json!("all"))));
        assert_eq!(typed_on(&blueprint, "n3"), [], "both at their pins' defaults");
        done(&mut blueprint, value("n4", "bank", Some(json!({"map": 191_104_002}))));
        assert_eq!(
            typed_on(&blueprint, "n4"),
            [("bank".to_owned(), json!({"map": 191_104_002}))],
            "a call's input"
        );
    }

    #[test]
    fn a_value_wired_of_another_type_or_out_of_its_bounds_is_refused_and_changes_nothing() {
        let mut blueprint = harvest();
        done(
            &mut blueprint,
            Edit::Add {
                does: crate::bot::Does::Set(id("pods")),
                position: [0, 0],
                wired_to: None,
            },
        );
        let before = blueprint.clone();
        for (change, why) in [
            (value("n3", "zone", Some(json!({"sub_area": 445}))), Refusal::Wired),
            (value("n3", "pods", Some(json!("80"))), Refusal::NotItsType),
            (value("n3", "pods", Some(json!(101))), Refusal::NotItsType),
            (value("n3", "pods", Some(Value::Null)), Refusal::NotItsType),
            (value("set_pods", "value", Some(json!(5))), Refusal::NotItsType),
            (value("n3", "run", Some(json!(1))), Refusal::NotItsType),
            (value("n3", "count", Some(json!(1))), Refusal::WrongWay),
            (value("n3", "nothing", Some(json!(1))), Refusal::NoSuchPin),
            (value("n9", "pods", Some(json!(1))), Refusal::NoSuchNode),
        ] {
            assert_eq!(
                edit(&mut blueprint, &id("events"), change.clone()),
                Err(why),
                "{change:?}"
            );
        }
        assert_eq!(blueprint, before, "a refusal changes nothing");
        done(&mut blueprint, value("set_pods", "value", Some(json!(10))));
    }
}
