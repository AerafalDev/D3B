//! The editor's changes to a blueprint (`docs/bot.md`, "In the app"), the owner's choices of 2026-11-16: a wire checked
//! by what its pins carry, which replaces the wire that fed its input or left its flow output, an input having one
//! value and a flow one way on; a node the picker offers, under an id of its own, wired to the pin it came from; a node
//! moved. Each is done whole or refused with nothing changed; `storage` saves it at once.

mod choices;

use std::collections::BTreeMap;

pub(crate) use choices::choices;

use super::blueprint::{Blueprint, Does, End, Graph, Id, Node, Wire};
use super::catalogue::{Carries, Side};
use super::pins::{NodePin, pins};

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
    /// A node put elsewhere.
    Move { node: Id, position: [i32; 2] },
}

/// Why an edit is refused. The editor offers none of these; a refusal says the webview and the file disagree.
#[derive(Debug, Clone, Copy, PartialEq, Eq, thiserror::Error)]
pub(crate) enum Refusal {
    #[error("the blueprint has no such graph")]
    NoSuchGraph,
    #[error("the graph has no such node")]
    NoSuchNode,
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
}

/// Makes `edit` in `blueprint`'s graph `graph`, or refuses it and changes nothing.
pub(crate) fn edit(blueprint: &mut Blueprint, graph: &Id, edit: Edit) -> Result<(), Refusal> {
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
        Edit::Move { node, position } => {
            if !shown.nodes.iter().any(|it| it.id == node) {
                return Err(Refusal::NoSuchNode);
            }
            blueprint
                .layout
                .entry(graph.clone())
                .or_default()
                .insert(node, position);
        }
    }
    Ok(())
}

/// A wire checked, and whether it carries a value rather than the flow.
struct Link {
    wire: Wire,
    value: bool,
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
    if !out.carries.feeds(input.carries) {
        return Err(Refusal::OtherType);
    }
    Ok(Link {
        value: input.carries != Carries::Exec,
        wire: Wire { from, to },
    })
}

/// The wire between the pin a wire dropped on the ground came from and `new`'s first pin that fits it.
fn to_new(blueprint: &Blueprint, graph: &Graph, new: &Node, end: End, side: Side) -> Result<Link, Refusal> {
    let dragged = pin_of(blueprint, graph, node(graph, &end.0)?, &end.1, side)?;
    let theirs = pins(blueprint, graph, new).unwrap_or_default();
    let at = |pin: &NodePin| End(new.id.clone(), Id(pin.id.clone()));
    let link = if side == Side::Out {
        let pin = theirs
            .iter()
            .find(|it| it.side == Side::In && dragged.carries.feeds(it.carries));
        pin.map(|pin| Link {
            value: pin.carries != Carries::Exec,
            wire: Wire { from: end, to: at(pin) },
        })
    } else {
        let pin = theirs
            .iter()
            .find(|it| it.side == Side::Out && it.carries.feeds(dragged.carries));
        pin.map(|pin| Link {
            value: dragged.carries != Carries::Exec,
            wire: Wire { from: at(pin), to: end },
        })
    };
    link.ok_or(Refusal::OtherType)
}

/// Adds `link`'s wire in place of the one that fed its input, with the value typed there, or that left its flow output.
fn join(graph: &mut Graph, Link { wire, value }: Link) {
    if value {
        graph.wires.retain(|it| it.to != wire.to);
        if let Some(node) = graph.nodes.iter_mut().find(|it| it.id == wire.to.0) {
            node.inputs.remove(&wire.to.1);
        }
    } else {
        graph.wires.retain(|it| it.from != wire.from);
    }
    graph.wires.push(wire);
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

    #[test]
    fn a_node_moved_changes_its_place_and_nothing_else() {
        let mut blueprint = harvest();
        let graphs = blueprint.graphs.clone();
        done(
            &mut blueprint,
            Edit::Move {
                node: id("n2"),
                position: [-40, 300],
            },
        );
        assert_eq!(blueprint.layout[&id("events")].get(&id("n2")), Some(&[-40, 300]));
        assert_eq!(blueprint.graphs, graphs);
        refused(
            &mut blueprint,
            vec![(
                Edit::Move {
                    node: id("n9"),
                    position: [0, 0],
                },
                Refusal::NoSuchNode,
            )],
        );
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
