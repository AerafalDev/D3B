//! A node's own pins edited (`docs/bot.md`, "The flow's kinds (v2.3)", the owner's choices of 2026-11-17): Sequence's
//! outputs counted, Switch's cases typed, and a numbered pin or a case removed, the wires and values of the pins after
//! it renumbered, as Unreal does. Each is one change, undone in one step.

use std::num::NonZeroU8;

use serde_json::Value;

use super::{Refusal, node, untype};
use crate::bot::blueprint::{Blueprint, Does, End, Graph, Id, Node, Wire};
use crate::bot::catalogue::{self, Side, Takes};
use crate::bot::pins::{has_wildcards, takes_type};
use crate::bot::variable::{Shape, Type, ValueType};

/// What a node's options make: numbered pins `<pin>_<n>` on a side, at least `least` of them, or a case each.
#[derive(Clone, Copy)]
enum Numbered {
    Count { pin: &'static str, side: Side, least: u8 },
    Cases,
}

impl Numbered {
    fn prefix(self) -> &'static str {
        match self {
            Self::Count { pin, .. } => pin,
            Self::Cases => "case",
        }
    }

    fn side(self) -> Side {
        match self {
            Self::Count { side, .. } => side,
            Self::Cases => Side::Out,
        }
    }

    fn least(self) -> usize {
        match self {
            Self::Count { least, .. } => least.into(),
            Self::Cases => 0,
        }
    }

    /// How many pins `node` has of it.
    fn len(self, node: &Node) -> usize {
        match self {
            Self::Count { least, .. } => usize::from(node.options.count.map_or(least, NonZeroU8::get)),
            Self::Cases => node.options.cases.len(),
        }
    }

    /// The number of a pin named as this kind's, `then_3`, in range or not; `then_03` and `then_+3` are none's.
    fn number_of(self, pin: &Id) -> Option<usize> {
        let rest = pin.0.strip_prefix(self.prefix())?.strip_prefix('_')?;
        let written = rest.bytes().all(|byte| byte.is_ascii_digit()) && !rest.starts_with('0');
        rest.parse().ok().filter(|_| written)
    }

    /// The pin's number, from 1, when `pin` is one of `node`'s.
    fn number(self, node: &Node, pin: &Id) -> Option<usize> {
        self.number_of(pin)
            .filter(|number| (1..=self.len(node)).contains(number))
    }
}

/// What `node`'s kind numbers.
fn numbered(node: &Node) -> Vec<Numbered> {
    let Does::Kind(kind) = &node.does else {
        return Vec::new();
    };
    let takes = catalogue::kind(&kind.0).map(|kind| kind.takes).unwrap_or_default();
    takes
        .iter()
        .filter_map(|takes| match *takes {
            Takes::Count { pin, least } => Some(Numbered::Count {
                pin: pin.id,
                side: pin.side,
                least,
            }),
            Takes::Cases => Some(Numbered::Cases),
            _ => None,
        })
        .collect()
}

/// `count` numbered pins on `id`, or its cases made `cases`, each a value of the node's type, or of `of` for a node
/// that notes none yet. The wires and values of the pins no longer there go with them; a case given twice is a
/// problem the Details marks, not a refusal.
pub(super) fn set(
    blueprint: &mut Blueprint,
    at: usize,
    id: &Id,
    count: Option<NonZeroU8>,
    cases: Option<Vec<Value>>,
    of: Option<ValueType>,
) -> Result<(), Refusal> {
    let found = node(&blueprint.graphs[at], id)?;
    let kinds = numbered(found);
    let counted = kinds.iter().copied().find(|it| matches!(it, Numbered::Count { .. }));
    let takes_cases = kinds.iter().any(|it| matches!(it, Numbered::Cases));
    if (count.is_some() && counted.is_none()) || (cases.is_some() && !takes_cases) {
        return Err(Refusal::NotAnOption);
    }
    if let (Some(count), Some(counted)) = (count, counted)
        && usize::from(count.get()) < counted.least()
    {
        return Err(Refusal::BelowLeast);
    }
    let typing = match (&cases, found.options.value_type) {
        (Some(cases), None) if !cases.is_empty() => match of {
            Some(of) if has_wildcards(found) && takes_type(found, of) => Some(of),
            _ => return Err(Refusal::NotItsType),
        },
        _ => None,
    };
    if let Some(cases) = &cases {
        let of = found.options.value_type.or(typing);
        let holds = |case: &Value| !case.is_null() && of.is_some_and(|of| Type::new(of, Shape::ONE).holds(case));
        if !cases.iter().all(holds) {
            return Err(Refusal::NotItsType);
        }
    }
    let graph = &mut blueprint.graphs[at];
    let Some(found) = graph.nodes.iter_mut().find(|it| &it.id == id) else {
        return Err(Refusal::NoSuchNode);
    };
    if let (Some(count), Some(Numbered::Count { least, .. })) = (count, counted) {
        found.options.count = (count.get() != least).then_some(count);
    }
    if let Some(cases) = cases {
        found.options.cases = cases;
    }
    if typing.is_some() {
        found.options.value_type = typing;
    }
    let found = found.clone();
    for kind in kinds {
        let kept = kind.len(&found);
        drop_past(graph, &found.id, kind, kept);
    }
    untype(graph);
    Ok(())
}

/// A Text's template made `text`: its holes its inputs, each a hole no longer there dropping its wire and its value; an
/// empty template is none.
pub(super) fn template(blueprint: &mut Blueprint, at: usize, id: &Id, text: String) -> Result<(), Refusal> {
    let found = node(&blueprint.graphs[at], id)?;
    let takes = match &found.does {
        Does::Kind(kind) => catalogue::kind(&kind.0).is_some_and(|kind| kind.takes.contains(&Takes::Text)),
        _ => false,
    };
    if !takes {
        return Err(Refusal::NotAnOption);
    }
    let holes = crate::bot::pins::holes(&text);
    let graph = &mut blueprint.graphs[at];
    let Some(found) = graph.nodes.iter_mut().find(|it| &it.id == id) else {
        return Err(Refusal::NoSuchNode);
    };
    found.options.text = (!text.is_empty()).then_some(text);
    found.inputs.retain(|pin, _| holes.contains(pin));
    graph
        .wires
        .retain(|wire| &wire.to.0 != id || holes.contains(&wire.to.1));
    // The node a removed hole's wire came from may use its type no more.
    untype(graph);
    Ok(())
}

/// The numbered pin or case `pin` of `id` gone, with its wire and value, the pins after it each taking the number
/// before, their wires and values with them; refused below the kind's least.
pub(super) fn remove(blueprint: &mut Blueprint, at: usize, id: &Id, pin: &Id) -> Result<(), Refusal> {
    let found = node(&blueprint.graphs[at], id)?;
    let Some((kind, number)) = numbered(found)
        .into_iter()
        .find_map(|kind| kind.number(found, pin).map(|number| (kind, number)))
    else {
        return Err(Refusal::NotAnOption);
    };
    let len = kind.len(found);
    if len <= kind.least() {
        return Err(Refusal::BelowLeast);
    }
    let graph = &mut blueprint.graphs[at];
    let Some(found) = graph.nodes.iter_mut().find(|it| &it.id == id) else {
        return Err(Refusal::NoSuchNode);
    };
    match kind {
        Numbered::Count { least, .. } => {
            let count = u8::try_from(len - 1).ok().and_then(NonZeroU8::new);
            found.options.count = count.filter(|count| count.get() != least);
        }
        Numbered::Cases => {
            found.options.cases.remove(number - 1);
        }
    }
    let name = |number: usize| Id(format!("{}_{number}", kind.prefix()));
    found.inputs.remove(pin);
    for later in number + 1..=len {
        if let Some(value) = found.inputs.remove(&name(later)) {
            found.inputs.insert(name(later - 1), value);
        }
    }
    let side = kind.side();
    graph.wires.retain(|wire| !on(wire_end(wire, side), id, pin));
    for wire in &mut graph.wires {
        let end = wire_end_mut(wire, side);
        if end.0 != *id {
            continue;
        }
        if let Some(later) = kind.number_of(&end.1)
            && later > number
        {
            end.1 = name(later - 1);
        }
    }
    untype(graph);
    Ok(())
}

fn wire_end(wire: &Wire, side: Side) -> &End {
    if side == Side::Out { &wire.from } else { &wire.to }
}

fn wire_end_mut(wire: &mut Wire, side: Side) -> &mut End {
    if side == Side::Out {
        &mut wire.from
    } else {
        &mut wire.to
    }
}

fn on(end: &End, node: &Id, pin: &Id) -> bool {
    &end.0 == node && &end.1 == pin
}

/// The wires and values of `node`'s pins of `kind` numbered past `kept`.
fn drop_past(graph: &mut Graph, node: &Id, kind: Numbered, kept: usize) {
    let past = |pin: &Id| kind.number_of(pin).is_some_and(|number| number > kept);
    let side = kind.side();
    graph.wires.retain(|wire| {
        let end = wire_end(wire, side);
        !(&end.0 == node && past(&end.1))
    });
    if let Some(found) = graph.nodes.iter_mut().find(|it| &it.id == node) {
        found.inputs.retain(|pin, _| !past(pin));
    }
}

#[cfg(test)]
mod tests {
    use serde_json::json;

    use super::super::{Edit, edit};
    use super::*;
    use crate::bot::json::read;

    fn id(text: &str) -> Id {
        Id(text.into())
    }

    /// On start → a Sequence of three, each output to a Notify; a Switch on professions with two cases, each wired.
    fn flows() -> Blueprint {
        read(
            r#"{"schema": 2, "id": "f", "name": "F", "version": 1, "graphs": [{"id": "events", "name": "Events",
  "nodes": [{"id": "start", "kind": "on_start"}, {"id": "seq", "kind": "sequence", "options": {"count": 3}},
            {"id": "a", "kind": "notify"}, {"id": "b", "kind": "notify"}, {"id": "c", "kind": "notify"},
            {"id": "sw", "kind": "switch", "options": {"type": "job", "cases": [24, 28]}, "inputs": {"value": 24}}],
  "wires": [{"from": ["start", "then"], "to": ["seq", "run"]}, {"from": ["seq", "then_1"], "to": ["a", "run"]},
            {"from": ["seq", "then_2"], "to": ["b", "run"]}, {"from": ["seq", "then_3"], "to": ["c", "run"]},
            {"from": ["sw", "case_1"], "to": ["a", "run"]}, {"from": ["sw", "case_2"], "to": ["b", "run"]},
            {"from": ["sw", "otherwise"], "to": ["c", "run"]}]}]}"#,
        )
        .unwrap_or_else(|error| panic!("{error}"))
    }

    fn options(node: &str, count: Option<u8>, cases: Option<Vec<Value>>, of: Option<ValueType>) -> Edit {
        Edit::Options {
            node: id(node),
            count: count.and_then(NonZeroU8::new),
            cases,
            of,
        }
    }

    fn remove_pin(node: &str, pin: &str) -> Edit {
        Edit::RemovePin {
            node: id(node),
            pin: id(pin),
        }
    }

    fn change(blueprint: &mut Blueprint, change: Edit) -> Result<(), Refusal> {
        edit(blueprint, &id("events"), change)
    }

    /// The wires out of `node`, as `pin → node`.
    fn outs(blueprint: &Blueprint, node: &str) -> Vec<String> {
        let wires = blueprint.graphs[0].wires.iter().filter(|wire| wire.from.0.0 == node);
        wires
            .map(|wire| format!("{} → {}", wire.from.1.0, wire.to.0.0))
            .collect()
    }

    fn the<'b>(blueprint: &'b Blueprint, node: &str) -> &'b Node {
        let found = blueprint.graphs[0].nodes.iter().find(|it| it.id.0 == node);
        found.unwrap_or_else(|| panic!("{node}"))
    }

    #[test]
    fn an_output_added_to_a_sequence_and_fewer_drop_the_wires_past_them() {
        let mut blueprint = flows();
        assert_eq!(change(&mut blueprint, options("seq", Some(4), None, None)), Ok(()));
        assert_eq!(the(&blueprint, "seq").options.count, NonZeroU8::new(4));
        assert_eq!(change(&mut blueprint, options("seq", Some(2), None, None)), Ok(()));
        assert_eq!(the(&blueprint, "seq").options.count, None, "its least, written as none");
        assert_eq!(outs(&blueprint, "seq"), ["then_1 → a", "then_2 → b"]);
    }

    #[test]
    fn a_pin_removed_moves_the_wires_after_it_up_and_none_goes_below_the_least() {
        let mut blueprint = flows();
        assert_eq!(change(&mut blueprint, remove_pin("seq", "then_2")), Ok(()));
        assert_eq!(outs(&blueprint, "seq"), ["then_1 → a", "then_2 → c"]);
        assert_eq!(the(&blueprint, "seq").options.count, None);
        let before = blueprint.clone();
        for (refused, why) in [
            (remove_pin("seq", "then_1"), Refusal::BelowLeast),
            (options("seq", Some(1), None, None), Refusal::BelowLeast),
            (remove_pin("seq", "then_3"), Refusal::NotAnOption),
            (remove_pin("seq", "then_01"), Refusal::NotAnOption),
            (remove_pin("seq", "then_+2"), Refusal::NotAnOption),
            (remove_pin("seq", "run"), Refusal::NotAnOption),
            (options("a", Some(3), None, None), Refusal::NotAnOption),
            (options("seq", None, Some(vec![json!(24)]), None), Refusal::NotAnOption),
            (remove_pin("nowhere", "then_1"), Refusal::NoSuchNode),
        ] {
            assert_eq!(change(&mut blueprint, refused.clone()), Err(why), "{refused:?}");
        }
        assert_eq!(blueprint, before, "a refusal changes nothing");
    }

    #[test]
    fn an_input_removed_moves_the_values_and_wires_after_it_up() {
        let mut blueprint = flows();
        let list = r#"{"id": "list", "kind": "make_list", "options": {"count": 3, "type": "number"}, "inputs": {"in_1": 1, "in_2": 2}}"#;
        let list = serde_json::from_str(list).unwrap_or_else(|error| panic!("{error}"));
        blueprint.graphs[0].nodes.push(list);
        let level = serde_json::from_str(r#"{"id": "lv", "kind": "level"}"#).unwrap_or_else(|error| panic!("{error}"));
        blueprint.graphs[0].nodes.push(level);
        let wire = Edit::Wire {
            from: End(id("lv"), id("value")),
            to: End(id("list"), id("in_3")),
        };
        assert_eq!(change(&mut blueprint, wire), Ok(()));
        assert_eq!(change(&mut blueprint, remove_pin("list", "in_1")), Ok(()));
        let list = the(&blueprint, "list");
        assert_eq!(list.inputs.iter().collect::<Vec<_>>(), [(&id("in_1"), &json!(2))]);
        let into = blueprint.graphs[0].wires.iter().filter(|wire| wire.to.0.0 == "list");
        assert_eq!(into.map(|wire| wire.to.1.0.as_str()).collect::<Vec<_>>(), ["in_2"]);
        assert_eq!(list.options.count, NonZeroU8::new(2));
        assert_eq!(
            list.options.value_type,
            Some(ValueType::Number),
            "still typed by its inputs"
        );
    }

    #[test]
    fn a_template_makes_its_holes_and_a_hole_gone_takes_its_wire_and_value() {
        let mut blueprint = flows();
        let text = r#"{"id": "tx", "kind": "text", "options": {"text": "{n} {who}"}, "inputs": {"who": "Lyra"}}"#;
        let level = r#"{"id": "lv", "kind": "level"}"#;
        for node in [text, level] {
            blueprint.graphs[0]
                .nodes
                .push(serde_json::from_str(node).unwrap_or_else(|error| panic!("{error}")));
        }
        let wire = Edit::Wire {
            from: End(id("lv"), id("value")),
            to: End(id("tx"), id("n")),
        };
        assert_eq!(change(&mut blueprint, wire), Ok(()));
        let renamed = Edit::Text {
            node: id("tx"),
            text: "{count} {who}".to_owned(),
        };
        assert_eq!(change(&mut blueprint, renamed), Ok(()));
        assert!(
            !blueprint.graphs[0].wires.iter().any(|wire| wire.to.0.0 == "tx"),
            "the hole n gone, its wire with it"
        );
        assert_eq!(the(&blueprint, "tx").inputs.len(), 1, "the hole who kept");
        let emptied = Edit::Text {
            node: id("tx"),
            text: String::new(),
        };
        assert_eq!(change(&mut blueprint, emptied), Ok(()));
        assert_eq!(
            (
                the(&blueprint, "tx").options.text.clone(),
                the(&blueprint, "tx").inputs.len()
            ),
            (None, 0)
        );
        // A Choose typed only by its wire into a hole: the hole gone, its type goes too.
        let choose = r#"{"id": "pick", "kind": "choose", "options": {"type": "zone"},
            "inputs": {"condition": true, "if_true": {"sub_area": 445}}}"#;
        blueprint.graphs[0]
            .nodes
            .push(serde_json::from_str(choose).unwrap_or_else(|error| panic!("{error}")));
        let back = Edit::Text {
            node: id("tx"),
            text: "{z}".to_owned(),
        };
        assert_eq!(change(&mut blueprint, back), Ok(()));
        let into = Edit::Wire {
            from: End(id("pick"), id("value")),
            to: End(id("tx"), id("z")),
        };
        assert_eq!(change(&mut blueprint, into), Ok(()));
        let cleared = Edit::Value {
            node: id("pick"),
            pin: id("if_true"),
            value: None,
            of: None,
        };
        assert_eq!(change(&mut blueprint, cleared), Ok(()));
        assert_eq!(
            the(&blueprint, "pick").options.value_type,
            Some(ValueType::Zone),
            "its wire still uses it"
        );
        let done = Edit::Text {
            node: id("tx"),
            text: "done".to_owned(),
        };
        assert_eq!(change(&mut blueprint, done), Ok(()));
        assert_eq!(the(&blueprint, "pick").options.value_type, None, "its type unused");
        let elsewhere = Edit::Text {
            node: id("seq"),
            text: "{x}".to_owned(),
        };
        assert_eq!(change(&mut blueprint, elsewhere), Err(Refusal::NotAnOption));
    }

    #[test]
    fn a_case_removed_takes_its_value_and_wire_and_the_next_moves_up() {
        let mut blueprint = flows();
        assert_eq!(change(&mut blueprint, remove_pin("sw", "case_1")), Ok(()));
        assert_eq!(the(&blueprint, "sw").options.cases, [json!(28)]);
        assert_eq!(outs(&blueprint, "sw"), ["case_1 → b", "otherwise → c"]);
        assert_eq!(change(&mut blueprint, remove_pin("sw", "case_1")), Ok(()));
        assert_eq!(outs(&blueprint, "sw"), ["otherwise → c"]);
        assert_eq!(
            change(&mut blueprint, remove_pin("sw", "case_1")),
            Err(Refusal::NotAnOption)
        );
        assert_eq!(
            the(&blueprint, "sw").options.value_type,
            Some(ValueType::Job),
            "its value still types it"
        );
    }

    #[test]
    fn cases_are_typed_by_the_switch_or_type_it_and_one_not_of_its_type_is_refused() {
        let mut blueprint = flows();
        let three = Some(vec![json!(24), json!(28), json!(24)]);
        assert_eq!(
            change(&mut blueprint, options("sw", None, three, None)),
            Ok(()),
            "twice is a problem"
        );
        assert_eq!(
            change(&mut blueprint, options("sw", None, Some(vec![json!(24)]), None)),
            Ok(())
        );
        assert_eq!(outs(&blueprint, "sw"), ["case_1 → a", "otherwise → c"]);
        let before = blueprint.clone();
        for refused in [
            options("sw", None, Some(vec![json!("bûcheron")]), None),
            options("sw", None, Some(vec![Value::Null]), None),
        ] {
            assert_eq!(change(&mut blueprint, refused), Err(Refusal::NotItsType));
        }
        assert_eq!(blueprint, before);
        // A Switch with no type takes the one its first cases are given as, among any.
        let mut fresh = flows();
        let untyped = fresh.graphs[0].nodes.iter_mut().find(|it| it.id.0 == "sw");
        if let Some(node) = untyped {
            node.options.value_type = None;
            node.options.cases.clear();
            node.inputs.clear();
        }
        let one = Some(vec![json!(5)]);
        assert_eq!(
            change(&mut fresh, options("sw", None, one.clone(), None)),
            Err(Refusal::NotItsType)
        );
        assert_eq!(
            change(&mut fresh, options("sw", None, one, Some(ValueType::Number))),
            Ok(())
        );
        assert_eq!(the(&fresh, "sw").options.value_type, Some(ValueType::Number));
        assert_eq!(change(&mut fresh, options("sw", None, Some(Vec::new()), None)), Ok(()));
        assert_eq!(
            the(&fresh, "sw").options.value_type,
            None,
            "no case left, nothing uses its type"
        );
    }
}
