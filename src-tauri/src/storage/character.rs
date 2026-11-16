//! The characters the player has played (`docs/bot.md`, "In the app"; ADR 0015): one file each in
//! `characters/<server>-<id>.json`, so the Characters view keeps a character whose client is closed, greyed, and
//! its blueprint and settings, which are the player's: what the game shows never writes over them. Its key is stable across servers: a character's id carries its server in its low
//! 16 bits (3.7.13.17, `artifacts/studies/character-key.md`, verified on two servers).

use std::collections::BTreeMap;
use std::io;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};
use serde_json::Value;

use super::atomic;
use super::store::{Kept, StorageError, Store, Why};

/// The folder of the characters' files, in the player's folder.
const CHARACTERS: &str = "characters";

/// The `schema` of a character's file this version writes. Schema 1, before step 3's PR 11, had no blueprint and
/// no settings, and reads as a character without either; schema 2, before PR 23, had no version of the blueprint run,
/// and reads as running its first.
pub(crate) const SCHEMA: u32 = 3;

/// A character played, as its file holds it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields, rename_all = "camelCase")]
pub(crate) struct Played {
    pub(crate) schema: u32,
    /// The game's id of the character, its server in its low 16 bits.
    pub(crate) id: i64,
    pub(crate) name: String,
    pub(crate) level: i32,
    pub(crate) breed: i32,
    /// `"male"` or `"female"`, when its look said.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) sex: Option<String>,
    /// When it was last seen in game, in milliseconds since 1970.
    pub(crate) last_seen: u64,
    /// The map it was last seen on.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) map_id: Option<i64>,
    /// The id of the blueprint it runs, none until the player chooses one (the owner, 2026-11-16).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) blueprint: Option<String>,
    /// Its own values of the blueprint's variables set per character, by variable; one left out takes the default.
    #[serde(default, skip_serializing_if = "BTreeMap::is_empty")]
    pub(crate) settings: BTreeMap<String, Value>,
    /// The version of D3B's blueprint it runs that its settings were last carried to; none for one of mine.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub(crate) blueprint_version: Option<u32>,
    /// The settings whose values a new version of its blueprint could not keep, which its tab says until the player
    /// closes the notice (the owner, 2026-11-16).
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub(crate) dropped: Vec<String>,
}

impl Played {
    /// Its file's name without `.json`: `<server>-<id>`, digits and one dash.
    pub(crate) fn key(&self) -> String {
        key_of(self.id)
    }
}

/// The key of the character `id`: its server, its low 16 bits, then the whole id.
pub(crate) fn key_of(id: i64) -> String {
    format!("{}-{id}", id & 0xFFFF)
}

/// The characters of the player's folder, the most recently seen first, and the files kept.
#[derive(Debug, Default)]
pub(crate) struct PlayedCharacters {
    pub(crate) read: Vec<Played>,
    pub(crate) kept: Vec<Kept>,
}

impl Store {
    fn characters(&self) -> PathBuf {
        self.root().join(CHARACTERS)
    }

    /// Every character played, the most recently seen first; none when the folder is not made yet.
    pub(crate) fn played(&self) -> PlayedCharacters {
        let mut played = PlayedCharacters::default();
        let folder = self.characters();
        let entries = match std::fs::read_dir(&folder) {
            Ok(entries) => entries,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return played,
            Err(error) => {
                played.kept.push(Kept {
                    path: folder,
                    why: Why::Unreadable(error),
                });
                return played;
            }
        };
        for path in entries.flatten().map(|entry| entry.path()) {
            if !path
                .extension()
                .is_some_and(|extension| extension.eq_ignore_ascii_case("json"))
            {
                continue;
            }
            match read(&path) {
                Ok(character) => played.read.push(character),
                Err(why) => played.kept.push(Kept { path, why }),
            }
        }
        played
            .read
            .sort_by(|a, b| b.last_seen.cmp(&a.last_seen).then_with(|| a.name.cmp(&b.name)));
        played
    }

    /// Writes what the game shows of `character` to its file, whole, keeping the blueprint and settings the file
    /// holds, unless a file there does not read as a character of this version.
    pub(crate) fn remember(&self, character: &Played) -> Result<(), StorageError> {
        let _writing = self.lock();
        let path = self.characters().join(format!("{}.json", character.key()));
        let mut character = character.clone();
        match read(&path) {
            Ok(before) => {
                character.blueprint = before.blueprint;
                character.settings = before.settings;
                character.blueprint_version = before.blueprint_version;
                character.dropped = before.dropped;
            }
            Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {}
            Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }),
            Err(why) => return Err(StorageError::Kept { path, why }),
        }
        write(&self.characters(), &path, &mut character)
    }

    /// Changes the file of the character `key` by `change`, under the lock so that no sight of it in game comes
    /// between, and writes it whole; a key that names no character's file is refused, and nothing is written when
    /// `change` refuses.
    pub(crate) fn change_character<E: From<StorageError>>(
        &self,
        key: &str,
        change: impl FnOnce(&mut Played) -> Result<(), E>,
    ) -> Result<(), E> {
        // The key comes from the webview: only a character's key names a file, never a path elsewhere.
        let shaped = key
            .split_once('-')
            .is_some_and(|(server, id)| [server, id].iter().all(|part| part.parse::<u64>().is_ok()));
        if !shaped {
            return Err(StorageError::NoSuchCharacter { key: key.to_owned() }.into());
        }
        let _writing = self.lock();
        let path = self.characters().join(format!("{key}.json"));
        let mut character = match read(&path) {
            Ok(character) => character,
            Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {
                return Err(StorageError::NoSuchCharacter { key: key.to_owned() }.into());
            }
            Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }.into()),
            Err(why) => return Err(StorageError::Kept { path, why }.into()),
        };
        change(&mut character)?;
        Ok(write(&self.characters(), &path, &mut character)?)
    }

    /// Sends the file of the character `key` to `discard` (the system's trash in the app), so that it can be brought
    /// back; a key that names no character's file is refused.
    pub(crate) fn forget(
        &self,
        key: &str,
        discard: impl FnOnce(&std::path::Path) -> io::Result<()>,
    ) -> Result<(), StorageError> {
        let path = self.characters().join(format!("{key}.json"));
        {
            // The key is checked under the lock; the trash, which may take its time, is not waited on there.
            let _writing = self.lock();
            if !self.played().read.iter().any(|character| character.key() == key) {
                return Err(StorageError::NoSuchCharacter { key: key.to_owned() });
            }
        }
        discard(&path).map_err(|source| StorageError::Write { path, source })
    }
}

/// Writes `character` at `path` in `folder`, whole, of this version's schema.
fn write(folder: &std::path::Path, path: &std::path::Path, character: &mut Played) -> Result<(), StorageError> {
    character.schema = SCHEMA;
    let mut text = serde_json::to_string_pretty(character).unwrap_or_else(|error| unreachable!("{error}"));
    text.push('\n');
    let written = std::fs::create_dir_all(folder).and_then(|()| atomic::write(path, text.as_bytes()));
    written.map_err(|source| StorageError::Write {
        path: path.to_path_buf(),
        source,
    })
}

fn read(path: &std::path::Path) -> Result<Played, Why> {
    let bytes = std::fs::read(path).map_err(Why::Unreadable)?;
    let text = String::from_utf8(bytes).map_err(|_| Why::NotText)?;
    let value: serde_json::Value =
        serde_json::from_str(&text).map_err(|error| Why::NotACharacter(error.to_string()))?;
    match value.get("schema").and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(SCHEMA) => return Err(Why::NewerCharacter { found }),
        Some(found) if found >= 1 => {}
        _ => return Err(Why::NotACharacter(format!("its schema must be 1 to {SCHEMA}"))),
    }
    let character: Played = serde_json::from_str(&text).map_err(|error| Why::NotACharacter(error.to_string()))?;
    let name = path.file_name().and_then(|name| name.to_str());
    if name == Some(format!("{}.json", character.key()).as_str()) {
        Ok(character)
    } else {
        Err(Why::KeyIsNotId { id: character.id })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    // A character of server 294: its id's low 16 bits.
    const ID: i64 = 0x9e_447f_0126;

    fn kalyx(last_seen: u64) -> Played {
        let sex = Some("male".to_owned());
        Played {
            schema: SCHEMA,
            id: ID,
            name: "Kalyx".into(),
            level: 54,
            breed: 8,
            sex,
            last_seen,
            map_id: Some(191_104_002),
            blueprint: None,
            settings: BTreeMap::new(),
            blueprint_version: None,
            dropped: Vec::new(),
        }
    }

    #[test]
    fn a_characters_key_is_its_server_then_its_id() {
        assert_eq!(key_of(ID), format!("294-{ID}"));
        assert_eq!(kalyx(0).key(), format!("294-{ID}"));
    }

    #[test]
    fn a_character_remembered_is_read_back_the_most_recently_seen_first() {
        let folder = Folder::new("characters-remembered");
        let store = Store::new(folder.path().to_path_buf());
        assert_eq!(store.played().read, [], "no folder yet");
        store.remember(&kalyx(10)).unwrap_or_else(|error| panic!("{error}"));
        let other = Played {
            id: 0x1_0000_0161,
            name: "Sylvane".into(),
            last_seen: 20,
            ..kalyx(0)
        };
        store.remember(&other).unwrap_or_else(|error| panic!("{error}"));
        let names: Vec<_> = store.played().read.into_iter().map(|it| it.name).collect();
        assert_eq!(names, ["Sylvane", "Kalyx"]);
        store.remember(&kalyx(30)).unwrap_or_else(|error| panic!("{error}"));
        let played = store.played();
        assert_eq!(
            (played.read.len(), played.read[0].name.as_str()),
            (2, "Kalyx"),
            "remembered again, not twice"
        );
        assert!(folder.path().join(format!("characters/294-{ID}.json")).exists());
    }

    #[test]
    fn a_file_that_does_not_read_is_kept_and_never_written_over() {
        let folder = Folder::new("characters-kept");
        let store = Store::new(folder.path().to_path_buf());
        std::fs::create_dir_all(folder.path().join("characters")).unwrap_or_else(|error| panic!("{error}"));
        let path = folder.path().join(format!("characters/294-{ID}.json"));
        let newer = serde_json::to_string(&Played {
            schema: SCHEMA + 1,
            ..kalyx(1)
        })
        .unwrap_or_default();
        std::fs::write(&path, &newer).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(
            folder.path().join("characters/1-2.json"),
            serde_json::to_string(&kalyx(1)).unwrap_or_default(),
        )
        .unwrap_or_else(|error| panic!("{error}"));
        let kept: Vec<_> = store.played().kept.into_iter().map(|kept| kept.why).collect();
        assert!(
            matches!(
                kept.as_slice(),
                [Why::KeyIsNotId { .. }, Why::NewerCharacter { found: 4 }]
                    | [Why::NewerCharacter { found: 4 }, Why::KeyIsNotId { .. }]
            ),
            "{kept:?}"
        );
        assert!(matches!(store.remember(&kalyx(5)), Err(StorageError::Kept { .. })));
        assert_eq!(std::fs::read_to_string(&path).ok(), Some(newer));
    }

    #[test]
    fn a_file_of_schema_1_reads_without_a_blueprint_and_is_written_back_of_this_schema() {
        let folder = Folder::new("characters-schema-1");
        let store = Store::new(folder.path().to_path_buf());
        std::fs::create_dir_all(folder.path().join("characters")).unwrap_or_else(|error| panic!("{error}"));
        let path = folder.path().join(format!("characters/294-{ID}.json"));
        let former = format!(r#"{{"schema": 1, "id": {ID}, "name": "Kalyx", "level": 54, "breed": 8, "lastSeen": 7}}"#);
        std::fs::write(&path, former).unwrap_or_else(|error| panic!("{error}"));
        let read = store.played().read;
        assert_eq!(
            read.first().map(|it| (it.blueprint.clone(), it.settings.len())),
            Some((None, 0))
        );
        store.remember(&kalyx(8)).unwrap_or_else(|error| panic!("{error}"));
        let written = std::fs::read_to_string(&path).unwrap_or_default();
        assert!(written.contains(r#""schema": 3"#), "{written}");
    }

    #[test]
    fn a_file_of_no_schema_or_schema_0_is_no_character() {
        let folder = Folder::new("characters-schema-0");
        let store = Store::new(folder.path().to_path_buf());
        std::fs::create_dir_all(folder.path().join("characters")).unwrap_or_else(|error| panic!("{error}"));
        for (name, schema) in [("1-1", r#""schema": 0, "#), ("1-2", "")] {
            let text = format!(r#"{{{schema}"id": 1, "name": "Kalyx", "level": 1, "breed": 8, "lastSeen": 7}}"#);
            std::fs::write(folder.path().join(format!("characters/{name}.json")), text)
                .unwrap_or_else(|error| panic!("{error}"));
        }
        let played = store.played();
        assert_eq!(played.read, []);
        assert!(
            played.kept.iter().all(|kept| matches!(kept.why, Why::NotACharacter(_))) && played.kept.len() == 2,
            "{:?}",
            played.kept
        );
    }

    #[test]
    fn the_blueprint_and_settings_changed_are_kept_when_the_game_shows_the_character_again() {
        let folder = Folder::new("characters-change");
        let store = Store::new(folder.path().to_path_buf());
        store.remember(&kalyx(1)).unwrap_or_else(|error| panic!("{error}"));
        let changed = store.change_character(&kalyx(1).key(), |character| {
            character.blueprint = Some("automatic-harvest".into());
            character.settings.insert("pods".into(), serde_json::json!(80));
            character.blueprint_version = Some(2);
            character.dropped = vec!["keep".into()];
            Ok::<(), StorageError>(())
        });
        assert!(changed.is_ok(), "{changed:?}");
        store.remember(&kalyx(2)).unwrap_or_else(|error| panic!("{error}"));
        let read = store.played().read;
        let character = read.first().unwrap_or_else(|| panic!("no character"));
        assert_eq!(
            (character.last_seen, character.blueprint.as_deref()),
            (2, Some("automatic-harvest"))
        );
        assert_eq!(character.settings.get("pods"), Some(&serde_json::json!(80)));
        assert_eq!(
            (character.blueprint_version, character.dropped.as_slice()),
            (Some(2), ["keep".to_owned()].as_slice()),
            "the version run and the notice are the player's too"
        );
    }

    #[test]
    fn a_change_refused_or_of_an_unknown_character_writes_nothing() {
        let folder = Folder::new("characters-change-refused");
        let store = Store::new(folder.path().to_path_buf());
        // A character's file out of its folder, which a key made a path would read and find not named by its id.
        std::fs::create_dir_all(folder.path().join("blueprints")).unwrap_or_else(|error| panic!("{error}"));
        let elsewhere = serde_json::to_string(&kalyx(1)).unwrap_or_default();
        std::fs::write(folder.path().join("blueprints/x.json"), elsewhere).unwrap_or_else(|error| panic!("{error}"));
        for key in ["294-1", "../blueprints/x", "294-1/../blueprints/x"] {
            let unknown = store.change_character(key, |_| Ok::<(), StorageError>(()));
            assert!(
                matches!(unknown, Err(StorageError::NoSuchCharacter { .. })),
                "{key}: {unknown:?}"
            );
        }
        store.remember(&kalyx(1)).unwrap_or_else(|error| panic!("{error}"));
        let refused = store.change_character(&kalyx(1).key(), |character| {
            character.blueprint = Some("hunt".into());
            Err(StorageError::NoSuchCharacter { key: "refused".into() })
        });
        assert!(refused.is_err());
        assert_eq!(store.played().read.first().map(|it| it.blueprint.clone()), Some(None));
    }

    #[test]
    fn a_character_forgotten_goes_to_the_trash_and_an_unknown_one_is_refused() {
        let folder = Folder::new("characters-forget");
        let store = Store::new(folder.path().to_path_buf());
        store.remember(&kalyx(1)).unwrap_or_else(|error| panic!("{error}"));
        let mut trashed = Vec::new();
        let forgotten = store.forget(&kalyx(1).key(), |path| {
            trashed.push(path.to_path_buf());
            std::fs::remove_file(path)
        });
        assert!(forgotten.is_ok(), "{forgotten:?}");
        assert_eq!(trashed, [folder.path().join(format!("characters/294-{ID}.json"))]);
        assert_eq!(store.played().read, []);
        let unknown = store.forget("../blueprints/x", |_| Ok(()));
        assert!(matches!(unknown, Err(StorageError::NoSuchCharacter { .. })));
    }
}
