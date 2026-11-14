//! The characters played (`docs/bot.md`, "In the app"; the owner's choices of 2026-11-14): each remembered in the
//! player's folder as it is seen in game, listed with when it was last seen so the Characters view keeps it greyed
//! once its client is closed, and forgotten into the system's trash.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use super::LoadedData;
use super::blueprints::PlayersFiles;
use super::character::{CharacterSex, Names, Place, breed, place};
use crate::game::{Sex, View, Who};
use crate::storage::{CHARACTER_SCHEMA, Played, StorageError};

/// What to remember of a connection's character as its views come: written when it is first known and each time its
/// name, level, breed or sex change, and once more when its connection closes, with when and where it was last seen.
#[derive(Debug, Default)]
pub(crate) struct Remembering {
    last: Option<Played>,
    written: Option<Played>,
}

impl Remembering {
    /// The character to write after `view` came at `now` (milliseconds since 1970), if any; `None` for `view` is the
    /// connection's close.
    pub(crate) fn seen(&mut self, view: Option<&View>, now: u64) -> Option<Played> {
        let Some(view) = view else {
            let mut last = self.last.take()?;
            last.last_seen = now;
            self.written = None;
            return Some(last);
        };
        let who = view.who.as_ref()?;
        let sex = who
            .sex
            .map(|sex| if sex == Sex::Female { "female" } else { "male" }.to_owned());
        let character = Played {
            schema: CHARACTER_SCHEMA,
            id: who.id,
            name: who.name.clone(),
            level: who.level,
            breed: who.breed,
            sex,
            last_seen: now,
            map_id: view.map_id,
        };
        let same = |written: &Played| {
            (written.id, &written.name, written.level, written.breed, &written.sex)
                == (
                    character.id,
                    &character.name,
                    character.level,
                    character.breed,
                    &character.sex,
                )
        };
        let changed = !self.written.as_ref().is_some_and(same);
        self.last = Some(character.clone());
        changed.then(|| {
            self.written = Some(character.clone());
            character
        })
    }
}

/// The characters played changed: one was remembered or forgotten.
#[derive(Debug, Clone, Serialize, Deserialize, Type, tauri_specta::Event)]
pub struct PlayedChanged;

/// Writes `character` among the player's files and says so to the webview; a failure is only logged, the game going
/// on as before.
pub(crate) fn remember(handle: &tauri::AppHandle, character: &Played) {
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

fn listed(files: &PlayersFiles, data: Option<&LoadedData>) -> Vec<PlayedCharacter> {
    let Some(store) = files.store() else { return Vec::new() };
    let named = data.and_then(LoadedData::named);
    store
        .played()
        .read
        .into_iter()
        .map(|character| {
            let sex = character
                .sex
                .as_deref()
                .map(|sex| if sex == "female" { Sex::Female } else { Sex::Male });
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
        })
        .collect()
}

/// The characters played, the most recently seen first.
#[tauri::command]
#[specta::specta]
pub async fn played_characters(app: tauri::AppHandle) -> Result<Vec<PlayedCharacter>, String> {
    let read = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        let data = app.try_state::<LoadedData>();
        listed(&files, data.as_deref())
    });
    read.await.map_err(|error| error.to_string())
}

/// Forget: the file of the character `key` goes to the system's trash.
#[tauri::command]
#[specta::specta]
pub async fn forget_character(app: tauri::AppHandle, key: String) -> Result<(), ForgetError> {
    let handle = app.clone();
    let forgotten = tauri::async_runtime::spawn_blocking(move || {
        let files = app.state::<PlayersFiles>();
        let store = files.store().ok_or(ForgetError::NoFolder)?;
        match store.forget(&key, |path| trash::delete(path).map_err(std::io::Error::other)) {
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

    fn view(name: &str, level: i32, map_id: Option<i64>) -> View {
        let who = Who {
            id: 0x9e_447f_0126,
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

    #[test]
    fn a_character_is_written_when_first_known_and_when_who_it_is_changes() {
        let mut remembering = Remembering::default();
        assert_eq!(remembering.seen(Some(&View::default()), 1), None, "not known yet");
        let first = remembering.seen(Some(&view("Kalyx", 54, Some(1))), 2);
        assert_eq!(
            first.map(|it| (it.name, it.level, it.last_seen)),
            Some(("Kalyx".into(), 54, 2))
        );
        assert_eq!(
            remembering.seen(Some(&view("Kalyx", 54, Some(2))), 3),
            None,
            "a new map alone writes nothing"
        );
        let level = remembering.seen(Some(&view("Kalyx", 55, Some(2))), 4);
        assert_eq!(level.map(|it| it.level), Some(55));
    }

    #[test]
    fn a_closed_connection_writes_when_and_where_its_character_was_last_seen() {
        let mut remembering = Remembering::default();
        remembering.seen(Some(&view("Kalyx", 54, Some(1))), 2);
        remembering.seen(Some(&view("Kalyx", 54, Some(7))), 3);
        let closed = remembering.seen(None, 9);
        assert_eq!(closed.map(|it| (it.last_seen, it.map_id)), Some((9, Some(7))));
        assert_eq!(remembering.seen(None, 10), None, "closed once");
        assert_eq!(
            Remembering::default().seen(None, 1),
            None,
            "no character, nothing to write"
        );
    }
}
