//! The player's zones (`docs/bot.md`, "The concepts"; ADR 0015): one file each in `zones/<id>.json`, painted on the
//! World tab map by map or a whole sub-area at a time. Its maps are coordinates on one world, as the map paints them,
//! every floor of those coordinates counting: what the World tab shows, read by hand, and resolved into the game's maps
//! by the runtime (the owner, 2026-11-16).

use std::collections::{BTreeSet, HashSet};
use std::fmt::Write as _;
use std::io;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

use super::atomic;
use super::store::{Kept, StorageError, Store, Why};
use crate::bot::{self, Id};

/// The folder of the zones' files, in the player's folder.
const ZONES: &str = "zones";

/// The `schema` of a zone's file this version writes.
pub(crate) const SCHEMA: u32 = 1;

/// A zone of mine, as its file holds it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(crate) struct Zone {
    pub(crate) schema: u32,
    pub(crate) id: Id,
    pub(crate) name: String,
    /// The world its maps are on, the game's world map id: the Twelve's is 1.
    pub(crate) world: i32,
    /// Its maps' coordinates on that world, each once, in order.
    pub(crate) maps: BTreeSet<[i16; 2]>,
}

/// The zones of the player's folder, in the order of their file names, and the files kept as they are.
#[derive(Debug, Default)]
pub(crate) struct Zones {
    pub(crate) read: Vec<Zone>,
    pub(crate) kept: Vec<Kept>,
}

impl Store {
    fn zones_folder(&self) -> PathBuf {
        self.root().join(ZONES)
    }

    /// Every zone of the player's folder; none when the folder is not made yet.
    pub(crate) fn zones(&self) -> Zones {
        let mut zones = Zones::default();
        let folder = self.zones_folder();
        let entries = match std::fs::read_dir(&folder) {
            Ok(entries) => entries,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return zones,
            Err(error) => {
                zones.kept.push(Kept {
                    path: folder,
                    why: Why::Unreadable(error),
                });
                return zones;
            }
        };
        // Only JSON files, in any case: a temporary file a crash left is not ours.
        let mut paths: Vec<PathBuf> = entries
            .flatten()
            .map(|entry| entry.path())
            .filter(|path| {
                path.extension()
                    .is_some_and(|extension| extension.eq_ignore_ascii_case("json"))
            })
            .collect();
        paths.sort();
        for path in paths {
            match read(&path) {
                Ok(zone) => zones.read.push(zone),
                Err(why) => zones.kept.push(Kept { path, why }),
            }
        }
        zones
    }

    /// A new zone named `name` on `world`, without maps, under an id made from its name that no file of the folder
    /// holds, in any case; the id is chosen and the file written under one lock.
    pub(crate) fn create_zone(&self, name: &str, world: i32) -> Result<Zone, StorageError> {
        let _writing = self.lock();
        let folder = self.zones_folder();
        let mut taken = HashSet::new();
        match std::fs::read_dir(&folder) {
            Ok(entries) => {
                for path in entries.flatten().map(|entry| entry.path()) {
                    if let Some(stem) = path.file_stem().and_then(|stem| stem.to_str()) {
                        taken.insert(Id(stem.to_lowercase()));
                    }
                }
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => {}
            // A folder that does not list could hide the file a new id would write over.
            Err(source) => return Err(StorageError::Unreadable { path: folder, source }),
        }
        let zone = Zone {
            schema: SCHEMA,
            id: bot::id_for(name, &taken),
            name: name.to_owned(),
            world,
            maps: BTreeSet::new(),
        };
        let path = folder.join(format!("{}.json", zone.id.0));
        write(&folder, &path, &zone)?;
        Ok(zone)
    }

    /// Changes the zone `id` by `change` and writes it whole, under the lock; the zone written. An id that names no
    /// zone's file is refused, and nothing is written when `change` refuses.
    pub(crate) fn change_zone<E: From<StorageError>>(
        &self,
        id: &str,
        change: impl FnOnce(&mut Zone) -> Result<(), E>,
    ) -> Result<Zone, E> {
        let path = self.zone_path(id)?;
        let _writing = self.lock();
        let mut zone = match read(&path) {
            Ok(zone) => zone,
            Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {
                return Err(StorageError::NoSuchZone { id: id.to_owned() }.into());
            }
            Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }.into()),
            Err(why) => return Err(StorageError::Kept { path, why }.into()),
        };
        change(&mut zone)?;
        write(&self.zones_folder(), &path, &zone)?;
        Ok(zone)
    }

    /// Sends the file of the zone `id` to `discard` (the system's trash in the app), so that it can be brought back; an
    /// id that names no zone that reads is refused, and a file kept is never discarded.
    pub(crate) fn delete_zone(
        &self,
        id: &str,
        discard: impl FnOnce(&Path) -> io::Result<()>,
    ) -> Result<(), StorageError> {
        let path = self.zone_path(id)?;
        {
            // Checked under the lock; the trash, which may take its time, is not waited on there.
            let _writing = self.lock();
            match read(&path) {
                Ok(_) => {}
                Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {
                    return Err(StorageError::NoSuchZone { id: id.to_owned() });
                }
                Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }),
                Err(why) => return Err(StorageError::Kept { path, why }),
            }
        }
        discard(&path).map_err(|source| StorageError::Write { path, source })
    }

    /// The file of the zone `id`; an id from the webview names a file only when well formed, never a path elsewhere.
    fn zone_path(&self, id: &str) -> Result<PathBuf, StorageError> {
        if Id(id.to_owned()).is_well_formed() {
            Ok(self.zones_folder().join(format!("{id}.json")))
        } else {
            Err(StorageError::NoSuchZone { id: id.to_owned() })
        }
    }
}

/// `zone` as its file writes it: one map a line, so that a map painted changes one line of a diff.
fn text(zone: &Zone) -> String {
    let quoted = |text: &str| serde_json::to_string(text).unwrap_or_else(|error| unreachable!("{error}"));
    let mut text = format!(
        "{{\n  \"schema\": {},\n  \"id\": {},\n  \"name\": {},\n  \"world\": {},\n  \"maps\": [",
        SCHEMA,
        quoted(&zone.id.0),
        quoted(&zone.name),
        zone.world
    );
    for (index, [x, y]) in zone.maps.iter().enumerate() {
        let comma = if index + 1 < zone.maps.len() { "," } else { "" };
        let _ = write!(text, "\n    [{x}, {y}]{comma}");
    }
    text.push_str(if zone.maps.is_empty() { "]\n}\n" } else { "\n  ]\n}\n" });
    text
}

/// Writes `zone` at `path` in `folder`, whole.
fn write(folder: &Path, path: &Path, zone: &Zone) -> Result<(), StorageError> {
    let written = std::fs::create_dir_all(folder).and_then(|()| atomic::write(path, text(zone).as_bytes()));
    written.map_err(|source| StorageError::Write {
        path: path.to_path_buf(),
        source,
    })
}

fn read(path: &Path) -> Result<Zone, Why> {
    let bytes = std::fs::read(path).map_err(Why::Unreadable)?;
    let text = String::from_utf8(bytes).map_err(|_| Why::NotText)?;
    let value: serde_json::Value = serde_json::from_str(&text).map_err(|error| Why::NotAZone(error.to_string()))?;
    match value.get("schema").and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(SCHEMA) => return Err(Why::NewerZone { found }),
        Some(found) if found >= 1 => {}
        _ => return Err(Why::NotAZone(format!("its schema must be 1 to {SCHEMA}"))),
    }
    let zone: Zone = serde_json::from_str(&text).map_err(|error| Why::NotAZone(error.to_string()))?;
    if !zone.id.is_well_formed() {
        return Err(Why::NotAZone(format!("{} is no id a file can be named by", zone.id.0)));
    }
    let name = path.file_name().and_then(|name| name.to_str());
    if name == Some(format!("{}.json", zone.id.0).as_str()) {
        Ok(zone)
    } else {
        Err(Why::NameIsNotId { id: zone.id })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn put(folder: &Folder, name: &str, text: &str) -> PathBuf {
        std::fs::create_dir_all(folder.path().join(ZONES)).unwrap_or_else(|error| panic!("{error}"));
        let path = folder.path().join(ZONES).join(name);
        std::fs::write(&path, text).unwrap_or_else(|error| panic!("{error}"));
        path
    }

    fn paint(store: &Store, id: &str, maps: &[[i16; 2]]) -> Zone {
        store
            .change_zone(id, |zone| {
                zone.maps.extend(maps);
                Ok::<_, StorageError>(())
            })
            .unwrap_or_else(|error| panic!("{error}"))
    }

    #[test]
    fn a_zone_created_is_empty_on_its_world_under_an_id_from_its_name_free_in_any_case() {
        let folder = Folder::new("zones-create");
        let store = Store::new(folder.path().to_path_buf());
        assert!(store.zones().read.is_empty(), "no folder yet");
        put(&folder, "Nouvelle-Zone.JSON", "{");
        let zone = store
            .create_zone("Nouvelle zone", 1)
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            (zone.id.0.as_str(), zone.name.as_str(), zone.world, zone.maps.len()),
            ("nouvelle-zone-2", "Nouvelle zone", 1, 0)
        );
        let again = store
            .create_zone("Nouvelle zone", 2)
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(again.id.0, "nouvelle-zone-3");
        let read: Vec<_> = store.zones().read.into_iter().map(|zone| zone.id.0).collect();
        assert_eq!(read, ["nouvelle-zone-2", "nouvelle-zone-3"]);
    }

    #[test]
    fn a_zone_painted_is_written_a_map_a_line_and_read_back() {
        let folder = Folder::new("zones-paint");
        let store = Store::new(folder.path().to_path_buf());
        let zone = store
            .create_zone("Ma forêt", 1)
            .unwrap_or_else(|error| panic!("{error}"));
        let empty = std::fs::read_to_string(folder.path().join("zones/ma-foret.json")).unwrap_or_default();
        assert_eq!(
            empty,
            "{\n  \"schema\": 1,\n  \"id\": \"ma-foret\",\n  \"name\": \"Ma forêt\",\n  \"world\": 1,\n  \"maps\": []\n}\n"
        );
        let painted = paint(&store, &zone.id.0, &[[5, -18], [4, -18], [5, -18]]);
        assert_eq!(painted.maps.iter().copied().collect::<Vec<_>>(), [[4, -18], [5, -18]]);
        let text = std::fs::read_to_string(folder.path().join("zones/ma-foret.json")).unwrap_or_default();
        assert!(
            text.ends_with("\"maps\": [\n    [4, -18],\n    [5, -18]\n  ]\n}\n"),
            "{text}"
        );
        assert_eq!(store.zones().read, [painted]);
    }

    #[test]
    fn files_that_do_not_read_are_kept_and_never_written_over_or_discarded() {
        let folder = Folder::new("zones-kept");
        let store = Store::new(folder.path().to_path_buf());
        let newer = r#"{"schema": 2, "id": "newer", "name": "N", "world": 1, "maps": []}"#;
        let newer_path = put(&folder, "newer.json", newer);
        put(&folder, "broken.json", "{\"schema\": 1,");
        put(
            &folder,
            "copy.json",
            r#"{"schema": 1, "id": "other", "name": "O", "world": 1, "maps": []}"#,
        );
        put(
            &folder,
            "odd.json",
            r#"{"schema": 1, "id": "odd", "name": "O", "world": 1, "maps": [], "x": 1}"#,
        );
        put(
            &folder,
            "none.json",
            r#"{"id": "none", "name": "O", "world": 1, "maps": []}"#,
        );
        put(&folder, "ok.json.d3b-tmp", "half");
        let kept: Vec<_> = store.zones().kept.into_iter().map(|kept| kept.why).collect();
        assert!(
            matches!(
                kept.as_slice(),
                [
                    Why::NotAZone(_),
                    Why::NameIsNotId { .. },
                    Why::NewerZone { found: 2 },
                    Why::NotAZone(_),
                    Why::NotAZone(_),
                ]
            ),
            "{kept:?}"
        );
        let changed = store.change_zone("newer", |zone| {
            zone.name = "Lost".into();
            Ok::<_, StorageError>(())
        });
        assert!(matches!(changed, Err(StorageError::Kept { .. })), "{changed:?}");
        let deleted = store.delete_zone("newer", |_| panic!("a kept file is never discarded"));
        assert!(matches!(deleted, Err(StorageError::Kept { .. })), "{deleted:?}");
        assert_eq!(std::fs::read_to_string(&newer_path).ok().as_deref(), Some(newer));
    }

    #[test]
    fn a_change_refused_or_of_an_unknown_zone_writes_nothing() {
        let folder = Folder::new("zones-refused");
        let store = Store::new(folder.path().to_path_buf());
        let zone = store
            .create_zone("Ma zone", 1)
            .unwrap_or_else(|error| panic!("{error}"));
        let refused = store.change_zone(&zone.id.0, |zone| {
            zone.name = "Lost".into();
            Err(StorageError::NoSuchZone { id: "refused".into() })
        });
        assert!(refused.is_err());
        assert_eq!(store.zones().read[0].name, "Ma zone", "nothing written");
        for id in ["gone", "../blueprints/x", "Ma-Zone"] {
            let unknown = store.change_zone(id, |_| Ok::<_, StorageError>(()));
            assert!(
                matches!(unknown, Err(StorageError::NoSuchZone { .. })),
                "{id}: {unknown:?}"
            );
        }
    }

    #[test]
    fn a_zone_deleted_goes_to_the_trash_and_an_unknown_one_is_refused() {
        let folder = Folder::new("zones-delete");
        let store = Store::new(folder.path().to_path_buf());
        let zone = store
            .create_zone("Ma zone", 1)
            .unwrap_or_else(|error| panic!("{error}"));
        let mut trashed = Vec::new();
        let deleted = store.delete_zone(&zone.id.0, |path| {
            trashed.push(path.to_path_buf());
            std::fs::remove_file(path)
        });
        assert!(deleted.is_ok(), "{deleted:?}");
        assert_eq!(trashed, [folder.path().join("zones/ma-zone.json")]);
        assert_eq!(store.zones().read, []);
        for id in ["ma-zone", "../characters/x"] {
            let unknown = store.delete_zone(id, |_| Ok(()));
            assert!(
                matches!(unknown, Err(StorageError::NoSuchZone { .. })),
                "{id}: {unknown:?}"
            );
        }
    }
}
