//! My variables (step 3's (14), the owner's choices of 2026-11-17): one created under an id made from its name, which
//! stays when it is renamed; one changed whole (its name, type, default, bounds, help, set per character, memory), a
//! new type dropping the wires of its nodes it no longer fits, a default or a value typed on a Set it refuses, and
//! bounds it no longer has; one deleted with its nodes and their wires. Each is undone as the other edits.

use std::collections::HashSet;

use serde_json::Value;

use super::Refusal;
use crate::bot::blueprint::{Blueprint, Does, End, Id};
use crate::bot::copy::id_for;
use crate::bot::pins::pins;
use crate::bot::variable::{ValueType, Variable};

/// A variable named `name`, yes or no and false, as Unreal's new variable; its id, made from its name.
pub(super) fn create(blueprint: &mut Blueprint, name: &str) -> Result<Id, Refusal> {
    let name = name.trim();
    if name.is_empty() {
        return Err(Refusal::Blank);
    }
    let taken: HashSet<Id> = blueprint.variables.iter().map(|it| it.id.clone()).collect();
    let id = id_for(name, "variable", &taken);
    blueprint.variables.push(Variable {
        id: id.clone(),
        name: name.to_owned(),
        value_type: ValueType::Bool,
        default: Value::Bool(false),
        bounds: None,
        per_character: false,
        memory: false,
        help: String::new(),
    });
    Ok(id)
}

/// The variable of `changed`'s id, made `changed`, and what its nodes no longer fit dropped.
pub(super) fn change(blueprint: &mut Blueprint, mut changed: Variable) -> Result<(), Refusal> {
    let at = blueprint
        .variables
        .iter()
        .position(|it| it.id == changed.id)
        .ok_or(Refusal::NoSuchVariable)?;
    // The name as the player sees it, without the spaces around it.
    let name = changed.name.trim().to_owned();
    changed.name = name;
    if changed.name.is_empty() {
        return Err(Refusal::Blank);
    }
    let retyped = blueprint.variables[at].value_type != changed.value_type;
    if retyped {
        if !changed.value_type.is_numeric() {
            changed.bounds = None;
        }
        if !changed.value_type.holds(&changed.default) {
            changed.default = Value::Null;
        }
    }
    if let Some([smallest, largest]) = changed.bounds {
        let percentage = changed.value_type == ValueType::Percentage;
        if !changed.value_type.is_numeric() || smallest > largest || percentage && (smallest < 0 || largest > 100) {
            return Err(Refusal::NotItsType);
        }
        // Bounds narrowed past the default bring it within them, as a field of a number would.
        if let Some(whole) = changed.default.as_i64() {
            changed.default = Value::from(whole.clamp(smallest, largest));
        }
    }
    if !changed.default.is_null() && !changed.accepts(&changed.default) {
        return Err(Refusal::NotItsType);
    }
    blueprint.variables[at] = changed;
    fit(blueprint, &blueprint.variables[at].id.clone());
    Ok(())
}

/// Drops, from the nodes reading or writing `variable`, the wires its type no longer fits and the values typed on a
/// Set that it refuses.
fn fit(blueprint: &mut Blueprint, variable: &Id) {
    let Some(it) = blueprint.variables.iter().find(|it| &it.id == variable).cloned() else {
        return;
    };
    for at in 0..blueprint.graphs.len() {
        let graph = &blueprint.graphs[at];
        let on = |does: &Does| matches!(does, Does::Get(id) | Does::Set(id) if id == variable);
        let nodes: Vec<Id> = graph
            .nodes
            .iter()
            .filter(|node| on(&node.does))
            .map(|node| node.id.clone())
            .collect();
        let carries = |end: &End, out: bool| {
            let node = graph.nodes.iter().find(|node| node.id == end.0)?;
            let pins = pins(blueprint, graph, node)?;
            let side = if out {
                crate::bot::Side::Out
            } else {
                crate::bot::Side::In
            };
            pins.into_iter()
                .find(|pin| pin.side == side && pin.id == end.1.0)
                .map(|pin| pin.carries)
        };
        let unfit: Vec<_> = graph
            .wires
            .iter()
            .filter(|wire| nodes.contains(&wire.from.0) || nodes.contains(&wire.to.0))
            .filter(|wire| match (carries(&wire.from, true), carries(&wire.to, false)) {
                (Some(from), Some(to)) => !from.feeds(to),
                _ => false,
            })
            .cloned()
            .collect();
        let graph = &mut blueprint.graphs[at];
        graph.wires.retain(|wire| !unfit.contains(wire));
        for node in graph.nodes.iter_mut().filter(|node| nodes.contains(&node.id)) {
            node.inputs.retain(|_, value| it.accepts(value));
        }
    }
}

/// The variable `id` gone, with every node reading or writing it, their wires and places.
pub(super) fn delete(blueprint: &mut Blueprint, id: &Id) -> Result<(), Refusal> {
    let before = blueprint.variables.len();
    blueprint.variables.retain(|it| &it.id != id);
    if blueprint.variables.len() == before {
        return Err(Refusal::NoSuchVariable);
    }
    for graph in &mut blueprint.graphs {
        let on = |does: &Does| matches!(does, Does::Get(it) | Does::Set(it) if it == id);
        let gone: Vec<Id> = graph
            .nodes
            .iter()
            .filter(|node| on(&node.does))
            .map(|node| node.id.clone())
            .collect();
        graph.nodes.retain(|node| !gone.contains(&node.id));
        graph
            .wires
            .retain(|wire| !gone.contains(&wire.from.0) && !gone.contains(&wire.to.0));
        if let Some(places) = blueprint.layout.get_mut(&graph.id) {
            places.retain(|node, _| !gone.contains(node));
            if places.is_empty() {
                blueprint.layout.remove(&graph.id);
            }
        }
    }
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

    fn done(blueprint: &mut Blueprint, change: Edit) {
        let said = format!("{change:?}");
        assert_eq!(edit(blueprint, &id("events"), change), Ok(()), "{said}");
    }

    fn variable(blueprint: &Blueprint, of: &str) -> Variable {
        let found = blueprint.variables.iter().find(|it| it.id.0 == of).cloned();
        found.unwrap_or_else(|| panic!("no variable {of}"))
    }

    fn wires(blueprint: &Blueprint) -> Vec<String> {
        let text = |end: &End| format!("{}.{}", end.0.0, end.1.0);
        let wires = &blueprint.graphs[0].wires;
        wires
            .iter()
            .map(|wire| format!("{}>{}", text(&wire.from), text(&wire.to)))
            .collect()
    }

    #[test]
    fn a_variable_created_takes_an_id_made_from_its_name_and_is_yes_or_no() {
        let mut blueprint = harvest();
        for _ in 0..2 {
            done(
                &mut blueprint,
                Edit::NewVariable {
                    name: " Objectif atteint ".into(),
                },
            );
        }
        let ids: Vec<&str> = blueprint.variables[3..].iter().map(|it| it.id.0.as_str()).collect();
        assert_eq!(ids, ["objectif-atteint", "objectif-atteint-2"]);
        let made = variable(&blueprint, "objectif-atteint");
        assert_eq!(
            (made.name.as_str(), made.value_type, made.default),
            ("Objectif atteint", ValueType::Bool, json!(false))
        );
        let before = blueprint.clone();
        assert_eq!(
            edit(&mut blueprint, &id("events"), Edit::NewVariable { name: "  ".into() }),
            Err(Refusal::Blank)
        );
        assert_eq!(blueprint, before);
    }

    #[test]
    fn a_variable_renamed_keeps_its_id_and_its_settings_as_they_are() {
        let mut blueprint = harvest();
        let mut pods = variable(&blueprint, "pods");
        pods.name = "Banque à".into();
        pods.help = "Les pods qui envoient à la banque.".into();
        pods.memory = true;
        done(&mut blueprint, Edit::Variable(pods.clone()));
        assert_eq!(variable(&blueprint, "pods"), pods);
    }

    #[test]
    fn a_variable_retyped_drops_the_wires_and_the_values_it_no_longer_fits() {
        let mut blueprint = harvest();
        done(
            &mut blueprint,
            Edit::Add {
                does: Does::Set(id("pods")),
                position: [0, 0],
                wired_to: None,
            },
        );
        done(
            &mut blueprint,
            Edit::Value {
                node: id("set_pods"),
                pin: id("value"),
                value: Some(json!(50)),
            },
        );
        done(
            &mut blueprint,
            Edit::Wire {
                from: End(id("n2"), id("value")),
                to: End(id("n3"), id("zone")),
            },
        );
        let mut zone = variable(&blueprint, "zone");
        zone.value_type = ValueType::Number;
        zone.default = json!(7);
        done(&mut blueprint, Edit::Variable(zone));
        assert!(
            !wires(&blueprint).contains(&"n2.value>n3.zone".to_owned()),
            "{:?}",
            wires(&blueprint)
        );
        let mut pods = variable(&blueprint, "pods");
        pods.value_type = ValueType::Text;
        done(&mut blueprint, Edit::Variable(pods));
        let pods = variable(&blueprint, "pods");
        assert_eq!(
            (pods.default, pods.bounds),
            (Value::Null, None),
            "a default and bounds a text refuses"
        );
        let set = blueprint.graphs[0].nodes.iter().find(|node| node.id.0 == "set_pods");
        assert!(set.is_some_and(|node| node.inputs.is_empty()), "its value typed goes");
    }

    #[test]
    fn bounds_narrowed_bring_the_default_within_them_and_wrong_ones_are_refused() {
        let mut blueprint = harvest();
        let mut pods = variable(&blueprint, "pods");
        pods.bounds = Some([10, 50]);
        done(&mut blueprint, Edit::Variable(pods.clone()));
        assert_eq!(variable(&blueprint, "pods").default, json!(50));
        let before = blueprint.clone();
        let mut zone = variable(&blueprint, "zone");
        zone.bounds = Some([0, 1]);
        for (wrong, why) in [
            (
                Variable {
                    bounds: Some([60, 10]),
                    ..pods.clone()
                },
                Refusal::NotItsType,
            ),
            (
                Variable {
                    bounds: Some([0, 101]),
                    ..pods.clone()
                },
                Refusal::NotItsType,
            ),
            (
                Variable {
                    default: json!("half"),
                    ..pods.clone()
                },
                Refusal::NotItsType,
            ),
            (zone, Refusal::NotItsType),
            (
                Variable {
                    name: " ".into(),
                    ..pods.clone()
                },
                Refusal::Blank,
            ),
            (Variable { id: id("gone"), ..pods }, Refusal::NoSuchVariable),
        ] {
            assert_eq!(
                edit(&mut blueprint, &id("events"), Edit::Variable(wrong.clone())),
                Err(why),
                "{wrong:?}"
            );
        }
        assert_eq!(blueprint, before, "a refusal changes nothing");
    }

    #[test]
    fn a_variable_deleted_takes_its_nodes_their_wires_and_places_with_it() {
        let mut blueprint = harvest();
        done(
            &mut blueprint,
            Edit::Add {
                does: Does::Set(id("zone")),
                position: [5, 5],
                wired_to: None,
            },
        );
        done(&mut blueprint, Edit::DeleteVariable { id: id("zone") });
        assert!(blueprint.variables.iter().all(|it| it.id.0 != "zone"));
        let nodes: Vec<&str> = blueprint.graphs[0]
            .nodes
            .iter()
            .map(|node| node.id.0.as_str())
            .collect();
        assert_eq!(nodes, ["n1", "n3", "n4", "n5"]);
        assert_eq!(wires(&blueprint), ["n1.then>n3.run"]);
        assert!(
            blueprint
                .layout
                .get(&id("events"))
                .is_none_or(|places| !places.contains_key(&id("set_zone")))
        );
        let before = blueprint.clone();
        assert_eq!(
            edit(&mut blueprint, &id("events"), Edit::DeleteVariable { id: id("zone") }),
            Err(Refusal::NoSuchVariable)
        );
        assert_eq!(blueprint, before);
    }
}
