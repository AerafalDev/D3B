//! My zones (`docs/bot.md`, "In the app"; the owner's choices of 2026-11-16): listed with the files kept, created empty
//! on the world the World tab shows, painted a stroke at a time, renamed, and deleted into the system's trash.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;

use super::blueprints::{KeptFile, PlayersFiles};
use super::played::to_trash;
use crate::storage::{StorageError, Store, Zone};

/// A zone of mine, as the World view, the World tab and the Zone field show it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct ZoneView {
    pub id: String,
    pub name: String,
    /// The world its maps are on.
    pub world: i32,
    /// Its maps' coordinates on that world, each once, in order.
    pub maps: Vec<[i16; 2]>,
}

/// My zones, in the order of their files, and the files kept as they are.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct ZoneList {
    pub zones: Vec<ZoneView>,
    pub kept: Vec<KeptFile>,
}

/// A stroke on the map: the maps the brush or the whole sub-area adds, or the eraser takes away.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize, Type)]
pub struct Stroke {
    pub maps: Vec<[i16; 2]>,
    pub erase: bool,
}

/// Why a zone was not created, changed or deleted.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ZoneError {
    /// No zone of mine has this id.
    NoSuchZone,
    /// The system names no folder for the player's files.
    NoFolder,
    /// A name of nothing but spaces.
    EmptyName,
    /// The system could not read or write its file, or it is kept as it is: why, in its own words.
    NotSaved { reason: String },
}

impl From<StorageError> for ZoneError {
    fn from(error: StorageError) -> Self {
        match error {
            StorageError::NoSuchZone { .. } => Self::NoSuchZone,
            error => Self::NotSaved {
                reason: error.to_string(),
            },
        }
    }
}

fn view(zone: Zone) -> ZoneView {
    ZoneView {
        id: zone.id.0,
        name: zone.name,
        world: zone.world,
        maps: zone.maps.into_iter().collect(),
    }
}

fn store(files: &PlayersFiles) -> Result<&Store, ZoneError> {
    files.store().ok_or(ZoneError::NoFolder)
}

/// `name` without the spaces around it, refused when nothing is left.
fn trimmed(name: &str) -> Result<&str, ZoneError> {
    let name = name.trim();
    if name.is_empty() {
        Err(ZoneError::EmptyName)
    } else {
        Ok(name)
    }
}

fn listed(files: &PlayersFiles) -> ZoneList {
    let Some(store) = files.store() else {
        return ZoneList {
            zones: Vec::new(),
            kept: Vec::new(),
        };
    };
    let zones = store.zones();
    let kept = zones.kept.iter().map(|file| {
        tauri_plugin_log::log::warn!("{} is kept as it is: {}", file.path.display(), file.why);
        KeptFile {
            path: file.path.display().to_string(),
            why: (&file.why).into(),
        }
    });
    ZoneList {
        kept: kept.collect(),
        zones: zones.read.into_iter().map(view).collect(),
    }
}

fn created(files: &PlayersFiles, name: &str, world: i32) -> Result<ZoneView, ZoneError> {
    Ok(view(store(files)?.create_zone(trimmed(name)?, world)?))
}

fn renamed(files: &PlayersFiles, id: &str, name: &str) -> Result<ZoneView, ZoneError> {
    let name = trimmed(name)?;
    let zone = store(files)?.change_zone(id, |zone| {
        name.clone_into(&mut zone.name);
        Ok::<_, ZoneError>(())
    })?;
    Ok(view(zone))
}

fn painted(files: &PlayersFiles, id: &str, stroke: &Stroke) -> Result<ZoneView, ZoneError> {
    let zone = store(files)?.change_zone(id, |zone| {
        if stroke.erase {
            let erased: std::collections::BTreeSet<_> = stroke.maps.iter().collect();
            zone.maps.retain(|map| !erased.contains(map));
        } else {
            zone.maps.extend(&stroke.maps);
        }
        Ok::<_, ZoneError>(())
    })?;
    Ok(view(zone))
}

/// Runs `work` with the player's files off the main thread, as a file's write may take its time.
async fn with_files<T: Send + 'static>(
    app: tauri::AppHandle,
    work: impl FnOnce(&PlayersFiles) -> Result<T, ZoneError> + Send + 'static,
) -> Result<T, ZoneError> {
    let done = tauri::async_runtime::spawn_blocking(move || work(&app.state::<PlayersFiles>()));
    done.await.unwrap_or_else(|error| {
        Err(ZoneError::NotSaved {
            reason: error.to_string(),
        })
    })
}

/// My zones and the files kept.
#[tauri::command]
#[specta::specta]
pub async fn zones(app: tauri::AppHandle) -> Result<ZoneList, ZoneError> {
    with_files(app, |files| Ok(listed(files))).await
}

/// A new zone named `name`, without maps, on `world`.
#[tauri::command]
#[specta::specta]
pub async fn create_zone(app: tauri::AppHandle, name: String, world: i32) -> Result<ZoneView, ZoneError> {
    with_files(app, move |files| created(files, &name, world)).await
}

/// The zone `id` named `name`; its id stays.
#[tauri::command]
#[specta::specta]
pub async fn rename_zone(app: tauri::AppHandle, id: String, name: String) -> Result<ZoneView, ZoneError> {
    with_files(app, move |files| renamed(files, &id, &name)).await
}

/// A stroke on the zone `id`, saved at once: the zone as it now is.
#[tauri::command]
#[specta::specta]
pub async fn paint_zone(app: tauri::AppHandle, id: String, stroke: Stroke) -> Result<ZoneView, ZoneError> {
    with_files(app, move |files| painted(files, &id, &stroke)).await
}

/// The file of the zone `id` to the system's trash, without asking (the owner, 2026-11-16, as Forget).
#[tauri::command]
#[specta::specta]
pub async fn delete_zone(app: tauri::AppHandle, id: String) -> Result<(), ZoneError> {
    with_files(app, move |files| Ok(store(files)?.delete_zone(&id, to_trash)?)).await
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn files(folder: &Folder) -> PlayersFiles {
        PlayersFiles::new(Some(Store::new(folder.path().to_path_buf())))
    }

    #[test]
    fn a_zone_is_created_painted_erased_and_renamed_its_id_kept() {
        let folder = Folder::new("ipc-zones");
        let files = files(&folder);
        let zone = created(&files, "  Ma forêt ", 1).unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!(
            (zone.id.as_str(), zone.name.as_str(), zone.world),
            ("ma-foret", "Ma forêt", 1)
        );
        let brush = Stroke {
            maps: vec![[5, -18], [4, -18], [4, -17]],
            erase: false,
        };
        let zone = painted(&files, "ma-foret", &brush).unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!(zone.maps, [[4, -18], [4, -17], [5, -18]]);
        let eraser = Stroke {
            maps: vec![[4, -17], [9, 9]],
            erase: true,
        };
        let zone = painted(&files, "ma-foret", &eraser).unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!(zone.maps, [[4, -18], [5, -18]]);
        let zone = renamed(&files, "ma-foret", "Bois").unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!((zone.id.as_str(), zone.name.as_str()), ("ma-foret", "Bois"));
        assert_eq!(listed(&files).zones, [zone]);
    }

    #[test]
    fn an_empty_name_an_unknown_zone_and_no_folder_are_refused() {
        let folder = Folder::new("ipc-zones-refused");
        let files = files(&folder);
        assert_eq!(created(&files, "   ", 1), Err(ZoneError::EmptyName));
        created(&files, "Ma zone", 1).unwrap_or_else(|error| panic!("{error:?}"));
        assert_eq!(renamed(&files, "ma-zone", ""), Err(ZoneError::EmptyName));
        assert_eq!(listed(&files).zones[0].name, "Ma zone", "not renamed");
        assert_eq!(renamed(&files, "gone", "X"), Err(ZoneError::NoSuchZone));
        let none = PlayersFiles::new(None);
        assert_eq!(created(&none, "X", 1), Err(ZoneError::NoFolder));
        assert_eq!(listed(&none).zones, []);
    }

    #[test]
    fn a_file_kept_is_listed_with_why_and_a_change_to_it_said_not_saved() {
        let folder = Folder::new("ipc-zones-kept");
        std::fs::create_dir_all(folder.path().join("zones")).unwrap_or_else(|error| panic!("{error}"));
        let newer = r#"{"schema": 9, "id": "newer", "name": "N", "world": 1, "maps": []}"#;
        std::fs::write(folder.path().join("zones/newer.json"), newer).unwrap_or_else(|error| panic!("{error}"));
        let files = files(&folder);
        let list = listed(&files);
        assert!(
            matches!(
                list.kept.as_slice(),
                [KeptFile {
                    why: super::super::blueprints::KeptWhy::Newer { found: 9 },
                    ..
                }]
            ),
            "{list:?}"
        );
        assert!(matches!(renamed(&files, "newer", "X"), Err(ZoneError::NotSaved { .. })));
    }
}
