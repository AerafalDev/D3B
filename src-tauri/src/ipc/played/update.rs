//! A given blueprint's new version (`docs/bot.md`, "The concepts"): the characters that run it go on running it, their
//! settings carried as when another blueprint is chosen, and a value the new version cannot keep said once, in a notice
//! of the character's tab the player closes (the owner, 2026-11-16). A character's file keeps the version it runs, not
//! the variables of that version, which this one no longer ships: a value is kept when the new version's setting of
//! the same id accepts it (the owner, 2026-11-16).

use tauri_specta::Event;

use super::PlayedChanged;
use super::settings::{SettingError, changed};
use crate::bot::{self, Blueprint};
use crate::storage::{Played, StorageError, Store};

/// The version a character's file runs of D3B's blueprint: a file written before versions were kept ran the first.
fn ran(character: &Played) -> u32 {
    character.blueprint_version.unwrap_or(1)
}

/// The given blueprint `character` runs, when it has a newer version than the one its settings were carried to.
fn newer<'a>(character: &Played, given: &'a [Blueprint]) -> Option<&'a Blueprint> {
    let runs = character.blueprint.as_deref()?;
    let blueprint = given.iter().find(|blueprint| blueprint.id.0 == runs)?;
    (blueprint.version.get() > ran(character)).then_some(blueprint)
}

/// `character`'s settings carried to `blueprint`'s version, the values lost added to its notice.
fn carry(character: &mut Played, blueprint: &Blueprint) {
    let kept = bot::carried(&character.settings, None, &blueprint.variables);
    for (id, value) in &character.settings {
        let still = blueprint
            .variables
            .iter()
            .any(|variable| variable.id.0 == *id && variable.per_character && variable.accepts(value));
        if !still && !character.dropped.contains(id) {
            character.dropped.push(id.clone());
        }
    }
    character.dropped.sort();
    character.settings = kept;
    character.blueprint_version = Some(blueprint.version.get());
}

/// Brings every character running one of `given` to its version, once; how many were.
pub(crate) fn update(store: &Store, given: &[Blueprint]) -> usize {
    let mut updated = 0;
    for character in store.played().read {
        if newer(&character, given).is_none() {
            continue;
        }
        let mut carried_now = false;
        let carried = store.change_character(&character.key(), |character| {
            // Read again under the lock: the player may have chosen another blueprint since.
            if let Some(blueprint) = newer(character, given) {
                carry(character, blueprint);
                carried_now = true;
            }
            Ok::<(), StorageError>(())
        });
        match carried {
            Ok(()) if carried_now => updated += 1,
            Ok(()) => {}
            Err(error) => tauri_plugin_log::log::warn!(
                "{} is not brought to its blueprint's new version: {error}",
                character.name
            ),
        }
    }
    updated
}

/// At start, brings the characters to the versions of D3B's blueprints this version ships, away from the webview.
pub fn start(app: &tauri::AppHandle) {
    use tauri::Manager;
    let app = app.clone();
    tauri::async_runtime::spawn_blocking(move || {
        let given = match bot::given() {
            Ok(given) => given,
            Err(error) => {
                tauri_plugin_log::log::error!("D3B's blueprints do not read, no character is updated: {error}");
                return;
            }
        };
        let files = app.state::<super::super::blueprints::PlayersFiles>();
        let Some(store) = files.store() else { return };
        if update(store, &given) > 0 {
            let _ = PlayedChanged.emit(&app);
        }
    });
}

/// Closes the notice of the settings a new version of the character `key`'s blueprint could not keep.
#[tauri::command]
#[specta::specta]
pub async fn dismiss_dropped_settings(app: tauri::AppHandle, key: String) -> Result<(), SettingError> {
    changed(app, move |store| dismiss(store, &key)).await
}

fn dismiss(store: &Store, key: &str) -> Result<(), SettingError> {
    store.change_character(key, |character| {
        character.dropped.clear();
        Ok(())
    })
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;
    use std::num::NonZeroU32;

    use serde_json::{Value, json};

    use super::*;
    use crate::storage::CHARACTER_SCHEMA;
    use crate::storage::tests::Folder;

    /// Automatic harvest as this version ships it, version 1, and the fixture of its version 2: Keep gone, Bank at
    /// from 30 %, the nearest bank no longer the default.
    fn versions() -> (Blueprint, Blueprint) {
        let given = bot::given().unwrap_or_else(|error| panic!("{error}"));
        let first = given
            .into_iter()
            .find(|blueprint| blueprint.id.0 == "automatic-harvest")
            .unwrap_or_else(|| panic!("no automatic-harvest"));
        assert_eq!(first.version.get(), 1);
        let mut second = first.clone();
        second.version = NonZeroU32::MIN.saturating_add(1);
        second.variables.retain(|variable| variable.id.0 != "keep");
        // Its node gone, the deposit keeps nothing, typed on its pin.
        let graph = &mut second.graphs[0];
        graph.nodes.retain(|node| node.id.0 != "keep");
        graph.wires.retain(|wire| wire.from.0.0 != "keep");
        for node in graph.nodes.iter_mut().filter(|node| node.id.0 == "deposit") {
            node.inputs.insert(bot::Id("keep".to_owned()), json!("none"));
        }
        for variable in &mut second.variables {
            match variable.id.0.as_str() {
                "pods" => variable.bounds = Some([30, 100]),
                "bank" => variable.default = json!({"map": 1}),
                _ => {}
            }
        }
        assert_eq!(bot::problems(&second), [], "the fixture is a blueprint that runs");
        (first, second)
    }

    fn character(id: i64, blueprint: Option<&str>, settings: &[(&str, Value)]) -> Played {
        Played {
            schema: CHARACTER_SCHEMA,
            id,
            name: format!("C{id}"),
            level: 1,
            breed: 8,
            sex: None,
            last_seen: 1,
            map_id: None,
            blueprint: blueprint.map(str::to_owned),
            settings: settings
                .iter()
                .map(|(id, value)| ((*id).to_owned(), value.clone()))
                .collect(),
            blueprint_version: None,
            dropped: Vec::new(),
        }
    }

    fn read(store: &Store, id: i64) -> Played {
        let played = store.played().read;
        let found = played.into_iter().find(|character| character.id == id);
        found.unwrap_or_else(|| panic!("no character {id}"))
    }

    fn harvester() -> Played {
        character(
            0x1_0001,
            Some("automatic-harvest"),
            &[
                ("zone", json!({"sub_area": 445})),
                ("pods", json!(20)),
                ("bank", json!({"map": 1})),
                ("keep", json!("all")),
            ],
        )
    }

    #[test]
    fn a_character_running_a_given_blueprint_goes_on_with_its_new_version_and_says_what_it_dropped() {
        let folder = Folder::new("update-carried");
        let store = Store::new(folder.path().to_path_buf());
        store.remember(&harvester()).unwrap_or_else(|error| panic!("{error}"));
        let (_, second) = versions();
        assert_eq!(update(&store, std::slice::from_ref(&second)), 1);
        let after = read(&store, harvester().id);
        assert_eq!(after.blueprint.as_deref(), Some("automatic-harvest"), "still run");
        assert_eq!(after.blueprint_version, Some(2));
        assert_eq!(
            after.settings,
            BTreeMap::from([("zone".to_owned(), json!({"sub_area": 445}))]),
            "the zone kept; the bank now the default, so no longer its own"
        );
        assert_eq!(
            after.dropped,
            ["keep", "pods"],
            "Keep gone and 20 % out of its new bounds; the bank equal to the new default is no loss"
        );
    }

    #[test]
    fn the_update_happens_once_and_the_notice_stays_until_it_is_closed() {
        let folder = Folder::new("update-once");
        let store = Store::new(folder.path().to_path_buf());
        store.remember(&harvester()).unwrap_or_else(|error| panic!("{error}"));
        let (first, second) = versions();
        assert_eq!(update(&store, std::slice::from_ref(&second)), 1);
        let path = folder.path().join(format!("characters/{}.json", harvester().key()));
        let written = std::fs::read_to_string(&path).unwrap_or_default();
        assert_eq!(update(&store, std::slice::from_ref(&second)), 0, "already at version 2");
        assert_eq!(update(&store, &[first]), 0, "an older version takes nothing back");
        assert_eq!(std::fs::read_to_string(&path).unwrap_or_default(), written);
        assert_eq!(dismiss(&store, &harvester().key()), Ok(()));
        let after = read(&store, harvester().id);
        assert_eq!((after.dropped.len(), after.blueprint_version), (0, Some(2)));
        assert_eq!(dismiss(&store, "294-1"), Err(SettingError::NoSuchCharacter));
    }

    #[test]
    fn characters_on_another_blueprint_one_of_mine_or_none_are_not_touched() {
        let folder = Folder::new("update-untouched");
        let store = Store::new(folder.path().to_path_buf());
        let others = [
            character(0x2_0001, Some("hunt"), &[("pods", json!(20))]),
            character(0x3_0001, Some("my-harvest"), &[("keep", json!("all"))]),
            character(0x4_0001, None, &[]),
        ];
        for other in &others {
            store.remember(other).unwrap_or_else(|error| panic!("{error}"));
        }
        let (_, second) = versions();
        assert_eq!(update(&store, &[second]), 0);
        for other in others {
            assert_eq!(read(&store, other.id), other);
        }
    }

    #[test]
    fn a_setting_no_longer_set_per_character_is_lost_and_a_notice_left_open_is_not_doubled() {
        let (_, mut second) = versions();
        for variable in second.variables.iter_mut().filter(|variable| variable.id.0 == "bank") {
            variable.per_character = false;
        }
        let mut character = Played {
            dropped: vec!["keep".into()],
            ..harvester()
        };
        carry(&mut character, &second);
        assert_eq!(
            character.dropped,
            ["bank", "keep", "pods"],
            "the bank, its own no longer; keep once"
        );
        assert_eq!(character.settings.keys().collect::<Vec<_>>(), ["zone"]);
    }

    #[test]
    fn a_file_without_a_version_runs_the_first() {
        let (first, second) = versions();
        let before = harvester();
        assert_eq!(ran(&before), 1);
        assert!(newer(&before, std::slice::from_ref(&first)).is_none());
        assert!(newer(&before, std::slice::from_ref(&second)).is_some());
        let at_two = Played {
            blueprint_version: Some(2),
            ..before
        };
        assert!(newer(&at_two, &[second]).is_none());
    }
}
