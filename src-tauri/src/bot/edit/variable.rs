//! My variables (step 3's (14), the owner's choices of 2026-11-17): one created under an id made from its name, which
//! stays when it is renamed; one changed whole (its name, type, default, bounds, help, set per character, memory), a
//! new type dropping the wires of its nodes it no longer fits, a default or a value typed on a Set it refuses, and
//! bounds it no longer has; one deleted with its nodes and their wires. Each is undone as the other edits.

use std::collections::HashSet;

use serde_json::Value;

use super::Refusal;
use crate::bot::blueprint::{Blueprint, Does, End, Id, Node};
use crate::bot::catalogue::Carries;
use crate::bot::copy::id_for;
use crate::bot::pins::pins;
use crate::bot::variable::{Shape, ValueType, Variable};

/// A variable named `name`, yes or no and false, as Unreal's new variable; its id, made from its name.
pub(super) fn create(blueprint: &mut Blueprint, name: &str) -> Result<Id, Refusal> {
    let name = name.trim();
    if name.is_empty() {
        return Err(Refusal::Blank);
    }
    // Nor a local's, which a Get in its function would then mean too.
    let locals = blueprint
        .graphs
        .iter()
        .flat_map(|it| it.function.iter().flat_map(|it| &it.locals));
    let taken: HashSet<Id> = blueprint
        .variables
        .iter()
        .chain(locals)
        .map(|it| it.id.clone())
        .collect();
    let id = id_for(name, "variable", &taken);
    blueprint.variables.push(Variable {
        id: id.clone(),
        name: name.to_owned(),
        value_type: ValueType::Bool,
        list: false,
        ranges: false,
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
    let was = &blueprint.variables[at];
    let percentage = changed.value_type == ValueType::Percentage;
    let wrong = |[smallest, largest]: [i64; 2]| percentage && (smallest < 0 || largest > 100);
    // Bounds hold for a single number or percentage only.
    let numeric = changed.value_type.is_numeric() && changed.of_type().shape == Shape::ONE;
    if was.of_type() != changed.of_type() {
        // Bounds a new type cannot have go: none on a text or a list, none past 0 to 100 on a percentage.
        if !numeric || changed.bounds.is_some_and(wrong) {
            changed.bounds = None;
        }
        if !changed.of_type().holds(&changed.default) {
            changed.default = Value::Null;
        }
    }
    if let Some([smallest, largest]) = changed.bounds {
        if !numeric || smallest > largest || wrong([smallest, largest]) {
            return Err(Refusal::NotItsType);
        }
        // Bounds narrowed past the default bring it within them, as a field of a number would; a default typed out
        // of bounds left as they were is refused below.
        if changed.bounds != was.bounds
            && let Some(whole) = changed.default.as_i64()
        {
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

/// Drops, from the nodes reading or writing `variable`, or naming it in their options, the wires its type no longer fits
/// and the values typed that their pins, a Set's within its bounds, refuse.
fn fit(blueprint: &mut Blueprint, variable: &Id) {
    let Some(it) = blueprint.variables.iter().find(|it| &it.id == variable).cloned() else {
        return;
    };
    for at in 0..blueprint.graphs.len() {
        let graph = &blueprint.graphs[at];
        let nodes: Vec<Id> = graph
            .nodes
            .iter()
            .filter(|node| on(node, variable))
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
        let unheld: Vec<(Id, Id)> = graph
            .nodes
            .iter()
            .filter(|node| nodes.contains(&node.id))
            .flat_map(|node| {
                let pins = pins(blueprint, graph, node).unwrap_or_default();
                let held = |pin: &Id, value: &Value| {
                    let typed = pins.iter().find(|it| it.side == crate::bot::Side::In && it.id == pin.0);
                    match (&node.does, typed.map(|it| it.carries)) {
                        (Does::Set(_), _) => it.accepts(value),
                        (_, Some(Carries::Value(typed))) => typed.holds(value),
                        _ => false,
                    }
                };
                let unheld = node.inputs.iter().filter(|(pin, value)| !held(pin, value));
                unheld
                    .map(|(pin, _)| (node.id.clone(), pin.clone()))
                    .collect::<Vec<_>>()
            })
            .collect();
        let graph = &mut blueprint.graphs[at];
        graph.wires.retain(|wire| !unfit.contains(wire));
        for (node, pin) in &unheld {
            if let Some(node) = graph.nodes.iter_mut().find(|it| &it.id == node) {
                node.inputs.remove(pin);
            }
        }
        // A wildcard whose last wire went no longer has a type.
        super::untype(graph);
    }
}

/// Whether `node` reads or writes `variable`, or names it in its options: Add to, a list's Add…
fn on(node: &Node, variable: &Id) -> bool {
    matches!(&node.does, Does::Get(id) | Does::Set(id) if id == variable)
        || node.options.variable.as_ref() == Some(variable)
}

/// The variable `id` gone, with every node reading or writing it or naming it, their wires and places.
pub(super) fn delete(blueprint: &mut Blueprint, id: &Id) -> Result<(), Refusal> {
    let before = blueprint.variables.len();
    blueprint.variables.retain(|it| &it.id != id);
    if blueprint.variables.len() == before {
        return Err(Refusal::NoSuchVariable);
    }
    for graph in &mut blueprint.graphs {
        let gone: Vec<Id> = graph
            .nodes
            .iter()
            .filter(|node| on(node, id))
            .map(|node| node.id.clone())
            .collect();
        graph.nodes.retain(|node| !gone.contains(&node.id));
        graph
            .wires
            .retain(|wire| !gone.contains(&wire.from.0) && !gone.contains(&wire.to.0));
        super::untype(graph);
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
    fn a_variable_retyped_drops_the_wires_and_the_values_it_no_longer_fits_and_keeps_the_others() {
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
                of: None,
            },
        );
        done(
            &mut blueprint,
            Edit::Wire {
                from: End(id("n4"), id("done")),
                to: End(id("set_pods"), id("run")),
            },
        );
        done(
            &mut blueprint,
            Edit::Add {
                does: Does::Get(id("pods")),
                position: [0, 0],
                wired_to: None,
            },
        );
        done(
            &mut blueprint,
            Edit::Add {
                does: Does::Kind(id("wait")),
                position: [0, 0],
                wired_to: None,
            },
        );
        done(
            &mut blueprint,
            Edit::Wire {
                from: End(id("get_pods"), id("value")),
                to: End(id("wait"), id("minutes")),
            },
        );
        // A number: its percentage's bounds and value still fit, and so do the flow and the wire into a number.
        let mut pods = variable(&blueprint, "pods");
        pods.value_type = ValueType::Number;
        done(&mut blueprint, Edit::Variable(pods));
        let pods = variable(&blueprint, "pods");
        assert_eq!((pods.default.clone(), pods.bounds), (json!(90), Some([10, 100])));
        let set_inputs = |blueprint: &Blueprint| {
            let set = blueprint.graphs[0].nodes.iter().find(|node| node.id.0 == "set_pods");
            set.map(|node| node.inputs.len()).unwrap_or_default()
        };
        assert_eq!(set_inputs(&blueprint), 1, "50 still holds");
        let kept = [
            "n4.done>set_pods.run".to_owned(),
            "get_pods.value>wait.minutes".to_owned(),
        ];
        assert!(
            kept.iter().all(|wire| wires(&blueprint).contains(wire)),
            "{:?}",
            wires(&blueprint)
        );
        // A text: the wire into a number goes, the flow stays, and its default, bounds and value go.
        let mut pods = variable(&blueprint, "pods");
        pods.value_type = ValueType::Text;
        done(&mut blueprint, Edit::Variable(pods));
        let pods = variable(&blueprint, "pods");
        assert_eq!(
            (pods.default, pods.bounds),
            (Value::Null, None),
            "a default and bounds a text refuses"
        );
        assert_eq!(set_inputs(&blueprint), 0, "its value typed goes");
        assert!(wires(&blueprint).contains(&kept[0]) && !wires(&blueprint).contains(&kept[1]));
        let mut zone = variable(&blueprint, "zone");
        zone.value_type = ValueType::Number;
        done(&mut blueprint, Edit::Variable(zone));
        assert!(
            !wires(&blueprint).contains(&"n2.value>n3.zone".to_owned()),
            "{:?}",
            wires(&blueprint)
        );
    }

    #[test]
    fn a_number_made_a_list_drops_the_default_and_the_bounds_a_list_cannot_hold() {
        let mut blueprint = harvest();
        let mut goal = variable(&blueprint, "harvests");
        goal.bounds = Some([0, 10_000]);
        done(&mut blueprint, Edit::Variable(goal.clone()));
        goal.list = true;
        done(&mut blueprint, Edit::Variable(goal.clone()));
        let made = variable(&blueprint, "harvests");
        assert_eq!((made.list, made.bounds, made.default), (true, None, Value::Null));
        goal.default = json!([5000]);
        goal.bounds = Some([0, 10_000]);
        let refused = edit(&mut blueprint, &Id("events".into()), Edit::Variable(goal));
        assert_eq!(refused, Err(Refusal::NotItsType), "no bounds on a list");
    }

    #[test]
    fn a_number_made_a_percentage_drops_bounds_past_0_to_100() {
        let mut blueprint = harvest();
        let mut goal = variable(&blueprint, "harvests");
        goal.bounds = Some([0, 10_000]);
        goal.default = json!(5000);
        done(&mut blueprint, Edit::Variable(goal.clone()));
        goal.value_type = ValueType::Percentage;
        done(&mut blueprint, Edit::Variable(goal));
        let goal = variable(&blueprint, "harvests");
        assert_eq!(
            (goal.bounds, goal.default),
            (None, Value::Null),
            "5000 is no percentage"
        );
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
