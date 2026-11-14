//! The blueprints, as the Library lists them (`docs/bot.md`, "In the app"): D3B's, then mine from `Documents/D3B`, and
//! the files there that do not read; and Duplicate, which saves a copy among mine.

use std::collections::{BTreeMap, HashSet};

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;

use crate::bot::{self, Blueprint, Id};
use crate::storage::{StorageError, Store};

/// The player's folder: `Documents/D3B`, or the app's data folder on a system without a documents folder (ADR 0015).
pub fn open_store(app: &tauri::AppHandle) -> PlayersFiles {
    let paths = app.path();
    let root = paths
        .document_dir()
        .map(|documents| documents.join("D3B"))
        .or_else(|_| paths.app_data_dir());
    PlayersFiles(root.ok().map(Store::new))
}

/// The player's files, or none when the system names no folder for them.
pub struct PlayersFiles(Option<Store>);

/// A blueprint as the Library lists it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct BlueprintSummary {
    pub id: String,
    /// The file's name; a given blueprint's is read in the interface's translations, `given.<id>.name`.
    pub name: String,
    pub description: String,
    /// D3B's own, locked.
    pub given: bool,
    pub version: u32,
    /// The given blueprint a copy is based on, and its version then.
    pub based_on: Option<BasedOnView>,
    /// How many problems keep it from running.
    pub problems: u32,
}

/// What a copy is based on.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct BasedOnView {
    pub id: String,
    pub version: u32,
}

/// A file of the player's folder kept as it is, and why.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct KeptFile {
    pub path: String,
    pub why: String,
}

/// The Library's content.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct BlueprintList {
    pub blueprints: Vec<BlueprintSummary>,
    pub kept: Vec<KeptFile>,
}

/// A copy's texts in the player's language: its name, description, and each variable's name and help by id.
#[derive(Debug, Clone, Deserialize, Type)]
pub struct CopyTexts {
    pub name: String,
    pub description: String,
    pub variables: BTreeMap<String, VariableTexts>,
}

/// A variable's name and help line.
#[derive(Debug, Clone, Deserialize, Type)]
pub struct VariableTexts {
    pub name: String,
    pub help: String,
}

/// Why Duplicate made nothing.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum DuplicateError {
    /// No blueprint has this id.
    NoSuchBlueprint,
    /// The system names no folder for the player's files.
    NoFolder,
    /// The copy could not be saved: the reason.
    NotSaved { reason: String },
}

fn summary(blueprint: &Blueprint, given: bool) -> BlueprintSummary {
    BlueprintSummary {
        id: blueprint.id.0.clone(),
        name: blueprint.name.clone(),
        description: blueprint.description.clone(),
        given,
        version: blueprint.version.get(),
        based_on: blueprint.based_on.as_ref().map(|based| BasedOnView {
            id: based.id.0.clone(),
            version: based.version.get(),
        }),
        problems: u32::try_from(bot::problems(blueprint).len()).unwrap_or(u32::MAX),
    }
}

fn given() -> Vec<Blueprint> {
    // The given blueprints' tests hold them readable: none is ever missing.
    bot::given().unwrap_or_default()
}

/// D3B's blueprints, then mine in the order of their files, and the files kept.
fn list(store: Option<&Store>) -> BlueprintList {
    let mut blueprints: Vec<_> = given().iter().map(|blueprint| summary(blueprint, true)).collect();
    let mut kept = Vec::new();
    if let Some(store) = store {
        let mine = store.blueprints();
        blueprints.extend(mine.read.iter().map(|blueprint| summary(blueprint, false)));
        kept = mine
            .kept
            .iter()
            .map(|file| KeptFile {
                path: file.path.display().to_string(),
                why: file.why.to_string(),
            })
            .collect();
    }
    BlueprintList { blueprints, kept }
}

/// Saves a copy of the blueprint `id` among mine, with `texts`; gives back the copy's id.
fn duplicate(store: Option<&Store>, id: &str, texts: CopyTexts) -> Result<String, DuplicateError> {
    let store = store.ok_or(DuplicateError::NoFolder)?;
    let (given, mine) = (given(), store.blueprints().read);
    let source = given
        .iter()
        .map(|it| (it, true))
        .chain(mine.iter().map(|it| (it, false)))
        .find(|(it, _)| it.id.0 == id);
    let (source, is_given) = source.ok_or(DuplicateError::NoSuchBlueprint)?;
    let taken: HashSet<Id> = given.iter().chain(&mine).map(|it| it.id.clone()).collect();
    let copy_id = bot::id_for(&texts.name, &taken);
    let variables = texts
        .variables
        .into_iter()
        .map(|(id, it)| (Id(id), (it.name, it.help)))
        .collect();
    let texts = bot::Texts {
        name: texts.name,
        description: texts.description,
        variables,
    };
    let copy = bot::copy(source, is_given, copy_id.clone(), texts);
    store
        .save_blueprint(&copy)
        .map_err(|error: StorageError| DuplicateError::NotSaved {
            reason: error.to_string(),
        })?;
    Ok(copy_id.0)
}

/// The Library's blueprints and the files kept.
#[tauri::command]
#[specta::specta]
pub async fn blueprints(files: tauri::State<'_, PlayersFiles>) -> Result<BlueprintList, ()> {
    let store = files.0.clone();
    Ok(tauri::async_runtime::spawn_blocking(move || list(store.as_ref()))
        .await
        .unwrap_or_else(|_| list(None)))
}

/// Duplicate: a copy of the blueprint `id` among mine, named and worded by `texts`; its id.
#[tauri::command]
#[specta::specta]
pub async fn duplicate_blueprint(
    id: String,
    texts: CopyTexts,
    files: tauri::State<'_, PlayersFiles>,
) -> Result<String, DuplicateError> {
    let store = files.0.clone();
    let saved = tauri::async_runtime::spawn_blocking(move || duplicate(store.as_ref(), &id, texts)).await;
    saved.unwrap_or_else(|error| {
        Err(DuplicateError::NotSaved {
            reason: error.to_string(),
        })
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn texts(name: &str) -> CopyTexts {
        let zone = VariableTexts {
            name: "Zone".into(),
            help: "Où récolter.".into(),
        };
        CopyTexts {
            name: name.into(),
            description: "À moi.".into(),
            variables: BTreeMap::from([("zone".into(), zone)]),
        }
    }

    #[test]
    fn the_library_lists_d3bs_blueprints_first_without_problems() {
        let list = list(None);
        let ids: Vec<_> = list
            .blueprints
            .iter()
            .map(|it| (it.id.as_str(), it.given, it.problems))
            .collect();
        assert_eq!(
            ids,
            [
                ("automatic-harvest", true, 0),
                ("job-harvest", true, 0),
                ("hunt", true, 0)
            ]
        );
        assert_eq!(list.kept, []);
    }

    #[test]
    fn duplicate_saves_a_copy_among_mine_based_on_the_given_one() {
        let folder = Folder::new("ipc-duplicate");
        let store = Store::new(folder.path().to_path_buf());
        let copied = duplicate(Some(&store), "automatic-harvest", texts("Récolte automatique (copie)"));
        assert_eq!(copied, Ok("recolte-automatique-copie".to_owned()));
        let again = duplicate(Some(&store), "automatic-harvest", texts("Récolte automatique (copie)"));
        assert_eq!(
            again,
            Ok("recolte-automatique-copie-2".to_owned()),
            "an id taken gets a number"
        );
        let list = list(Some(&store));
        let mine: Vec<_> = list.blueprints.iter().filter(|it| !it.given).collect();
        assert_eq!(mine.len(), 2);
        assert_eq!(
            (mine[0].name.as_str(), mine[0].problems),
            ("Récolte automatique (copie)", 0)
        );
        let based = mine[0]
            .based_on
            .as_ref()
            .map(|based| (based.id.as_str(), based.version));
        assert_eq!(based, Some(("automatic-harvest", 1)));
        assert!(folder.path().join("blueprints/recolte-automatique-copie.json").exists());
    }

    #[test]
    fn duplicate_refuses_an_unknown_blueprint_and_a_missing_folder() {
        let folder = Folder::new("ipc-duplicate-refused");
        let store = Store::new(folder.path().to_path_buf());
        assert_eq!(
            duplicate(Some(&store), "teleport", texts("Copie")),
            Err(DuplicateError::NoSuchBlueprint)
        );
        assert_eq!(duplicate(None, "hunt", texts("Copie")), Err(DuplicateError::NoFolder));
    }

    #[test]
    fn the_files_that_do_not_read_are_listed_with_why() {
        let folder = Folder::new("ipc-kept");
        std::fs::create_dir_all(folder.path().join("blueprints")).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(folder.path().join("blueprints/broken.json"), "{").unwrap_or_else(|error| panic!("{error}"));
        let list = list(Some(&Store::new(folder.path().to_path_buf())));
        assert_eq!(list.kept.len(), 1);
        assert!(
            list.kept[0].path.ends_with("broken.json") && list.kept[0].why.contains("not a blueprint"),
            "{:?}",
            list.kept
        );
    }
}
