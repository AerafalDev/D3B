//! The characters the player has played (`docs/bot.md`, "In the app"; ADR 0015): one file each in
//! `characters/<server>-<id>.json`, so the Characters view keeps a character whose client is closed, greyed, and
//! later its blueprint and settings. Its key is stable across servers: a character's id carries its server in its low
//! 16 bits (3.7.13.17, `artifacts/studies/character-key.md`, verified on two servers).

use std::io;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use super::atomic;
use super::store::{Kept, StorageError, Store, Why};

/// The folder of the characters' files, in the player's folder.
const CHARACTERS: &str = "characters";

/// The `schema` of a character's file this version writes and reads.
pub(crate) const SCHEMA: u32 = 1;

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

    /// Writes `character` to its file, whole, unless a file there does not read as a character of this version.
    pub(crate) fn remember(&self, character: &Played) -> Result<(), StorageError> {
        let _writing = self.lock();
        let path = self.characters().join(format!("{}.json", character.key()));
        match read(&path) {
            Ok(_) => {}
            Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {}
            Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }),
            Err(why) => return Err(StorageError::Kept { path, why }),
        }
        let mut text = serde_json::to_string_pretty(character).unwrap_or_else(|error| unreachable!("{error}"));
        text.push('\n');
        let written = std::fs::create_dir_all(self.characters()).and_then(|()| atomic::write(&path, text.as_bytes()));
        written.map_err(|source| StorageError::Write { path, source })
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

fn read(path: &std::path::Path) -> Result<Played, Why> {
    let bytes = std::fs::read(path).map_err(Why::Unreadable)?;
    let text = String::from_utf8(bytes).map_err(|_| Why::NotText)?;
    let value: serde_json::Value =
        serde_json::from_str(&text).map_err(|error| Why::NotACharacter(error.to_string()))?;
    match value.get("schema").and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(SCHEMA) => return Err(Why::NewerCharacter { found }),
        Some(found) if found == u64::from(SCHEMA) => {}
        _ => return Err(Why::NotACharacter(format!("its schema must be {SCHEMA}"))),
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
        let newer = serde_json::to_string(&Played { schema: 2, ..kalyx(1) }).unwrap_or_default();
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
                [Why::KeyIsNotId { .. }, Why::NewerCharacter { found: 2 }]
                    | [Why::NewerCharacter { found: 2 }, Why::KeyIsNotId { .. }]
            ),
            "{kept:?}"
        );
        assert!(matches!(store.remember(&kalyx(5)), Err(StorageError::Kept { .. })));
        assert_eq!(std::fs::read_to_string(&path).ok(), Some(newer));
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
