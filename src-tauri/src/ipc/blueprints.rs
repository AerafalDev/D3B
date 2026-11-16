//! The blueprints, as the Library lists them (`docs/bot.md`, "In the app"): D3B's, then mine from `Documents/D3B`, and
//! the files there that do not read; Duplicate, which saves a copy among mine; and the `.d3b` file (ADR 0017), a
//! blueprint exported through the system's save dialog, then previewed and imported among mine on another D3B.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_plugin_dialog::DialogExt;

use super::graph::ProblemView;
use crate::bot::{self, Blueprint, Id, ReadError};
use crate::storage::{self, ShareError, StorageError, Store, Why};

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

impl PlayersFiles {
    /// The player's files in `store`, for the tests.
    #[cfg(test)]
    pub(crate) fn new(store: Option<Store>) -> Self {
        Self(store)
    }

    /// The player's folder, when the system names one.
    pub(crate) fn store(&self) -> Option<&Store> {
        self.0.as_ref()
    }
}

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
    /// Its variables, those set per character first shown as its settings.
    pub variables: Vec<VariableView>,
}

/// A variable of a blueprint, as the Library's preview shows it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct VariableView {
    pub id: String,
    /// The file's name; a given blueprint's is read in the translations, `given.<id>.variables.<variable>.name`.
    pub name: String,
    /// Its type, as the file writes it: `zone`, `percentage`, `bank`…
    pub value_type: String,
    /// Its default as JSON, which the interface reads by its type; `None` when the blueprint gives none.
    pub default: Option<String>,
    /// The smallest and largest value of a number or a percentage.
    pub bounds: Option<[i32; 2]>,
    pub per_character: bool,
    pub memory: bool,
    pub help: String,
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
    pub why: KeptWhy,
}

/// Why a file is kept, for the interface to say in the player's language.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum KeptWhy {
    /// The system could not read it: its own words.
    Unreadable {
        reason: String,
    },
    NotText,
    /// Written by a newer D3B, of this schema.
    Newer {
        found: u32,
    },
    /// Not of a blueprint's shape: what serde says, with its line and column.
    NotABlueprint {
        detail: String,
    },
    /// It holds another id than its name says.
    NameIsNotId {
        id: String,
    },
    /// It holds the id of one of D3B's blueprints.
    GivenId {
        id: String,
    },
    /// Not of a character's shape: what serde says.
    NotACharacter {
        detail: String,
    },
}

impl From<&Why> for KeptWhy {
    fn from(why: &Why) -> Self {
        match why {
            Why::Unreadable(error) => Self::Unreadable {
                reason: error.to_string(),
            },
            Why::NotText => Self::NotText,
            Why::NotRead(ReadError::Newer { found }) | Why::NewerCharacter { found } => Self::Newer {
                found: u32::try_from(*found).unwrap_or(u32::MAX),
            },
            Why::NotRead(ReadError::NotABlueprint(error)) => Self::NotABlueprint {
                detail: error.to_string(),
            },
            Why::NameIsNotId { id } => Self::NameIsNotId { id: id.0.clone() },
            Why::GivenId { id } => Self::GivenId { id: id.0.clone() },
            Why::NotACharacter(detail) => Self::NotACharacter { detail: detail.clone() },
            Why::KeyIsNotId { id } => Self::NameIsNotId { id: id.to_string() },
        }
    }
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
    /// The system could not read the folder or write the copy: its own words.
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
        variables: blueprint
            .variables
            .iter()
            .map(|variable| VariableView {
                id: variable.id.0.clone(),
                name: variable.name.clone(),
                value_type: serde_json::to_value(variable.value_type)
                    .ok()
                    .and_then(|value| value.as_str().map(str::to_owned))
                    .unwrap_or_default(),
                default: (!variable.default.is_null()).then(|| variable.default.to_string()),
                // Bounds a file can hold beyond a 32-bit number read as its ends.
                bounds: variable.bounds.map(|bounds| {
                    bounds.map(|end| i32::try_from(end).unwrap_or(if end < 0 { i32::MIN } else { i32::MAX }))
                }),
                per_character: variable.per_character,
                memory: variable.memory,
                help: variable.help.clone(),
            })
            .collect(),
    }
}

/// D3B's blueprints, then mine in the order of their files, and the files kept; an error only for a bug of this
/// version, D3B's own not reading.
fn list(store: Option<&Store>) -> Result<BlueprintList, String> {
    let given = bot::given().map_err(|error| format!("D3B's blueprints do not read: {error}"))?;
    let mut blueprints: Vec<_> = given.iter().map(|blueprint| summary(blueprint, true)).collect();
    let mut kept = Vec::new();
    if let Some(store) = store {
        let mine = store.blueprints();
        blueprints.extend(mine.read.iter().map(|blueprint| summary(blueprint, false)));
        kept = mine
            .kept
            .iter()
            .map(|file| KeptFile {
                path: file.path.display().to_string(),
                why: (&file.why).into(),
            })
            .collect();
    }
    Ok(BlueprintList { blueprints, kept })
}

fn duplicate(store: Option<&Store>, id: &str, texts: CopyTexts) -> Result<String, DuplicateError> {
    let store = store.ok_or(DuplicateError::NoFolder)?;
    match store.duplicate(id, texts_of(texts)) {
        Ok(id) => Ok(id.0),
        Err(StorageError::NoSuchBlueprint { .. }) => Err(DuplicateError::NoSuchBlueprint),
        Err(error) => Err(DuplicateError::NotSaved {
            reason: error.to_string(),
        }),
    }
}

/// The Library's blueprints and the files kept.
#[tauri::command]
#[specta::specta]
pub async fn blueprints(files: tauri::State<'_, PlayersFiles>) -> Result<BlueprintList, String> {
    let store = files.0.clone();
    let listed = tauri::async_runtime::spawn_blocking(move || list(store.as_ref())).await;
    listed.map_err(|error| error.to_string())?
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

/// A `.d3b` file read, as the import's preview shows it before anything is saved.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct SharedPreview {
    /// The file, which the import reads again.
    pub path: String,
    /// Its blueprint as the Library would list it, as one of mine.
    pub blueprint: BlueprintSummary,
    /// Its problems, which it keeps once imported.
    pub problems: Vec<ProblemView>,
    /// What it holds: its graphs, their nodes, and the zones and routes that come with it.
    pub graphs: u32,
    pub nodes: u32,
    pub zones: u32,
    pub routes: u32,
}

/// Why a blueprint was not exported, or a `.d3b` not previewed or imported.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ShareFailure {
    /// The system names no folder for the player's files.
    NoFolder,
    /// No blueprint has this id.
    NoSuchBlueprint,
    /// The file's name does not end in `.d3b`.
    NotD3b,
    /// The system could not read it: its own words.
    Unreadable {
        reason: String,
    },
    NotText,
    /// Written by a newer D3B, of this format.
    Newer {
        found: u32,
    },
    /// Its blueprint written by a newer D3B, of this schema.
    NewerSchema {
        found: u32,
    },
    /// Not of a `.d3b`'s shape, or its blueprint not of a blueprint's: what serde says, with its line and column.
    NotOfItsShape {
        detail: String,
    },
    /// It holds zones or routes, which this version does not import.
    HoldsZonesOrRoutes,
    /// The system could not write the file or the blueprint: its own words.
    NotSaved {
        reason: String,
    },
}

impl From<ShareError> for ShareFailure {
    fn from(error: ShareError) -> Self {
        match error {
            ShareError::NotD3b => Self::NotD3b,
            ShareError::Unreadable(error) => Self::Unreadable {
                reason: error.to_string(),
            },
            ShareError::NotText => Self::NotText,
            ShareError::Newer { found } => Self::Newer {
                found: u32::try_from(found).unwrap_or(u32::MAX),
            },
            ShareError::Blueprint(ReadError::Newer { found }) => Self::NewerSchema {
                found: u32::try_from(found).unwrap_or(u32::MAX),
            },
            ShareError::NotOfItsShape(error) | ShareError::Blueprint(ReadError::NotABlueprint(error)) => {
                Self::NotOfItsShape {
                    detail: error.to_string(),
                }
            }
            ShareError::HoldsZonesOrRoutes => Self::HoldsZonesOrRoutes,
        }
    }
}

impl From<StorageError> for ShareFailure {
    fn from(error: StorageError) -> Self {
        match error {
            StorageError::NoSuchBlueprint { .. } => Self::NoSuchBlueprint,
            error => Self::NotSaved {
                reason: error.to_string(),
            },
        }
    }
}

fn texts_of(texts: CopyTexts) -> bot::Texts {
    let variables = texts
        .variables
        .into_iter()
        .map(|(id, it)| (Id(id), (it.name, it.help)))
        .collect();
    bot::Texts {
        name: texts.name,
        description: texts.description,
        variables,
    }
}

/// The `.d3b` sharing the blueprint `id`: the name its file is offered under, and its text.
fn exported(store: Option<&Store>, id: &str, texts: Option<CopyTexts>) -> Result<(String, String), ShareFailure> {
    let store = store.ok_or(ShareFailure::NoFolder)?;
    let blueprint = store.shared(id, texts.map(texts_of))?;
    Ok((
        storage::shared_file_name(&blueprint.name),
        storage::write_shared(&blueprint),
    ))
}

fn count(n: usize) -> u32 {
    u32::try_from(n).unwrap_or(u32::MAX)
}

fn preview(path: &Path) -> Result<SharedPreview, ShareFailure> {
    let blueprint = storage::read_shared(path)?;
    Ok(SharedPreview {
        path: path.display().to_string(),
        blueprint: summary(&blueprint, false),
        problems: bot::problems(&blueprint).iter().map(ProblemView::from).collect(),
        graphs: count(blueprint.graphs.len()),
        nodes: count(blueprint.graphs.iter().map(|graph| graph.nodes.len()).sum()),
        zones: 0,
        routes: 0,
    })
}

fn import(store: Option<&Store>, path: &Path) -> Result<String, ShareFailure> {
    let store = store.ok_or(ShareFailure::NoFolder)?;
    let blueprint = storage::read_shared(path)?;
    Ok(store.import(blueprint)?.0)
}

/// Runs `job` off the async runtime: the dialogs and the files block.
async fn blocking<T: Send + 'static>(
    job: impl FnOnce() -> Result<T, ShareFailure> + Send + 'static,
) -> Result<T, ShareFailure> {
    tauri::async_runtime::spawn_blocking(job).await.unwrap_or_else(|error| {
        Err(ShareFailure::NotSaved {
            reason: error.to_string(),
        })
    })
}

/// Export: the blueprint `id`, D3B's (worded by `texts`, the player's words for its translations) or mine, written
/// where the player chooses in the system's save dialog; the file's path, or none when the dialog was cancelled.
#[tauri::command]
#[specta::specta]
pub async fn export_blueprint(
    window: tauri::Window,
    id: String,
    texts: Option<CopyTexts>,
    files: tauri::State<'_, PlayersFiles>,
) -> Result<Option<String>, ShareFailure> {
    let store = files.0.clone();
    blocking(move || {
        let (name, text) = exported(store.as_ref(), &id, texts)?;
        let chosen = window
            .dialog()
            .file()
            .set_parent(&window)
            .set_file_name(name)
            .add_filter("D3B", &[storage::SHARE_EXTENSION])
            .blocking_save_file();
        let Some(mut path) = chosen.and_then(|chosen| chosen.into_path().ok()) else {
            return Ok(None);
        };
        // A name the player typed without its extension (GTK's dialog keeps it so) still imports as a `.d3b`.
        if !path
            .extension()
            .is_some_and(|extension| extension.eq_ignore_ascii_case(storage::SHARE_EXTENSION))
        {
            path.as_mut_os_string().push(format!(".{}", storage::SHARE_EXTENSION));
        }
        storage::write_whole(&path, text.as_bytes()).map_err(|error| ShareFailure::NotSaved {
            reason: error.to_string(),
        })?;
        Ok(Some(path.display().to_string()))
    })
    .await
}

/// Import, first step: a `.d3b` chosen in the system's open dialog, previewed; none when the dialog was cancelled.
#[tauri::command]
#[specta::specta]
pub async fn pick_shared(window: tauri::Window) -> Result<Option<SharedPreview>, ShareFailure> {
    blocking(move || {
        let chosen = window
            .dialog()
            .file()
            .set_parent(&window)
            .add_filter("D3B", &[storage::SHARE_EXTENSION])
            .blocking_pick_file();
        chosen
            .and_then(|chosen| chosen.into_path().ok())
            .map(|path| preview(&path))
            .transpose()
    })
    .await
}

/// The preview of the `.d3b` at `path`, dropped on the window.
#[tauri::command]
#[specta::specta]
pub async fn preview_shared(path: String) -> Result<SharedPreview, ShareFailure> {
    blocking(move || preview(&PathBuf::from(path))).await
}

/// Import: the blueprint of the `.d3b` at `path`, read again, saved among mine under a free id; that id.
#[tauri::command]
#[specta::specta]
pub async fn import_shared(path: String, files: tauri::State<'_, PlayersFiles>) -> Result<String, ShareFailure> {
    let store = files.0.clone();
    blocking(move || import(store.as_ref(), &PathBuf::from(path))).await
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
        let list = list(None).unwrap_or_else(|error| panic!("{error}"));
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
        let settings: Vec<_> = list.blueprints[0]
            .variables
            .iter()
            .map(|it| {
                (
                    it.id.as_str(),
                    it.value_type.as_str(),
                    it.default.as_deref(),
                    it.per_character,
                )
            })
            .collect();
        assert_eq!(
            settings[0],
            ("zone", "zone", None, true),
            "no default: each character chooses"
        );
        assert_eq!(settings[1], ("pods", "percentage", Some("90"), true));
        assert_eq!(list.blueprints[0].variables[1].bounds, Some([10, 100]));
        // Bounds beyond a 32-bit number read as its ends.
        let mut wide = bot::given().unwrap_or_default().remove(0);
        wide.variables[1].bounds = Some([-5_000_000_000, 5_000_000_000]);
        assert_eq!(summary(&wide, true).variables[1].bounds, Some([i32::MIN, i32::MAX]));
        assert_eq!(
            settings[2],
            ("bank", "bank", Some("\"nearest\""), true),
            "the default as JSON"
        );
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
        let list = list(Some(&store)).unwrap_or_else(|error| panic!("{error}"));
        let mine: Vec<_> = list.blueprints.iter().filter(|it| !it.given).collect();
        assert_eq!(mine.len(), 2);
        let first = mine
            .iter()
            .find(|it| it.id == "recolte-automatique-copie")
            .unwrap_or_else(|| panic!("{mine:?}"));
        assert_eq!(
            (first.name.as_str(), first.description.as_str(), first.problems),
            ("Récolte automatique (copie)", "À moi.", 0)
        );
        let based = first.based_on.as_ref().map(|based| (based.id.as_str(), based.version));
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
        let list = list(Some(&Store::new(folder.path().to_path_buf()))).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(list.kept.len(), 1);
        assert!(list.kept[0].path.ends_with("broken.json"), "{:?}", list.kept);
        assert!(
            matches!(&list.kept[0].why, KeptWhy::NotABlueprint { detail } if detail.contains("line 1")),
            "{:?}",
            list.kept
        );
    }

    #[test]
    fn a_blueprint_exported_is_previewed_then_imported_the_same_among_mine() {
        let folder = Folder::new("ipc-share");
        let store = Store::new(folder.path().join("D3B"));
        let mut words = texts("Récolte automatique");
        words.description = "Récolte une zone.".into();
        let exported_ = exported(Some(&store), "automatic-harvest", Some(words));
        let (name, text) = exported_.unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!(name, "Récolte automatique.d3b");
        let path = folder.path().join(&name);
        std::fs::write(&path, &text).unwrap_or_else(|error| panic!("{error}"));
        let shown = preview(&path).unwrap_or_else(|error| panic!("{error:?}"));
        let blueprint = &shown.blueprint;
        assert_eq!(
            (
                blueprint.id.as_str(),
                blueprint.name.as_str(),
                blueprint.given,
                blueprint.problems
            ),
            ("recolte-automatique", "Récolte automatique", false, 0)
        );
        let based = blueprint
            .based_on
            .as_ref()
            .map(|based| (based.id.as_str(), based.version));
        assert_eq!(based, Some(("automatic-harvest", 1)));
        assert_eq!(blueprint.variables[0].name, "Zone", "the player's words");
        let given = &bot::given().unwrap_or_default()[0];
        let nodes: usize = given.graphs.iter().map(|graph| graph.nodes.len()).sum();
        assert_eq!((shown.graphs, shown.nodes), (count(given.graphs.len()), count(nodes)));
        assert!(shown.nodes > shown.graphs, "nodes, not graphs");
        assert_eq!((shown.problems.len(), shown.zones, shown.routes), (0, 0, 0));
        assert_eq!(import(Some(&store), &path), Ok("recolte-automatique".into()));
        assert_eq!(
            import(Some(&store), &path),
            Ok("recolte-automatique-2".into()),
            "never over a file"
        );
        let list = list(Some(&store)).unwrap_or_else(|error| panic!("{error}"));
        let mine: Vec<_> = list.blueprints.iter().filter(|it| !it.given).collect();
        assert_eq!(mine.len(), 2);
        assert_eq!(
            (mine[0].name.as_str(), &mine[0].variables, &mine[0].based_on),
            (blueprint.name.as_str(), &blueprint.variables, &blueprint.based_on),
            "the same blueprint"
        );
        let again = exported(Some(&store), "recolte-automatique", None);
        assert_eq!(again.map(|(_, text)| text), Ok(text), "mine exported as it is");
    }

    #[test]
    fn a_file_that_does_not_read_is_refused_with_why_and_nothing_saved() {
        let folder = Folder::new("ipc-share-refused");
        let store = Store::new(folder.path().join("D3B"));
        let put = |name: &str, text: &str| {
            let path = folder.path().join(name);
            std::fs::write(&path, text).unwrap_or_else(|error| panic!("{error}"));
            path
        };
        let (_, text) = exported(Some(&store), "hunt", None).unwrap_or_else(|error| panic!("{error:?}"));
        let newer = put("newer.d3b", &text.replacen("\"d3b\": 1", "\"d3b\": 3", 1));
        assert_eq!(preview(&newer), Err(ShareFailure::Newer { found: 3 }));
        let schema = put("schema.d3b", &text.replacen("\"schema\": 1", "\"schema\": 2", 1));
        assert_eq!(
            import(Some(&store), &schema),
            Err(ShareFailure::NewerSchema { found: 2 })
        );
        let broken = put("broken.d3b", "{\"d3b\": 1,");
        assert!(
            matches!(preview(&broken), Err(ShareFailure::NotOfItsShape { detail }) if detail.contains("line 1")),
            "with its line"
        );
        let json = put("hunt.json", &text);
        assert_eq!(import(Some(&store), &json), Err(ShareFailure::NotD3b));
        assert!(matches!(
            preview(&folder.path().join("gone.d3b")),
            Err(ShareFailure::Unreadable { .. })
        ));
        let zones = put("zones.d3b", &text.replacen("\"zones\": []", "\"zones\": [1]", 1));
        assert_eq!(preview(&zones), Err(ShareFailure::HoldsZonesOrRoutes));
        assert_eq!(import(None, &newer), Err(ShareFailure::NoFolder));
        assert_eq!(
            exported(Some(&store), "teleport", None),
            Err(ShareFailure::NoSuchBlueprint)
        );
        assert!(!folder.path().join("D3B").exists(), "nothing saved");
    }
}
