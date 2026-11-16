//! A character's blueprint and its settings (`docs/bot.md`, "The concepts"; ADR 0014), the owner's choices of
//! 2026-11-16: none until the player chooses one; another blueprint keeps the values of its settings of the same id and
//! type; a value set back to the default is no longer the character's own, so it shows unmarked.

use serde::Serialize;
use serde_json::Value;
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use super::super::blueprints::PlayersFiles;
use super::PlayedChanged;
use crate::bot::{self, Blueprint};
use crate::storage::{StorageError, Store};

/// Why a character's blueprint or setting was not changed.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum SettingError {
    NoSuchCharacter,
    NoFolder,
    NoSuchBlueprint,
    /// The character's blueprint sets no such variable per character, or it runs none.
    NoSuchSetting,
    /// Not a value of the setting's type, or out of its bounds.
    Refused,
    /// The system could not read or write its file: its own words.
    NotSaved {
        reason: String,
    },
}

impl From<StorageError> for SettingError {
    fn from(error: StorageError) -> Self {
        match error {
            StorageError::NoSuchCharacter { .. } => Self::NoSuchCharacter,
            error => Self::NotSaved {
                reason: error.to_string(),
            },
        }
    }
}

/// D3B's blueprints, then mine: those a character may run.
fn runnable(store: &Store) -> Vec<Blueprint> {
    // D3B's own always read, a test says so; mine that do not read are the Library's to report.
    let mut blueprints = bot::given().unwrap_or_default();
    blueprints.extend(store.blueprints().read);
    blueprints
}

fn find<'a>(blueprints: &'a [Blueprint], id: &str) -> Option<&'a Blueprint> {
    blueprints.iter().find(|blueprint| blueprint.id.0 == id)
}

/// The character `key` runs `blueprint`, or none, keeping the values its settings of the same id and type had.
fn choose(store: &Store, key: &str, blueprint: Option<&str>) -> Result<(), SettingError> {
    let blueprints = runnable(store);
    let to = match blueprint {
        Some(id) => Some(find(&blueprints, id).ok_or(SettingError::NoSuchBlueprint)?),
        None => None,
    };
    store.change_character(key, |character| {
        let from = character
            .blueprint
            .as_deref()
            .and_then(|id| find(&blueprints, id))
            .map(|from| from.variables.as_slice());
        character.settings = to.map_or_else(Default::default, |to| {
            bot::carried(&character.settings, from, &to.variables)
        });
        character.blueprint = to.map(|to| to.id.0.clone());
        Ok(())
    })
}

/// The character `key`'s own value of its setting `variable`, as JSON; none, or the default, resets it.
fn set(store: &Store, key: &str, variable: &str, value: Option<&str>) -> Result<(), SettingError> {
    let value: Option<Value> = value
        .map(serde_json::from_str)
        .transpose()
        .map_err(|_| SettingError::Refused)?;
    let blueprints = runnable(store);
    store.change_character(key, |character| {
        let runs = character.blueprint.as_deref().ok_or(SettingError::NoSuchSetting)?;
        let blueprint = find(&blueprints, runs).ok_or(SettingError::NoSuchBlueprint)?;
        let setting = blueprint
            .variables
            .iter()
            .find(|it| it.id.0 == variable && it.per_character)
            .ok_or(SettingError::NoSuchSetting)?;
        match value {
            Some(value) if value != setting.default => {
                if !setting.accepts(&value) {
                    return Err(SettingError::Refused);
                }
                character.settings.insert(variable.to_owned(), value);
            }
            _ => {
                character.settings.remove(variable);
            }
        }
        Ok(())
    })
}

/// Runs `change` on the player's folder away from the webview's thread, and says the characters changed when it did.
async fn changed(
    app: tauri::AppHandle,
    change: impl FnOnce(&Store) -> Result<(), SettingError> + Send + 'static,
) -> Result<(), SettingError> {
    let handle = app.clone();
    let done = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        change(files.store().ok_or(SettingError::NoFolder)?)
    });
    let done = done.await.unwrap_or_else(|error| {
        Err(SettingError::NotSaved {
            reason: error.to_string(),
        })
    });
    if done.is_ok() {
        let _ = PlayedChanged.emit(&handle);
    }
    done
}

/// The character `key` runs the blueprint `blueprint`, or none.
#[tauri::command]
#[specta::specta]
pub async fn set_character_blueprint(
    app: tauri::AppHandle,
    key: String,
    blueprint: Option<String>,
) -> Result<(), SettingError> {
    changed(app, move |store| choose(store, &key, blueprint.as_deref())).await
}

/// The character `key`'s own value of its setting `variable`, as JSON; none resets it to the blueprint's default.
#[tauri::command]
#[specta::specta]
pub async fn set_character_setting(
    app: tauri::AppHandle,
    key: String,
    variable: String,
    value: Option<String>,
) -> Result<(), SettingError> {
    changed(app, move |store| set(store, &key, &variable, value.as_deref())).await
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use serde_json::json;

    use super::*;
    use crate::storage::tests::Folder;
    use crate::storage::{CHARACTER_SCHEMA, Played};

    // A character of server 294, the key of its file.
    const ID: i64 = 0x9e_447f_0126;
    const KEY: &str = "294-679754006822";

    #[test]
    fn the_key_is_the_characters() {
        assert_eq!(crate::storage::key_of(ID), KEY);
    }

    fn store(name: &str) -> (Folder, Store) {
        let folder = Folder::new(name);
        let store = Store::new(folder.path().to_path_buf());
        let character = Played {
            schema: CHARACTER_SCHEMA,
            id: ID,
            name: "Kalyx".into(),
            level: 54,
            breed: 8,
            sex: None,
            last_seen: 1,
            map_id: None,
            blueprint: None,
            settings: BTreeMap::new(),
        };
        store.remember(&character).unwrap_or_else(|error| panic!("{error}"));
        (folder, store)
    }

    fn runs(store: &Store) -> (Option<String>, BTreeMap<String, Value>) {
        let played = store.played().read;
        let character = played.first().unwrap_or_else(|| panic!("no character"));
        (character.blueprint.clone(), character.settings.clone())
    }

    #[test]
    fn a_character_runs_none_until_one_is_chosen_and_another_keeps_its_zone() {
        let (_folder, store) = store("settings-choose");
        assert_eq!(runs(&store), (None, BTreeMap::new()), "none at first");
        assert_eq!(choose(&store, KEY, Some("automatic-harvest")), Ok(()));
        for (variable, value) in [("zone", r#"{"sub_area":445}"#), ("pods", "80")] {
            assert_eq!(set(&store, KEY, variable, Some(value)), Ok(()));
        }
        assert_eq!(choose(&store, KEY, Some("job-harvest")), Ok(()));
        let (blueprint, settings) = runs(&store);
        assert_eq!(blueprint.as_deref(), Some("job-harvest"));
        assert_eq!(
            settings.get("zone"),
            Some(&json!({"sub_area": 445})),
            "the same id and type"
        );
        assert_eq!(settings.get("pods"), Some(&json!(80)));
        assert_eq!(choose(&store, KEY, None), Ok(()));
        assert_eq!(runs(&store), (None, BTreeMap::new()), "none, and so no settings");
    }

    #[test]
    fn a_setting_set_back_to_its_default_or_reset_is_no_longer_the_characters() {
        let (_folder, store) = store("settings-reset");
        assert_eq!(choose(&store, KEY, Some("automatic-harvest")), Ok(()));
        assert_eq!(set(&store, KEY, "pods", Some("80")), Ok(()));
        assert_eq!(set(&store, KEY, "pods", Some("90")), Ok(()), "the default");
        assert_eq!(runs(&store).1, BTreeMap::new());
        assert_eq!(set(&store, KEY, "pods", Some("70")), Ok(()));
        assert_eq!(set(&store, KEY, "pods", None), Ok(()));
        assert_eq!(runs(&store).1, BTreeMap::new());
    }

    #[test]
    fn a_value_out_of_its_type_or_bounds_and_an_unknown_setting_are_refused_and_nothing_written() {
        let (_folder, store) = store("settings-refused");
        assert_eq!(
            set(&store, KEY, "pods", Some("80")),
            Err(SettingError::NoSuchSetting),
            "no blueprint"
        );
        assert_eq!(choose(&store, KEY, Some("nothing")), Err(SettingError::NoSuchBlueprint));
        assert_eq!(choose(&store, KEY, Some("automatic-harvest")), Ok(()));
        for (variable, value, error) in [
            ("pods", "5", SettingError::Refused),
            ("pods", "\"80\"", SettingError::Refused),
            ("pods", "{", SettingError::Refused),
            ("zone", r#"{"sub_area":-1}"#, SettingError::Refused),
            ("job", "24", SettingError::NoSuchSetting),
        ] {
            assert_eq!(
                set(&store, KEY, variable, Some(value)),
                Err(error),
                "{variable} {value}"
            );
        }
        assert_eq!(runs(&store).1, BTreeMap::new());
        assert_eq!(
            choose(&store, "294-1", Some("hunt")),
            Err(SettingError::NoSuchCharacter)
        );
    }
}
