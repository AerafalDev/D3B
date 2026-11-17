//! The zones a `.d3b` carries (ADR 0018): those of mine its blueprint names, as `{"zone": "<id>"}` in a variable's
//! default or a value typed on a node, and their import beside mine, never over a file (the owner, 2026-11-16): one
//! identical to a zone of mine is that zone, one whose id is taken takes a free one and the blueprint names it.

use std::collections::{BTreeMap, BTreeSet, HashSet};
use std::io;

use serde_json::Value;

use super::store::{StorageError, Store};
use super::zone::{self, Zone};
use crate::bot::{self, Blueprint, Id};

/// Calls `visit` on the id of each zone of mine `value` names, at any depth, so that a list of zones is seen too.
fn each_zone(value: &mut Value, visit: &mut impl FnMut(&mut String)) {
    match value {
        Value::Object(object) if object.len() == 1 => match object.get_mut("zone") {
            Some(Value::String(id)) => visit(id),
            _ => object.values_mut().for_each(|value| each_zone(value, visit)),
        },
        Value::Object(object) => object.values_mut().for_each(|value| each_zone(value, visit)),
        Value::Array(values) => values.iter_mut().for_each(|value| each_zone(value, visit)),
        _ => {}
    }
}

/// Calls `visit` on each value `blueprint` holds: its variables' defaults and the values typed on its nodes.
fn each_value(blueprint: &mut Blueprint, visit: &mut impl FnMut(&mut String)) {
    for variable in &mut blueprint.variables {
        each_zone(&mut variable.default, visit);
    }
    for graph in &mut blueprint.graphs {
        for node in &mut graph.nodes {
            node.inputs.values_mut().for_each(|value| each_zone(value, visit));
        }
    }
}

/// The ids of the zones of mine `blueprint` names.
pub(super) fn named(blueprint: &Blueprint) -> BTreeSet<Id> {
    let mut named = BTreeSet::new();
    // Read through a copy: the walk is one for reading and rewriting.
    each_value(&mut blueprint.clone(), &mut |id| {
        named.insert(Id(id.clone()));
    });
    named
}

/// Makes `blueprint` name each zone `renamed` gives a new id by that id.
pub(super) fn rename(blueprint: &mut Blueprint, renamed: &BTreeMap<Id, Id>) {
    if renamed.is_empty() {
        return;
    }
    each_value(blueprint, &mut |id| {
        if let Some(new) = renamed.get(&Id(id.clone())) {
            id.clone_from(&new.0);
        }
    });
}

impl Store {
    /// Adds `zones` among mine, the lock held: one with the name, world and maps of the zone of mine of its id is that
    /// zone; one whose id a zone or a file of the folder holds, in any case, or one of `reserved`, takes a free one; the
    /// others keep theirs. The new ids, by the ids they had.
    pub(super) fn add_zones_while_locked(
        &self,
        zones: Vec<Zone>,
        reserved: BTreeSet<Id>,
    ) -> Result<BTreeMap<Id, Id>, StorageError> {
        let mut renamed = BTreeMap::new();
        if zones.is_empty() {
            return Ok(renamed);
        }
        let folder = self.zones_folder();
        let mut taken: HashSet<Id> = reserved.into_iter().collect();
        match std::fs::read_dir(&folder) {
            Ok(entries) => {
                for path in entries.flatten().map(|entry| entry.path()) {
                    if let Some(stem) = path.file_stem().and_then(|stem| stem.to_str()) {
                        taken.insert(Id(stem.to_lowercase()));
                    }
                }
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => {}
            // A folder that does not list could hide the file a zone would be written over.
            Err(source) => return Err(StorageError::Unreadable { path: folder, source }),
        }
        let mine = self.zones().read;
        for mut zone in zones {
            let same = |other: &&Zone| {
                other.id == zone.id && other.name == zone.name && other.world == zone.world && other.maps == zone.maps
            };
            if mine.iter().any(|other| same(&other)) {
                continue;
            }
            // `zone::parse` refused an id no file can be named by.
            if taken.contains(&zone.id) {
                let id = bot::id_for(&zone.id.0, "zone", &taken);
                renamed.insert(std::mem::replace(&mut zone.id, id.clone()), id);
            }
            zone.schema = zone::SCHEMA;
            taken.insert(zone.id.clone());
            let path = folder.join(format!("{}.json", zone.id.0));
            zone::write(&folder, &path, &zone)?;
        }
        Ok(renamed)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::HARVEST;
    use crate::storage::share::Shared;
    use crate::storage::tests::Folder;

    /// The fixture as mine, its Zone defaulting to my zone `wheat` and a node naming the zones `wheat` and `bank`.
    fn harvest() -> Blueprint {
        let mut blueprint = bot::read(&HARVEST.replacen("automatic-harvest", "ma-recolte", 1))
            .unwrap_or_else(|error| panic!("{error}"));
        blueprint.variables[0].default = serde_json::json!({"zone": "wheat"});
        let node = &mut blueprint.graphs[0].nodes[0];
        node.inputs.insert(
            Id("zones".into()),
            serde_json::json!([{"zone": "wheat"}, {"zone": "bank"}, {"sub_area": 445}]),
        );
        blueprint
    }

    fn zone(id: &str, maps: &[[i16; 2]]) -> Zone {
        Zone {
            schema: zone::SCHEMA,
            id: Id(id.into()),
            name: id.to_uppercase(),
            world: 1,
            maps: maps.iter().copied().collect(),
        }
    }

    #[test]
    fn a_blueprint_names_the_zones_of_its_defaults_and_values_and_is_rewritten_to_name_new_ones() {
        let mut blueprint = harvest();
        let ids: Vec<_> = named(&blueprint).into_iter().map(|id| id.0).collect();
        assert_eq!(ids, ["bank", "wheat"]);
        rename(
            &mut blueprint,
            &BTreeMap::from([(Id("wheat".into()), Id("wheat-2".into()))]),
        );
        let ids: Vec<_> = named(&blueprint).into_iter().map(|id| id.0).collect();
        assert_eq!(ids, ["bank", "wheat-2"]);
        assert_eq!(blueprint.variables[0].default, serde_json::json!({"zone": "wheat-2"}));
    }

    #[test]
    fn the_zones_it_names_are_shared_and_imported_an_identical_one_reused_another_renamed_never_over_a_file() {
        let folder = Folder::new("share-zones");
        let mine = Store::new(folder.path().join("mine"));
        let theirs = Store::new(folder.path().join("theirs"));
        for zone in [
            zone("wheat", &[[0, 0], [1, 0]]),
            zone("bank", &[[4, -18]]),
            zone("unused", &[]),
        ] {
            let path = folder.path().join("mine/zones").join(format!("{}.json", zone.id.0));
            zone::write(&folder.path().join("mine/zones"), &path, &zone).unwrap_or_else(|error| panic!("{error}"));
        }
        let blueprint = harvest();
        mine.import(Shared {
            blueprint: blueprint.clone(),
            zones: Vec::new(),
        })
        .unwrap_or_else(|error| panic!("{error}"));
        let shared = mine
            .shared("ma-recolte", None)
            .unwrap_or_else(|error| panic!("{error}"));
        let ids: Vec<_> = shared.zones.iter().map(|zone| zone.id.0.as_str()).collect();
        assert_eq!(ids, ["bank", "wheat"], "only those it names");
        // On the other D3B: `bank` the same, `wheat` another zone, and a file kept under the id it would take next.
        let theirs_zones = folder.path().join("theirs/zones");
        let bank = zone("bank", &[[4, -18]]);
        zone::write(&theirs_zones, &theirs_zones.join("bank.json"), &bank).unwrap_or_else(|error| panic!("{error}"));
        let other = zone("wheat", &[[9, 9]]);
        zone::write(&theirs_zones, &theirs_zones.join("wheat.json"), &other).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(theirs_zones.join("Wheat-2.json"), "{").unwrap_or_else(|error| panic!("{error}"));
        let id = theirs.import(shared).unwrap_or_else(|error| panic!("{error}"));
        let imported = theirs.blueprints().read.into_iter().find(|it| it.id == id);
        let imported = imported.unwrap_or_else(|| panic!("{id:?}"));
        let ids: Vec<_> = named(&imported).into_iter().map(|id| id.0).collect();
        assert_eq!(ids, ["bank", "wheat-3"]);
        let zones = theirs.zones().read;
        let wheat = zones
            .iter()
            .find(|zone| zone.id.0 == "wheat-3")
            .map(|zone| (&zone.name, &zone.maps));
        assert_eq!(wheat, Some((&"WHEAT".to_owned(), &zone("x", &[[0, 0], [1, 0]]).maps)));
        assert_eq!(
            zones.iter().find(|zone| zone.id.0 == "wheat"),
            Some(&other),
            "never written over"
        );
        assert_eq!(zones.len(), 3, "bank reused");
        assert_eq!(
            std::fs::read_to_string(theirs_zones.join("Wheat-2.json"))
                .ok()
                .as_deref(),
            Some("{")
        );
    }

    #[test]
    fn an_import_carrying_zones_ends_my_zones_undo_history_so_no_step_writes_over_a_zone_it_brought() {
        let folder = Folder::new("share-zones-history");
        let store = Store::new(folder.path().to_path_buf());
        store.create_zone("Wheat", 1).unwrap_or_else(|error| panic!("{error}"));
        let remove = |path: &std::path::Path| std::fs::remove_file(path);
        let undone = store.undo_zone(remove).unwrap_or_else(|error| panic!("{error}"));
        assert!(undone.is_some_and(|state| state.zone.is_none()), "the creation undone");
        // Its id free again, a zone brought by a `.d3b` takes it.
        store
            .import(Shared {
                blueprint: harvest(),
                zones: vec![zone("wheat", &[[7, 7]])],
            })
            .unwrap_or_else(|error| panic!("{error}"));
        let redone = store.redo_zone(remove).unwrap_or_else(|error| panic!("{error}"));
        assert!(redone.is_none(), "nothing to redo: {redone:?}");
        let undone = store.undo_zone(remove).unwrap_or_else(|error| panic!("{error}"));
        assert!(undone.is_none(), "nothing to undo: {undone:?}");
        let wheat = store.zones().read.into_iter().find(|zone| zone.id.0 == "wheat");
        assert_eq!(
            wheat.map(|zone| zone.maps),
            Some(zone("x", &[[7, 7]]).maps),
            "the zone it brought"
        );
    }

    #[test]
    fn only_the_zones_it_names_are_imported_and_a_new_id_is_never_one_it_names() {
        let folder = Folder::new("share-zones-named");
        let store = Store::new(folder.path().to_path_buf());
        let zones = folder.path().join("zones");
        let other = zone("wheat", &[[9, 9]]);
        zone::write(&zones, &zones.join("wheat.json"), &other).unwrap_or_else(|error| panic!("{error}"));
        // It names `wheat` and `wheat-2`, carries only `wheat`, and carries `stray`, which it does not name.
        let mut blueprint = harvest();
        blueprint.variables[0].default = serde_json::json!({"zone": "wheat-2"});
        let shared = Shared {
            blueprint,
            zones: vec![zone("wheat", &[[0, 0]]), zone("stray", &[[5, 5]])],
        };
        let id = store.import(shared).unwrap_or_else(|error| panic!("{error}"));
        let imported = store.blueprints().read.into_iter().find(|it| it.id == id);
        let imported = imported.unwrap_or_else(|| panic!("{id:?}"));
        let ids: Vec<_> = named(&imported).into_iter().map(|id| id.0).collect();
        assert_eq!(
            ids,
            ["bank", "wheat-2", "wheat-3"],
            "wheat-2 still names the zone it did"
        );
        let ids: Vec<_> = store.zones().read.into_iter().map(|zone| zone.id.0).collect();
        assert_eq!(ids, ["wheat-3", "wheat"], "no stray");
    }
}
