//! The characters played (`docs/bot.md`, "In the app"; the owner's choices of 2026-11-14): each remembered in the
//! player's folder as it is seen in game, listed with when it was last seen so the Characters view keeps it greyed
//! once its client is closed, and forgotten into the system's trash.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use super::LoadedData;
use super::blueprints::{KeptFile, PlayersFiles};
use super::character::{CharacterSex, Names, Place, breed, place};
use crate::game::{Sex, View, Who};
use crate::storage::{CHARACTER_SCHEMA, Played, StorageError};

/// How long the last sight of a character on another map waits before it is written again: the owner's "at most once a
/// minute" of 2026-11-14, so the time and the map read right to the minute even when D3B is quit while playing.
const REWRITE: u64 = 60_000;

/// What to remember of a connection's character as its views come: written when it is first known, each time its name,
/// level, breed or sex change, when it is on another map a minute after its last write, when another character
/// takes the connection, and when the connection closes, with when and where it was last seen.
#[derive(Debug, Default)]
pub(crate) struct Remembering {
    last: Option<Played>,
    written: Option<Played>,
}

impl Remembering {
    /// The characters to write after `view` came at `now` (milliseconds since 1970); `None` for `view` is the
    /// connection's close. A character whose id is not positive is none: the selection named no character.
    pub(crate) fn seen(&mut self, view: Option<&View>, now: u64) -> Vec<Played> {
        let mut written = Vec::new();
        let Some(view) = view else {
            if let Some(mut last) = self.last.take() {
                last.last_seen = now;
                written.push(last);
            }
            self.written = None;
            return written;
        };
        let Some(who) = view.who.as_ref().filter(|who| who.id > 0) else {
            return written;
        };
        let character = Played {
            schema: CHARACTER_SCHEMA,
            id: who.id,
            name: who.name.clone(),
            level: who.level,
            breed: who.breed,
            sex: who
                .sex
                .map(|sex| if sex == Sex::Female { "female" } else { "male" }.to_owned()),
            last_seen: now,
            map_id: view.map_id,
        };
        if let Some(mut other) = self.last.take_if(|last| last.id != character.id) {
            other.last_seen = now;
            written.push(other);
            self.written = None;
        }
        let who_it_is = |played: &Played| {
            (
                played.id,
                played.name.clone(),
                played.level,
                played.breed,
                played.sex.clone(),
            )
        };
        let changed = self
            .written
            .as_ref()
            .is_none_or(|before| who_it_is(before) != who_it_is(&character));
        let moved = self
            .written
            .as_ref()
            .is_some_and(|before| before.map_id != character.map_id && now >= before.last_seen.saturating_add(REWRITE));
        if changed || moved {
            self.written = Some(character.clone());
            written.push(character.clone());
        }
        self.last = Some(character);
        written
    }
}

/// The one thread that writes the characters played, in the order they come, so the protocol's tasks never wait on
/// the disk or on the store's lock (`protocol::Observer` must never block). It ends with the app.
pub struct PlayedWriter(std::sync::mpsc::Sender<Played>);

/// Starts the writer of the characters played.
pub fn start_writer(handle: &tauri::AppHandle) -> PlayedWriter {
    let (sender, received) = std::sync::mpsc::channel::<Played>();
    let handle = handle.clone();
    std::thread::spawn(move || {
        for character in received {
            write(&handle, &character);
        }
    });
    PlayedWriter(sender)
}

/// The characters played changed: one was remembered or forgotten.
#[derive(Debug, Clone, Serialize, Deserialize, Type, tauri_specta::Event)]
pub struct PlayedChanged;

/// Hands `character` to the writer; returns at once.
pub(crate) fn remember(handle: &tauri::AppHandle, character: Played) {
    if let Some(writer) = handle.try_state::<PlayedWriter>() {
        let _ = writer.0.send(character);
    }
}

/// Writes `character` among the player's files and says so to the webview; a failure is only logged, the game going on
/// as before.
fn write(handle: &tauri::AppHandle, character: &Played) {
    let Some(files) = handle.try_state::<PlayersFiles>() else {
        return;
    };
    let Some(store) = files.store() else { return };
    match store.remember(character) {
        Ok(()) => {
            let _ = PlayedChanged.emit(handle);
        }
        Err(error) => tauri_plugin_log::log::warn!("the character {} is not remembered: {error}", character.name),
    }
}

/// A character played, as the Characters view lists it.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct PlayedCharacter {
    /// Its file's name: `<server>-<id>`, also its connection's view's `key`.
    pub key: String,
    pub name: String,
    pub level: i32,
    pub breed_name: Option<Names>,
    pub head: Option<String>,
    pub sex: Option<CharacterSex>,
    /// When it was last seen in game, in milliseconds since 1970.
    pub last_seen: f64,
    pub place: Option<Place>,
}

/// Why a character was not forgotten.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ForgetError {
    NoSuchCharacter,
    NoFolder,
    /// The system could not move its file to the trash: its own words.
    NotForgotten {
        reason: String,
    },
}

/// The characters played and the files kept as they are.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
pub struct PlayedList {
    pub characters: Vec<PlayedCharacter>,
    /// The characters' files that do not read, kept untouched (ADR 0015); the view reports them with the Library's
    /// own, decided with the owner.
    pub kept: Vec<KeptFile>,
}

fn listed(files: &PlayersFiles, data: Option<&LoadedData>) -> PlayedList {
    let Some(store) = files.store() else {
        return PlayedList {
            characters: Vec::new(),
            kept: Vec::new(),
        };
    };
    let named = data.and_then(LoadedData::named);
    let played = store.played();
    let kept = played.kept.iter().map(|file| {
        tauri_plugin_log::log::warn!("{} is kept as it is: {}", file.path.display(), file.why);
        KeptFile {
            path: file.path.display().to_string(),
            why: (&file.why).into(),
        }
    });
    let kept = kept.collect();
    let characters = played.read.into_iter().map(|character| {
        let sex = match character.sex.as_deref() {
            Some("female") => Some(Sex::Female),
            Some("male") => Some(Sex::Male),
            _ => None,
        };
        let who = Who {
            id: character.id,
            name: character.name.clone(),
            level: character.level,
            breed: character.breed,
            sex,
        };
        let (breed_name, head) = breed(Some(&who), named);
        PlayedCharacter {
            key: character.key(),
            name: character.name,
            level: character.level,
            breed_name,
            head,
            sex: sex.map(CharacterSex::from),
            #[expect(clippy::cast_precision_loss, reason = "a date in milliseconds is far below 2^53")]
            last_seen: character.last_seen as f64,
            place: character
                .map_id
                .map(|map_id| place(map_id, named.map(|named| named.world))),
        }
    });
    PlayedList {
        characters: characters.collect(),
        kept,
    }
}

/// The characters played, the most recently seen first, and their files kept.
#[tauri::command]
#[specta::specta]
pub async fn played_characters(app: tauri::AppHandle) -> Result<PlayedList, String> {
    let read = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        let data = app.try_state::<LoadedData>();
        listed(&files, data.as_deref())
    });
    read.await.map_err(|error| error.to_string())
}

/// Sends `path` to the system's trash, on a thread of its own so the trash's COM set-up on Windows meets no thread
/// that set it up otherwise. On macOS through the file manager, without asking to control the Finder, at the cost of
/// "Put Back" on some versions (the owner, 2026-11-14).
fn to_trash(path: &std::path::Path) -> std::io::Result<()> {
    let path = path.to_path_buf();
    let deleted = std::thread::spawn(move || {
        #[cfg(target_os = "macos")]
        {
            use trash::macos::{DeleteMethod, TrashContextExtMacos};
            let mut context = trash::TrashContext::default();
            context.set_delete_method(DeleteMethod::NsFileManager);
            context.delete(&path)
        }
        #[cfg(not(target_os = "macos"))]
        trash::delete(&path)
    });
    match deleted.join() {
        Ok(deleted) => deleted.map_err(std::io::Error::other),
        Err(_) => Err(std::io::Error::other("the trash failed")),
    }
}

/// Forget: the file of the character `key` goes to the system's trash.
#[tauri::command]
#[specta::specta]
pub async fn forget_character(app: tauri::AppHandle, key: String) -> Result<(), ForgetError> {
    let handle = app.clone();
    let forgotten = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        let store = files.store().ok_or(ForgetError::NoFolder)?;
        match store.forget(&key, to_trash) {
            Ok(()) => Ok(()),
            Err(StorageError::NoSuchCharacter { .. }) => Err(ForgetError::NoSuchCharacter),
            Err(error) => Err(ForgetError::NotForgotten {
                reason: error.to_string(),
            }),
        }
    });
    let forgotten = forgotten.await.unwrap_or_else(|error| {
        Err(ForgetError::NotForgotten {
            reason: error.to_string(),
        })
    });
    if forgotten.is_ok() {
        let _ = PlayedChanged.emit(&handle);
    }
    forgotten
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::Who;
    use crate::storage::Store;
    use crate::storage::tests::Folder;

    const KALYX: i64 = 0x9e_447f_0126;
    const MINUTE: u64 = 60_000;

    fn view(id: i64, name: &str, level: i32, map_id: Option<i64>) -> View {
        let who = Who {
            id,
            name: name.into(),
            level,
            breed: 8,
            sex: Some(Sex::Male),
        };
        View {
            who: Some(who),
            map_id,
            ..View::default()
        }
    }

    fn names(written: &[Played]) -> Vec<(&str, i32, u64, Option<i64>)> {
        written
            .iter()
            .map(|it| (it.name.as_str(), it.level, it.last_seen, it.map_id))
            .collect()
    }

    #[test]
    fn a_character_is_written_when_first_known_and_when_who_it_is_changes() {
        let mut remembering = Remembering::default();
        assert_eq!(remembering.seen(Some(&View::default()), 1), [], "not known yet");
        assert_eq!(
            names(&remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(1))), 2)),
            [("Kalyx", 54, 2, Some(1))]
        );
        assert_eq!(
            remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(1))), 3),
            [],
            "nothing new"
        );
        assert_eq!(
            names(&remembering.seen(Some(&view(KALYX, "Kalyx", 55, Some(1))), 4)),
            [("Kalyx", 55, 4, Some(1))]
        );
        assert_eq!(
            names(&remembering.seen(Some(&view(KALYX, "Kalix", 55, Some(1))), 5)),
            [("Kalix", 55, 5, Some(1))]
        );
        let mut female = view(KALYX, "Kalix", 55, Some(1));
        if let Some(who) = female.who.as_mut() {
            who.sex = Some(Sex::Female);
        }
        assert_eq!(remembering.seen(Some(&female), 6).len(), 1, "its sex too");
    }

    #[test]
    fn another_map_is_written_a_minute_after_the_last_write() {
        let mut remembering = Remembering::default();
        remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(1))), 0);
        assert_eq!(
            remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(2))), MINUTE - 1),
            [],
            "within the minute"
        );
        let moved = remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(3))), MINUTE);
        assert_eq!(names(&moved), [("Kalyx", 54, MINUTE, Some(3))]);
        assert_eq!(
            remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(3))), 3 * MINUTE),
            [],
            "the same map"
        );
    }

    #[test]
    fn a_closed_connection_or_another_character_writes_when_and_where_the_first_was_last_seen() {
        let mut remembering = Remembering::default();
        remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(1))), 2);
        remembering.seen(Some(&view(KALYX, "Kalyx", 54, Some(7))), 3);
        let switched = remembering.seen(Some(&view(KALYX + 0x1_0000, "Orrin", 23, Some(9))), 4);
        assert_eq!(names(&switched), [("Kalyx", 54, 4, Some(7)), ("Orrin", 23, 4, Some(9))]);
        assert_eq!(names(&remembering.seen(None, 9)), [("Orrin", 23, 9, Some(9))]);
        assert_eq!(remembering.seen(None, 10), [], "closed once");
    }

    #[test]
    fn a_selection_that_named_no_character_writes_nothing() {
        let mut remembering = Remembering::default();
        assert_eq!(remembering.seen(Some(&view(0, "", 0, None)), 1), []);
        assert_eq!(remembering.seen(Some(&view(-5, "Odd", 1, None)), 1), []);
        assert_eq!(remembering.seen(None, 2), [], "and nothing at close");
    }

    #[test]
    fn the_list_names_each_character_played_and_reports_the_files_kept() {
        let folder = Folder::new("ipc-played");
        let store = Store::new(folder.path().to_path_buf());
        let mut remembering = Remembering::default();
        let mut female = view(KALYX, "Kalyx", 54, Some(191_104_002));
        if let Some(who) = female.who.as_mut() {
            who.sex = Some(Sex::Female);
        }
        for character in remembering.seen(Some(&female), 1_000) {
            store.remember(&character).unwrap_or_else(|error| panic!("{error}"));
        }
        std::fs::write(folder.path().join("characters/1-2.json"), "{").unwrap_or_else(|error| panic!("{error}"));
        let list = listed(
            &PlayersFiles::new(Some(store)),
            Some(&LoadedData(Err("no data".into()), None)),
        );
        let character = list.characters.first().unwrap_or_else(|| panic!("{list:?}"));
        assert_eq!(character.key, format!("294-{KALYX}"));
        assert_eq!(
            (character.sex, character.last_seen, character.breed_name.clone()),
            (Some(CharacterSex::Female), 1_000.0, None)
        );
        assert_eq!(character.place.as_ref().map(|place| place.map_id), Some(191_104_002.0));
        assert!(
            matches!(
                list.kept.as_slice(),
                [KeptFile {
                    why: super::super::blueprints::KeptWhy::NotACharacter { .. },
                    ..
                }]
            ),
            "{:?}",
            list.kept
        );
        assert_eq!(listed(&PlayersFiles::new(None), None).characters, []);
    }

    #[test]
    fn a_sex_written_by_hand_otherwise_is_no_sex() {
        let folder = Folder::new("ipc-played-sex");
        let store = Store::new(folder.path().to_path_buf());
        let character = Played {
            schema: CHARACTER_SCHEMA,
            id: KALYX,
            name: "Kalyx".into(),
            level: 1,
            breed: 8,
            sex: Some("Female".into()),
            last_seen: 1,
            map_id: None,
        };
        store.remember(&character).unwrap_or_else(|error| panic!("{error}"));
        let list = listed(&PlayersFiles::new(Some(store)), None);
        assert_eq!(list.characters.first().map(|it| it.sex), Some(None));
    }
}
