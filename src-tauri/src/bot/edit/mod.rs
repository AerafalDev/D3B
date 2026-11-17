//! The editor's changes to a blueprint (`docs/bot.md`, "In the app"), the owner's choices of 2026-11-16: a wire checked
//! by what its pins carry, which replaces the wire that fed its input or left its flow output, an input having one
//! value and a flow one way on; a node the picker offers, under an id of its own, wired to the pin it came from; nodes
//! moved; nodes and wires deleted, a node's wires with it (step 3's PR 13); a value typed on an input (`value`); my
//! variables created, changed and deleted (`variable`); the blueprint renamed (`about`). Each is done whole or refused
//! with nothing changed; `storage` saves it at once and keeps what it replaced, for undo.

mod about;
mod choices;
mod value;
mod variable;

use std::collections::BTreeMap;

pub(crate) use choices::choices;

use super::blueprint::{Blueprint, Does, End, Graph, Id, Node, Wire};
use super::catalogue::{Carries, Side};
use super::pins::{NodePin, has_wildcards, pins, uses_wildcards};
use super::variable::{ValueType, Variable};

/// One change the editor makes to a graph.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Edit {
    /// A wire from an output to an input.
    Wire { from: End, to: End },
    /// A node the picker offers, where it was opened, wired to the pin a wire dropped on the ground came from.
    Add {
        does: Does,
        position: [i32; 2],
        wired_to: Option<(End, Side)>,
    },
    /// Nodes put elsewhere, dragged together: one change, undone at once.
    Move { nodes: Vec<(Id, [i32; 2])> },
    /// Nodes, with every wire to or from them, and wires, chosen together.
    Delete { nodes: Vec<Id>, wires: Vec<Wire> },
    /// A value typed on an input no wire feeds, or none to go back to the pin's default; on a wildcard of a node with no
    /// type yet, as a value of `of`, which its node then notes, since a value alone does not say its type.
    Value {
        node: Id,
        pin: Id,
        value: Option<serde_json::Value>,
        of: Option<ValueType>,
    },
    /// A variable of mine named so, under an id made from its name; the graph it is sent from does not matter.
    NewVariable { name: String },
    /// The variable of its id made this one, whole: its name, type, default, bounds, help, set per character, memory.
    Variable(Variable),
    /// A variable gone, with the nodes that read or write it.
    DeleteVariable { id: Id },
    /// The blueprint named and described so, its id kept.
    About { name: String, description: String },
}

/// Why an edit is refused. The editor offers none of these; a refusal says the webview and the file disagree.
#[derive(Debug, Clone, Copy, PartialEq, Eq, thiserror::Error)]
pub(crate) enum Refusal {
    #[error("the blueprint has no such graph")]
    NoSuchGraph,
    #[error("the graph has no such node")]
    NoSuchNode,
    #[error("the graph has no such wire")]
    NoSuchWire,
    #[error("the node has no such pin")]
    NoSuchPin,
    #[error("a wire goes from an output to an input")]
    WrongWay,
    #[error("the output carries what the input does not take")]
    OtherType,
    #[error("a wire joins two nodes")]
    SameNode,
    #[error("the picker does not offer it here")]
    NotAChoice,
    #[error("a wire feeds the input")]
    Wired,
    #[error("the value is not of the input's type, or out of its bounds")]
    NotItsType,
    #[error("the blueprint has no such variable")]
    NoSuchVariable,
    #[error("a name holds more than spaces")]
    Blank,
}

/// Makes `edit` in `blueprint`'s graph `graph`, or refuses it and changes nothing.
pub(crate) fn edit(blueprint: &mut Blueprint, graph: &Id, edit: Edit) -> Result<(), Refusal> {
    // A variable is the blueprint's, whichever graph shows.
    match edit {
        Edit::NewVariable { name } => return variable::create(blueprint, &name).map(drop),
        Edit::Variable(changed) => return variable::change(blueprint, changed),
        Edit::DeleteVariable { id } => return variable::delete(blueprint, &id),
        Edit::About { name, description } => return about::rename(blueprint, &name, &description),
        _ => {}
    }
    let at = blueprint
        .graphs
        .iter()
        .position(|it| &it.id == graph)
        .ok_or(Refusal::NoSuchGraph)?;
    let shown = &blueprint.graphs[at];
    match edit {
        Edit::Wire { from, to } => {
            let link = checked(blueprint, shown, from, to)?;
            join(&mut blueprint.graphs[at], link);
        }
        Edit::Add {
            does,
            position,
            wired_to,
        } => {
            if !choices(blueprint, shown).contains(&does) {
                return Err(Refusal::NotAChoice);
            }
            let node = Node {
                id: free_id(shown, &does),
                does,
                inputs: BTreeMap::new(),
                event: None,
                options: super::blueprint::Options::default(),
            };
            let link = wired_to
                .map(|(end, side)| to_new(blueprint, shown, &node, end, side))
                .transpose()?;
            let id = node.id.clone();
            blueprint.graphs[at].nodes.push(node);
            if let Some(link) = link {
                join(&mut blueprint.graphs[at], link);
            }
            blueprint.layout.entry(graph.clone()).or_default().insert(id, position);
        }
        Edit::Move { nodes } => {
            if !nodes
                .iter()
                .all(|(node, _)| shown.nodes.iter().any(|it| &it.id == node))
            {
                return Err(Refusal::NoSuchNode);
            }
            // None moved is no change, and leaves no empty layout behind.
            if !nodes.is_empty() {
                blueprint.layout.entry(graph.clone()).or_default().extend(nodes);
            }
        }
        Edit::Delete { nodes, wires } => {
            if !nodes.iter().all(|node| shown.nodes.iter().any(|it| &it.id == node)) {
                return Err(Refusal::NoSuchNode);
            }
            if !wires.iter().all(|wire| shown.wires.contains(wire)) {
                return Err(Refusal::NoSuchWire);
            }
            let gone = |end: &End| nodes.contains(&end.0);
            let shown = &mut blueprint.graphs[at];
            shown.nodes.retain(|it| !nodes.contains(&it.id));
            shown
                .wires
                .retain(|it| !wires.contains(it) && !gone(&it.from) && !gone(&it.to));
            untype(shown);
            if let Some(places) = blueprint.layout.get_mut(graph) {
                places.retain(|node, _| !nodes.contains(node));
                if places.is_empty() {
                    blueprint.layout.remove(graph);
                }
            }
        }
        Edit::Value { node, pin, value, of } => {
            value::typed(blueprint, at, &node, &pin, value, of)?;
            untype(&mut blueprint.graphs[at]);
        }
        Edit::NewVariable { .. } | Edit::Variable(_) | Edit::DeleteVariable { .. } | Edit::About { .. } => {}
    }
    Ok(())
}

/// A wire checked: whether it carries a value rather than the flow, and the type it gives a wildcard's node.
struct Link {
    wire: Wire,
    value: bool,
    typed: Option<(Id, ValueType)>,
}

/// The link `wire` makes from an output carrying `out` into an input carrying `input`: one feeding the other, or a value
/// giving its type to a wildcard of its shape, whose node notes it. `None` when they do not fit.
fn link(wire: Wire, out: Carries, input: Carries) -> Option<Link> {
    let typed = if out.feeds(input) {
        None
    } else {
        let of = out.types(input)?;
        let wildcard = if matches!(out, Carries::Wildcard(_)) {
            &wire.from
        } else {
            &wire.to
        };
        Some((wildcard.0.clone(), of))
    };
    Some(Link {
        value: input.is_value(),
        typed,
        wire,
    })
}

/// Whether a wire from `out` into `input` fits.
fn fits(out: Carries, input: Carries) -> bool {
    out.feeds(input) || out.types(input).is_some()
}

/// Drops the type a node notes for its wildcards once no wire and no value typed uses one of them.
pub(super) fn untype(graph: &mut Graph) {
    let unused: Vec<Id> = graph
        .nodes
        .iter()
        .filter(|node| node.options.value_type.is_some() && !uses_wildcards(graph, node))
        .map(|node| node.id.clone())
        .collect();
    for node in graph.nodes.iter_mut().filter(|node| unused.contains(&node.id)) {
        node.options.value_type = None;
    }
}

fn node<'g>(graph: &'g Graph, id: &Id) -> Result<&'g Node, Refusal> {
    graph.nodes.iter().find(|it| &it.id == id).ok_or(Refusal::NoSuchNode)
}

/// The pin `pin` of `node` on `side`; one on the other side only goes the wrong way.
fn pin_of(blueprint: &Blueprint, graph: &Graph, node: &Node, pin: &Id, side: Side) -> Result<NodePin, Refusal> {
    let pins = pins(blueprint, graph, node).ok_or(Refusal::NoSuchPin)?;
    let on = |side: Side| pins.iter().find(|it| it.side == side && it.id == pin.0).cloned();
    let other = if side == Side::In { Side::Out } else { Side::In };
    on(side).ok_or(if on(other).is_some() {
        Refusal::WrongWay
    } else {
        Refusal::NoSuchPin
    })
}

fn checked(blueprint: &Blueprint, graph: &Graph, from: End, to: End) -> Result<Link, Refusal> {
    let out = pin_of(blueprint, graph, node(graph, &from.0)?, &from.1, Side::Out)?;
    let input = pin_of(blueprint, graph, node(graph, &to.0)?, &to.1, Side::In)?;
    if from.0 == to.0 {
        return Err(Refusal::SameNode);
    }
    // A wire that feeds as the pins read wins, so a percentage into an untyped `≥`'s numbers notes no type and a
    // number may follow it; else a wildcard still open takes the type of the other end.
    let wire = Wire { from, to };
    let fed = out.carries.feeds(input.carries).then(|| Link {
        value: input.carries.is_value(),
        typed: None,
        wire: wire.clone(),
    });
    let link = if let Some(link) = fed {
        link
    } else {
        let out_open = open_of(blueprint, graph, &wire.from, Side::Out).unwrap_or(out.carries);
        let in_open = open_of(blueprint, graph, &wire.to, Side::In).unwrap_or(input.carries);
        link(wire, out_open, in_open).ok_or(Refusal::OtherType)?
    };
    typeable(graph, &link)?;
    Ok(link)
}

/// What the pin at `end` carries as the editor's check sees it (`open_pins`), a wire into an input replacing its own.
fn open_of(blueprint: &Blueprint, graph: &Graph, end: &End, side: Side) -> Option<Carries> {
    let node = graph.nodes.iter().find(|it| it.id == end.0)?;
    let replacing = (side == Side::In).then_some(&end.1);
    let pins = super::pins::open_pins(blueprint, graph, node, replacing)?;
    pins.into_iter()
        .find(|it| it.side == side && it.id == end.1.0)
        .map(|it| it.carries)
}

/// Refuses a link that would type a kind that changes a variable, which takes its type from it alone, or give a kind's
/// wildcards a type it does not take: `≥` of zones.
fn typeable(graph: &Graph, link: &Link) -> Result<(), Refusal> {
    let Some((id, of)) = &link.typed else { return Ok(()) };
    match graph.nodes.iter().find(|it| &it.id == id) {
        Some(node) if !has_wildcards(node) || !super::pins::takes_type(node, *of) => Err(Refusal::OtherType),
        _ => Ok(()),
    }
}

/// The wire between the pin a wire dropped on the ground came from and `new`'s first pin that fits it.
fn to_new(blueprint: &Blueprint, graph: &Graph, new: &Node, end: End, side: Side) -> Result<Link, Refusal> {
    let from = node(graph, &end.0)?;
    let resolved = pin_of(blueprint, graph, from, &end.1, side)?.carries;
    let dragged = open_of(blueprint, graph, &end, side).unwrap_or(resolved);
    let theirs = super::pins::open_pins(blueprint, graph, new, None).unwrap_or_default();
    let at = |pin: &NodePin| End(new.id.clone(), Id(pin.id.clone()));
    // A link that types a wildcard gives it a type its kind takes, whichever end it is, so another pin is tried.
    let fit = |out: Carries, input: Carries, new_is_out: bool| {
        let new_end = if new_is_out { out } else { input };
        let typed = if matches!(new_end, Carries::Wildcard(_)) {
            new
        } else {
            from
        };
        fits(out, input)
            && out
                .types(input)
                .is_none_or(|of| has_wildcards(typed) && super::pins::takes_type(typed, of))
    };
    let made = if side == Side::Out {
        let pin = theirs
            .iter()
            .find(|it| it.side == Side::In && fit(dragged, it.carries, false));
        pin.and_then(|pin| link(Wire { from: end, to: at(pin) }, dragged, pin.carries))
    } else {
        let pin = theirs
            .iter()
            .find(|it| it.side == Side::Out && fit(it.carries, dragged, true));
        pin.and_then(|pin| link(Wire { from: at(pin), to: end }, pin.carries, dragged))
    };
    made.ok_or(Refusal::OtherType)
}

/// Adds `link`'s wire in place of the one that fed its input, with the value typed there, or that left its flow output;
/// the wildcard it types notes its type, and one the wire replaced may no longer be used.
fn join(graph: &mut Graph, Link { wire, value, typed }: Link) {
    if value {
        graph.wires.retain(|it| it.to != wire.to);
        if let Some(node) = graph.nodes.iter_mut().find(|it| it.id == wire.to.0) {
            node.inputs.remove(&wire.to.1);
        }
    } else {
        graph.wires.retain(|it| it.from != wire.from);
    }
    if let Some((id, of)) = typed
        && let Some(node) = graph.nodes.iter_mut().find(|it| it.id == id)
    {
        node.options.value_type = Some(of);
    }
    graph.wires.push(wire);
    untype(graph);
}

/// An id no node of `graph` holds, nor a wire names, read from what the node does: `notify`, `notify_2`, `get_zone`…
/// A wire to a node gone is its own problem; a new node under that id would make it real without the player seeing.
fn free_id(graph: &Graph, does: &Does) -> Id {
    let base = match does {
        Does::Kind(kind) => kind.0.clone(),
        Does::Get(variable) => format!("get_{}", variable.0),
        Does::Set(variable) => format!("set_{}", variable.0),
        Does::Call(function) => format!("call_{}", function.0),
    };
    // Room left for a number, so the id stays within its limit; `choices` offers only well-formed ids, which are ASCII.
    let base: String = base.chars().take(Id::LIMIT - 8).collect();
    let taken = |id: &str| {
        let named = |end: &End| end.0.0 == id;
        graph.nodes.iter().any(|node| node.id.0 == id)
            || graph.wires.iter().any(|wire| named(&wire.from) || named(&wire.to))
    };
    if !taken(&base) {
        return Id(base);
    }
    let free = (2..=u32::MAX)
        .map(|number| format!("{base}_{number}"))
        .find(|id| !taken(id));
    Id(free.unwrap_or(base))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::json::{read, tests::HARVEST};

    fn id(text: &str) -> Id {
        Id(text.into())
    }

    fn end(node: &str, pin: &str) -> End {
        End(id(node), id(pin))
    }

    fn harvest() -> Blueprint {
        read(HARVEST).unwrap_or_else(|error| panic!("{error}"))
    }

    fn wire(from: (&str, &str), to: (&str, &str)) -> Edit {
        Edit::Wire {
            from: end(from.0, from.1),
            to: end(to.0, to.1),
        }
    }

    fn add(does: Does, wired_to: Option<(&str, &str, Side)>) -> Edit {
        Edit::Add {
            does,
            position: [10, 20],
            wired_to: wired_to.map(|(node, pin, side)| (end(node, pin), side)),
        }
    }

    fn wires(blueprint: &Blueprint) -> Vec<(String, String)> {
        let text = |end: &End| format!("{}.{}", end.0.0, end.1.0);
        blueprint.graphs[0]
            .wires
            .iter()
            .map(|wire| (text(&wire.from), text(&wire.to)))
            .collect()
    }

    fn has(blueprint: &Blueprint, from: &str, to: &str) -> bool {
        wires(blueprint).contains(&(from.to_owned(), to.to_owned()))
    }

    /// Makes `change` in the event graph, which takes it.
    fn done(blueprint: &mut Blueprint, change: Edit) {
        let said = format!("{change:?}");
        assert_eq!(edit(blueprint, &id("events"), change), Ok(()), "{said}");
    }

    /// Each edit is refused, as said, and changes nothing.
    fn refused(blueprint: &mut Blueprint, refused: Vec<(Edit, Refusal)>) {
        let before = blueprint.clone();
        for (change, why) in refused {
            assert_eq!(edit(blueprint, &id("events"), change.clone()), Err(why), "{change:?}");
        }
        assert_eq!(*blueprint, before, "a refusal changes nothing");
    }

    /// The harvest with an `=` and a For each of no type yet, a query of the pods and a get of a list of zones.
    fn with_wildcards() -> Blueprint {
        let mut blueprint = harvest();
        let zones = r#"{"id": "zones", "name": "Zones", "type": "zone", "list": true}"#;
        blueprint
            .variables
            .push(serde_json::from_str(zones).unwrap_or_else(|error| panic!("{error}")));
        let more = [
            r#"{"id": "eq", "kind": "test_equal"}"#,
            r#"{"id": "each", "kind": "test_for_each"}"#,
            r#"{"id": "q", "kind": "pods_percent"}"#,
            r#"{"id": "g", "get": "zones"}"#,
        ];
        for node in more {
            let node = serde_json::from_str(node).unwrap_or_else(|error| panic!("{error}"));
            blueprint.graphs[0].nodes.push(node);
        }
        blueprint
    }

    fn typed(blueprint: &Blueprint, node: &str) -> Option<ValueType> {
        let found = blueprint.graphs[0].nodes.iter().find(|it| it.id.0 == node);
        found.and_then(|it| it.options.value_type)
    }

    fn unwire(from: (&str, &str), to: (&str, &str)) -> Edit {
        let wires = vec![Wire {
            from: end(from.0, from.1),
            to: end(to.0, to.1),
        }];
        Edit::Delete { nodes: vec![], wires }
    }

    fn value(node: &str, pin: &str, value: Option<serde_json::Value>) -> Edit {
        Edit::Value {
            node: id(node),
            pin: id(pin),
            value,
            of: None,
        }
    }

    #[test]
    fn a_wire_to_a_wildcard_types_its_node_and_its_last_wire_gone_untypes_it() {
        let mut blueprint = with_wildcards();
        done(&mut blueprint, wire(("n2", "value"), ("eq", "a")));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Zone));
        // Typed, its other wildcard takes a zone and nothing else.
        refused(
            &mut blueprint,
            vec![(wire(("q", "value"), ("eq", "b")), Refusal::OtherType)],
        );
        done(&mut blueprint, unwire(("n2", "value"), ("eq", "a")));
        assert_eq!(typed(&blueprint, "eq"), None);
        done(&mut blueprint, wire(("q", "value"), ("eq", "b")));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Percentage));
    }

    #[test]
    fn a_wildcard_output_takes_the_type_of_the_input_it_feeds_until_another_wire_replaces_it() {
        let mut blueprint = with_wildcards();
        done(&mut blueprint, wire(("each", "element"), ("n3", "zone")));
        assert_eq!(typed(&blueprint, "each"), Some(ValueType::Zone));
        assert!(has(&blueprint, "each.element", "n3.zone") && !has(&blueprint, "n2.value", "n3.zone"));
        done(&mut blueprint, wire(("n2", "value"), ("n3", "zone")));
        assert_eq!(typed(&blueprint, "each"), None, "its only wire replaced");
    }

    #[test]
    fn a_wildcard_takes_a_wire_of_its_shape_and_never_another_wildcard() {
        let mut blueprint = with_wildcards();
        let refusals = vec![
            (wire(("n2", "value"), ("each", "list")), Refusal::OtherType),
            (wire(("g", "value"), ("eq", "a")), Refusal::OtherType),
            (wire(("each", "element"), ("eq", "a")), Refusal::OtherType),
        ];
        refused(&mut blueprint, refusals);
        done(&mut blueprint, wire(("g", "value"), ("each", "list")));
        assert_eq!(typed(&blueprint, "each"), Some(ValueType::Zone));
        // Typed, its element is one zone, which the equality takes and types.
        done(&mut blueprint, wire(("each", "element"), ("eq", "a")));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Zone));
    }

    #[test]
    fn a_first_value_on_a_wildcard_types_its_node_with_the_type_it_is_given_as() {
        let mut blueprint = with_wildcards();
        let typed_as = |of| Edit::Value {
            node: id("eq"),
            pin: id("b"),
            value: Some(serde_json::json!(90)),
            of,
        };
        let refusals = vec![
            (typed_as(Some(ValueType::Zone)), Refusal::NotItsType),
            (typed_as(None), Refusal::NotItsType),
        ];
        refused(&mut blueprint, refusals);
        done(&mut blueprint, typed_as(Some(ValueType::Percentage)));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Percentage));
        // Typed, the node keeps its type whatever another value says it is.
        done(&mut blueprint, typed_as(Some(ValueType::Duration)));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Percentage));
        done(&mut blueprint, value("eq", "b", None));
        assert_eq!(typed(&blueprint, "eq"), None);
    }

    #[test]
    fn a_variable_changed_or_deleted_takes_the_type_of_the_wildcards_it_alone_typed() {
        for change in [
            Edit::DeleteVariable { id: id("zone") },
            Edit::Variable(Variable {
                value_type: ValueType::Number,
                default: serde_json::json!(0),
                ..harvest().variables[0].clone()
            }),
        ] {
            let mut blueprint = with_wildcards();
            done(&mut blueprint, wire(("n2", "value"), ("eq", "a")));
            done(&mut blueprint, change.clone());
            assert_eq!(typed(&blueprint, "eq"), None, "{change:?}");
        }
    }

    #[test]
    fn a_switch_keeps_its_type_while_it_has_cases_and_a_hole_takes_any_value() {
        let mut blueprint = harvest();
        let more = [
            r#"{"id": "sw", "kind": "test_switch", "options": {"type": "job", "cases": [24]}}"#,
            r#"{"id": "tx", "kind": "test_text", "options": {"text": "{count} done"}}"#,
            r#"{"id": "add", "kind": "test_add_to"}"#,
        ];
        for node in more {
            let node = serde_json::from_str(node).unwrap_or_else(|error| panic!("{error}"));
            blueprint.graphs[0].nodes.push(node);
        }
        done(&mut blueprint, value("n3", "pods", Some(serde_json::json!(80))));
        assert_eq!(
            typed(&blueprint, "sw"),
            Some(ValueType::Job),
            "its cases are of its type"
        );
        done(&mut blueprint, value("tx", "count", Some(serde_json::json!("five"))));
        let refusals = vec![
            (value("tx", "count", Some(serde_json::json!(5))), Refusal::NotItsType),
            (value("tx", "count", Some(serde_json::Value::Null)), Refusal::NotItsType),
        ];
        refused(&mut blueprint, refusals);
        done(&mut blueprint, wire(("n3", "count"), ("tx", "count")));
        // Without its variable, Add to has no type to take from a wire or a value.
        let as_number = Edit::Value {
            node: id("add"),
            pin: id("amount"),
            value: Some(serde_json::json!(5)),
            of: Some(ValueType::Number),
        };
        let refusals = vec![
            (value("add", "amount", Some(serde_json::json!(5))), Refusal::NotItsType),
            (as_number, Refusal::NotItsType),
            (wire(("n3", "count"), ("add", "amount")), Refusal::OtherType),
        ];
        refused(&mut blueprint, refusals);
    }

    #[test]
    fn a_node_naming_a_variable_follows_its_change_and_goes_with_it() {
        let mut blueprint = harvest();
        let more = [
            r#"{"id": "add", "kind": "test_add_to", "options": {"variable": "pods"}, "inputs": {"amount": 5}}"#,
            r#"{"id": "q", "kind": "pods_percent"}"#,
            r#"{"id": "add2", "kind": "test_add_to", "options": {"variable": "pods"}}"#,
        ];
        for node in more {
            let node = serde_json::from_str(node).unwrap_or_else(|error| panic!("{error}"));
            blueprint.graphs[0].nodes.push(node);
        }
        done(&mut blueprint, wire(("q", "value"), ("add2", "amount")));
        let pods = blueprint.variables.iter().find(|it| it.id.0 == "pods").cloned();
        let zone = Variable {
            value_type: ValueType::Zone,
            default: serde_json::Value::Null,
            bounds: None,
            ..pods.unwrap_or_else(|| panic!("pods"))
        };
        done(&mut blueprint, Edit::Variable(zone));
        assert!(
            !has(&blueprint, "q.value", "add2.amount"),
            "a percentage no longer fits a zone"
        );
        let add = blueprint.graphs[0].nodes.iter().find(|it| it.id.0 == "add");
        assert_eq!(add.map(|it| it.inputs.len()), Some(0), "5 is no zone");
        done(&mut blueprint, Edit::DeleteVariable { id: id("pods") });
        assert!(blueprint.graphs[0].nodes.iter().all(|it| !it.id.0.starts_with("add")));
    }

    /// The harvest with an untyped `≥`, N minutes and a query of the pods.
    fn with_comparison() -> Blueprint {
        let mut blueprint = harvest();
        let more = [
            r#"{"id": "ge", "kind": "greater_or_equal"}"#,
            r#"{"id": "m", "kind": "minutes"}"#,
            r#"{"id": "q", "kind": "pods_percent"}"#,
        ];
        for node in more {
            let node = serde_json::from_str(node).unwrap_or_else(|error| panic!("{error}"));
            blueprint.graphs[0].nodes.push(node);
        }
        blueprint
    }

    #[test]
    fn an_untyped_comparison_takes_the_type_of_a_wire_it_compares_and_refuses_one_it_does_not() {
        let mut blueprint = with_comparison();
        // A zone has no order: refused, not taken.
        refused(
            &mut blueprint,
            vec![(wire(("n2", "value"), ("ge", "a")), Refusal::OtherType)],
        );
        done(&mut blueprint, wire(("m", "value"), ("ge", "a")));
        assert_eq!(typed(&blueprint, "ge"), Some(ValueType::Duration));
        // A percentage feeds the numbers it reads untyped, in either order with a number.
        let mut percentages = with_comparison();
        done(&mut percentages, wire(("q", "value"), ("ge", "b")));
        assert_eq!(typed(&percentages, "ge"), None);
        let level = serde_json::from_str(r#"{"id": "lv", "kind": "level"}"#).unwrap_or_else(|error| panic!("{error}"));
        percentages.graphs[0].nodes.push(level);
        done(&mut percentages, wire(("lv", "value"), ("ge", "a")));
        // Read as numbers by its wires, it takes no other type under them.
        refused(
            &mut percentages,
            vec![(wire(("m", "value"), ("ge", "a")), Refusal::OtherType)],
        );
        // Replacing its only wire, it is open again.
        let mut again = with_comparison();
        done(&mut again, wire(("q", "value"), ("ge", "a")));
        done(&mut again, wire(("m", "value"), ("ge", "a")));
        assert_eq!(typed(&again, "ge"), Some(ValueType::Duration));
    }

    #[test]
    fn a_value_read_as_a_number_keeps_an_untyped_comparison_from_another_type() {
        let mut blueprint = with_comparison();
        let ninety = |pin: &str, of| Edit::Value {
            node: id("ge"),
            pin: id(pin),
            value: Some(serde_json::json!(90)),
            of,
        };
        done(&mut blueprint, ninety("b", None));
        refused(
            &mut blueprint,
            vec![(wire(("m", "value"), ("ge", "a")), Refusal::OtherType)],
        );
        // Read as numbers by `b`, `a` takes a number whatever type it is given with.
        done(&mut blueprint, ninety("a", Some(ValueType::Duration)));
        assert_eq!(typed(&blueprint, "ge"), None);
        done(&mut blueprint, value("ge", "a", None));
        // Typed again on its own pin, the value it replaces does not count.
        done(&mut blueprint, ninety("b", Some(ValueType::Duration)));
        assert_eq!(typed(&blueprint, "ge"), Some(ValueType::Duration));
        // Typed, another type given with a value is not taken, and none drops the value whatever it says.
        done(&mut blueprint, ninety("b", Some(ValueType::Time)));
        assert_eq!(typed(&blueprint, "ge"), Some(ValueType::Duration));
        let dropped = Edit::Value {
            node: id("ge"),
            pin: id("b"),
            value: None,
            of: Some(ValueType::Zone),
        };
        done(&mut blueprint, dropped);
        assert_eq!(typed(&blueprint, "ge"), None);
    }

    #[test]
    fn a_node_added_from_an_untyped_comparison_skips_an_output_of_a_type_it_does_not_take() {
        let mut blueprint = with_comparison();
        done(
            &mut blueprint,
            add(Does::Kind(id("harvest_until")), Some(("ge", "a", Side::In))),
        );
        assert!(
            has(&blueprint, "harvest_until.count", "ge.a"),
            "the count, not the reason's text"
        );
        let mut zones = with_comparison();
        let ge_from_zone = add(Does::Kind(id("greater_or_equal")), Some(("n2", "value", Side::Out)));
        refused(&mut zones, vec![(ge_from_zone, Refusal::OtherType)]);
    }

    #[test]
    fn an_untyped_comparison_takes_a_values_type_or_reads_it_as_its_number() {
        let typed_as = |of| Edit::Value {
            node: id("ge"),
            pin: id("b"),
            value: Some(serde_json::json!(90)),
            of,
        };
        let mut blueprint = with_comparison();
        refused(
            &mut blueprint,
            vec![(typed_as(Some(ValueType::Zone)), Refusal::NotItsType)],
        );
        done(&mut blueprint, typed_as(Some(ValueType::Duration)));
        assert_eq!(typed(&blueprint, "ge"), Some(ValueType::Duration));
        let mut numbers = with_comparison();
        done(&mut numbers, typed_as(None));
        assert_eq!(typed(&numbers, "ge"), None, "read as its number, no type noted");
    }

    #[test]
    fn a_comparison_added_from_a_duration_takes_its_type() {
        let mut blueprint = with_comparison();
        done(
            &mut blueprint,
            add(Does::Kind(id("greater_or_equal")), Some(("m", "value", Side::Out))),
        );
        assert_eq!(typed(&blueprint, "greater_or_equal"), Some(ValueType::Duration));
        assert!(has(&blueprint, "m.value", "greater_or_equal.a"));
    }

    #[test]
    fn a_value_left_on_a_wildcard_of_no_type_can_be_dropped() {
        let mut blueprint = with_wildcards();
        if let Some(eq) = blueprint.graphs[0].nodes.iter_mut().find(|it| it.id.0 == "eq") {
            eq.inputs.insert(id("b"), serde_json::json!(5));
        }
        done(&mut blueprint, value("eq", "b", None));
        assert!(
            blueprint.graphs[0]
                .nodes
                .iter()
                .all(|it| !it.inputs.contains_key(&id("b")))
        );
    }

    #[test]
    fn a_node_added_from_a_wildcard_types_it_by_its_pin() {
        let mut blueprint = with_wildcards();
        done(
            &mut blueprint,
            add(Does::Kind(id("notify")), Some(("each", "element", Side::Out))),
        );
        assert_eq!(typed(&blueprint, "each"), Some(ValueType::Text));
        assert!(has(&blueprint, "each.element", "notify.text"));
        let mut untouched = with_wildcards();
        refused(
            &mut untouched,
            vec![(
                add(Does::Kind(id("finish")), Some(("each", "element", Side::Out))),
                Refusal::OtherType,
            )],
        );
    }

    #[test]
    fn a_value_on_a_wildcard_waits_for_its_type_and_keeps_it_once_typed() {
        let mut blueprint = with_wildcards();
        let zone = || Some(serde_json::json!({"sub_area": 445}));
        refused(&mut blueprint, vec![(value("eq", "b", zone()), Refusal::NotItsType)]);
        done(&mut blueprint, wire(("n2", "value"), ("eq", "a")));
        refused(
            &mut blueprint,
            vec![(value("eq", "b", Some(serde_json::json!(5))), Refusal::NotItsType)],
        );
        done(&mut blueprint, value("eq", "b", zone()));
        done(&mut blueprint, unwire(("n2", "value"), ("eq", "a")));
        assert_eq!(typed(&blueprint, "eq"), Some(ValueType::Zone), "its value keeps it");
        done(&mut blueprint, value("eq", "b", None));
        assert_eq!(typed(&blueprint, "eq"), None);
    }

    #[test]
    fn a_wire_joins_an_output_to_an_input_that_takes_what_it_carries() {
        let mut blueprint = harvest();
        done(&mut blueprint, wire(("n3", "done"), ("n4", "run")));
        assert!(has(&blueprint, "n3.done", "n4.run"));
        let wrong = vec![
            (wire(("n2", "value"), ("n3", "pods")), Refusal::OtherType),
            (wire(("n3", "run"), ("n4", "run")), Refusal::WrongWay),
            (wire(("n3", "done"), ("n4", "done")), Refusal::WrongWay),
            (wire(("n3", "nothing"), ("n4", "run")), Refusal::NoSuchPin),
            (wire(("n9", "then"), ("n4", "run")), Refusal::NoSuchNode),
            (wire(("n3", "done"), ("n3", "run")), Refusal::SameNode),
        ];
        refused(&mut blueprint, wrong);
        let elsewhere = edit(&mut blueprint, &id("nowhere"), wire(("n3", "done"), ("n4", "run")));
        assert_eq!(elsewhere, Err(Refusal::NoSuchGraph));
    }

    #[test]
    fn a_wire_replaces_the_one_that_fed_its_input_or_left_its_flow_output_and_a_value_typed_there() {
        let mut blueprint = harvest();
        done(&mut blueprint, wire(("n5", "then"), ("n3", "run")));
        let both = has(&blueprint, "n1.then", "n3.run") && has(&blueprint, "n5.then", "n3.run");
        assert!(both, "a flow in takes many");
        done(&mut blueprint, wire(("n1", "then"), ("n4", "run")));
        assert!(!has(&blueprint, "n1.then", "n3.run"), "a flow out goes one way");
        done(&mut blueprint, add(Does::Get(id("zone")), None));
        done(&mut blueprint, wire(("get_zone", "value"), ("n3", "zone")));
        assert!(!has(&blueprint, "n2.value", "n3.zone"), "an input has one value");
        done(&mut blueprint, add(Does::Kind(id("job_resources")), None));
        for _ in 0..2 {
            done(&mut blueprint, wire(("job_resources", "value"), ("n3", "what")));
        }
        let typed = &blueprint.graphs[0].nodes[2].inputs;
        assert!(!typed.contains_key(&id("what")), "the value typed goes");
        assert_eq!(
            wires(&blueprint).len(),
            4,
            "the same wire again is one: {:?}",
            wires(&blueprint)
        );
    }

    #[test]
    fn a_node_added_takes_a_free_id_and_its_place_and_its_wire_to_the_pin_it_came_from() {
        let mut blueprint = harvest();
        let notify = Does::Kind(id("notify"));
        done(&mut blueprint, add(notify.clone(), Some(("n3", "failed", Side::Out))));
        done(&mut blueprint, add(notify, None));
        let nodes = &blueprint.graphs[0].nodes;
        let ids: Vec<&str> = nodes.iter().map(|node| node.id.0.as_str()).collect();
        assert_eq!(ids[5..], ["notify", "notify_2"]);
        assert!(has(&blueprint, "n3.failed", "notify.run"));
        assert_eq!(blueprint.layout[&id("events")].get(&id("notify_2")), Some(&[10, 20]));
        done(
            &mut blueprint,
            add(Does::Kind(id("life_percent")), Some(("n3", "pods", Side::In))),
        );
        assert!(
            has(&blueprint, "life_percent.value", "n3.pods"),
            "from an input, its output"
        );
        done(
            &mut blueprint,
            add(Does::Set(id("pods")), Some(("life_percent", "value", Side::Out))),
        );
        assert!(
            has(&blueprint, "life_percent.value", "set_pods.value"),
            "its first input that fits"
        );
    }

    #[test]
    fn a_node_added_from_a_pin_already_wired_takes_its_place_as_a_wire_does() {
        let mut blueprint = harvest();
        done(
            &mut blueprint,
            add(Does::Kind(id("notify")), Some(("n1", "then", Side::Out))),
        );
        assert!(has(&blueprint, "n1.then", "notify.run") && !has(&blueprint, "n1.then", "n3.run"));
        done(
            &mut blueprint,
            add(Does::Get(id("zone")), Some(("n3", "zone", Side::In))),
        );
        assert!(has(&blueprint, "get_zone.value", "n3.zone") && !has(&blueprint, "n2.value", "n3.zone"));
        done(
            &mut blueprint,
            add(Does::Kind(id("job_resources")), Some(("n3", "what", Side::In))),
        );
        assert!(
            !blueprint.graphs[0].nodes[2].inputs.contains_key(&id("what")),
            "the value typed goes"
        );
    }

    #[test]
    fn a_new_node_never_takes_the_id_a_wire_to_a_node_gone_still_names() {
        let mut blueprint = harvest();
        blueprint.graphs[0].wires.push(Wire {
            from: end("notify", "done"),
            to: end("n4", "run"),
        });
        done(&mut blueprint, add(Does::Kind(id("notify")), None));
        assert_eq!(blueprint.graphs[0].nodes[5].id, id("notify_2"));
    }

    #[test]
    fn only_what_the_picker_offers_is_added_and_only_with_a_pin_for_its_wire() {
        let notify = || Does::Kind(id("notify"));
        let wrong = vec![
            (add(Does::Kind(id("on_start")), None), Refusal::NotAChoice),
            (add(Does::Kind(id("entry")), None), Refusal::NotAChoice),
            (add(Does::Kind(id("teleport")), None), Refusal::NotAChoice),
            (add(Does::Get(id("nothing")), None), Refusal::NotAChoice),
            (add(notify(), Some(("n2", "value", Side::Out))), Refusal::OtherType),
            (add(notify(), Some(("n2", "value", Side::In))), Refusal::WrongWay),
            (add(notify(), Some(("n9", "then", Side::Out))), Refusal::NoSuchNode),
        ];
        refused(&mut harvest(), wrong);
    }

    fn moved(nodes: &[(&str, [i32; 2])]) -> Edit {
        Edit::Move {
            nodes: nodes.iter().map(|(node, at)| (id(node), *at)).collect(),
        }
    }

    /// A wire by its ends, `(node, pin)` each.
    type Ends<'a> = ((&'a str, &'a str), (&'a str, &'a str));

    fn deleted(nodes: &[&str], wires: &[Ends<'_>]) -> Edit {
        Edit::Delete {
            nodes: nodes.iter().map(|node| id(node)).collect(),
            wires: wires
                .iter()
                .map(|(from, to)| Wire {
                    from: end(from.0, from.1),
                    to: end(to.0, to.1),
                })
                .collect(),
        }
    }

    #[test]
    fn nodes_moved_together_change_their_places_and_nothing_else() {
        let mut blueprint = harvest();
        let graphs = blueprint.graphs.clone();
        done(&mut blueprint, moved(&[("n2", [-40, 300]), ("n3", [5, 6])]));
        let layout = &blueprint.layout[&id("events")];
        assert_eq!(
            (layout.get(&id("n2")), layout.get(&id("n3"))),
            (Some(&[-40, 300]), Some(&[5, 6]))
        );
        assert_eq!(blueprint.graphs, graphs);
        refused(
            &mut blueprint,
            vec![(moved(&[("n2", [0, 0]), ("n9", [0, 0])]), Refusal::NoSuchNode)],
        );
        let mut bare = harvest();
        bare.layout.clear();
        let before = bare.clone();
        done(&mut bare, moved(&[]));
        done(&mut bare, deleted(&[], &[]));
        assert_eq!(bare, before, "none moved or deleted changes nothing");
    }

    #[test]
    fn a_node_deleted_takes_its_wires_and_its_place_with_it() {
        let mut blueprint = harvest();
        done(&mut blueprint, deleted(&["n3"], &[]));
        let ids: Vec<&str> = blueprint.graphs[0]
            .nodes
            .iter()
            .map(|node| node.id.0.as_str())
            .collect();
        assert_eq!(ids, ["n1", "n2", "n4", "n5"]);
        assert_eq!(wires(&blueprint), [], "both wires of n3 go with it");
        assert_eq!(blueprint.layout[&id("events")].get(&id("n3")), None);
        done(&mut blueprint, deleted(&["n1"], &[]));
        assert!(
            blueprint.layout.is_empty(),
            "a graph's layout left empty goes: {:?}",
            blueprint.layout
        );
    }

    #[test]
    fn a_wire_deleted_goes_alone_and_with_nodes_in_one_change() {
        let mut blueprint = harvest();
        let nodes = blueprint.graphs[0].nodes.clone();
        done(&mut blueprint, deleted(&[], &[(("n1", "then"), ("n3", "run"))]));
        assert_eq!(wires(&blueprint), [("n2.value".to_owned(), "n3.zone".to_owned())]);
        assert_eq!(blueprint.graphs[0].nodes, nodes);
        done(&mut blueprint, deleted(&["n3"], &[(("n2", "value"), ("n3", "zone"))]));
        assert_eq!((wires(&blueprint).len(), blueprint.graphs[0].nodes.len()), (0, 4));
    }

    #[test]
    fn a_deletion_naming_a_node_or_a_wire_the_graph_lacks_is_refused_whole() {
        let wrong = vec![
            (deleted(&["n3", "n9"], &[]), Refusal::NoSuchNode),
            (
                deleted(&["n3"], &[(("n1", "then"), ("n4", "run"))]),
                Refusal::NoSuchWire,
            ),
        ];
        refused(&mut harvest(), wrong);
    }

    #[test]
    fn a_new_nodes_id_stays_well_formed_however_long_its_variables() {
        let mut blueprint = harvest();
        let long = "v".repeat(Id::LIMIT);
        blueprint.variables[0].id = id(&long);
        for _ in 0..2 {
            done(&mut blueprint, add(Does::Get(id(&long)), None));
        }
        assert!(
            blueprint.graphs[0].nodes[5..]
                .iter()
                .all(|node| node.id.is_well_formed())
        );
    }
}
