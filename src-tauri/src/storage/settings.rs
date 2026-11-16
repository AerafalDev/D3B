//! The app's own settings (`docs/shell.md`, "Settings"): `settings.json` at the root of the player's folder, one file
//! for the whole app, written whole. A file that does not read gives the defaults and is never written over, as every
//! file of the player's folder (ADR 0015).

use std::io;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

use super::Store;
use super::atomic;

/// The schema of `settings.json` this version reads and writes.
pub(crate) const SCHEMA: u32 = 1;

const FILE: &str = "settings.json";

/// The app's settings. Each one has its default, so a file written before it came reads.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
pub(crate) struct AppSettings {
    /// Shows the Protocol view and its panel (the owner, 2026-11-16: off by default).
    pub(crate) advanced_tools: bool,
}

/// The file as written: its schema, then the settings.
#[derive(Serialize, Deserialize)]
struct File {
    schema: u32,
    #[serde(flatten)]
    settings: AppSettings,
}

/// Why the settings file does not read as this version's.
#[derive(Debug, thiserror::Error)]
pub(crate) enum NotRead {
    #[error("it is not text")]
    NotText,
    #[error("written by a newer D3B: schema {found}")]
    Newer { found: u64 },
    #[error("not the app's settings: {0}")]
    NotSettings(String),
}

/// Why the settings could not be read or written.
#[derive(Debug, thiserror::Error)]
pub(crate) enum SettingsError {
    /// The system could not read it now (a sync client holding it), so it is not written over; a later save may pass.
    #[error("{} could not be read: {source}", path.display())]
    Unreadable { path: PathBuf, source: io::Error },
    #[error("{} is kept as it is, not written over: {why}", path.display())]
    Kept { path: PathBuf, why: NotRead },
    #[error("{} could not be written: {source}", path.display())]
    Write { path: PathBuf, source: io::Error },
}

impl Store {
    fn settings_path(&self) -> PathBuf {
        self.root().join(FILE)
    }

    /// The app's settings: the defaults when the file is not there yet, else what it holds, or why it does not read.
    pub(crate) fn settings(&self) -> Result<AppSettings, SettingsError> {
        read(&self.settings_path()).map(Option::unwrap_or_default)
    }

    /// Writes `settings` whole, unless a file there does not read as this version's settings.
    pub(crate) fn save_settings(&self, settings: &AppSettings) -> Result<(), SettingsError> {
        let _writing = self.lock();
        let path = self.settings_path();
        read(&path)?;
        let file = File {
            schema: SCHEMA,
            settings: settings.clone(),
        };
        // Plain fields of a struct always serialize.
        let mut text = serde_json::to_string_pretty(&file).unwrap_or_else(|error| unreachable!("{error}"));
        text.push('\n');
        let written = std::fs::create_dir_all(self.root()).and_then(|()| atomic::write(&path, text.as_bytes()));
        written.map_err(|source| SettingsError::Write { path, source })
    }
}

/// The settings at `path`, or none when there is no file yet.
fn read(path: &Path) -> Result<Option<AppSettings>, SettingsError> {
    let bytes = match std::fs::read(path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(source) => {
            return Err(SettingsError::Unreadable {
                path: path.to_path_buf(),
                source,
            });
        }
    };
    parse(&bytes).map(Some).map_err(|why| SettingsError::Kept {
        path: path.to_path_buf(),
        why,
    })
}

fn parse(bytes: &[u8]) -> Result<AppSettings, NotRead> {
    let text = std::str::from_utf8(bytes).map_err(|_| NotRead::NotText)?;
    let value: serde_json::Value =
        serde_json::from_str(text).map_err(|error| NotRead::NotSettings(error.to_string()))?;
    match value.get("schema").and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(SCHEMA) => Err(NotRead::Newer { found }),
        Some(found) if found == u64::from(SCHEMA) => serde_json::from_value::<File>(value)
            .map(|file| file.settings)
            .map_err(|error| NotRead::NotSettings(error.to_string())),
        _ => Err(NotRead::NotSettings(format!("its schema must be {SCHEMA}"))),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::tests::Folder;

    fn on() -> AppSettings {
        AppSettings { advanced_tools: true }
    }

    #[test]
    fn no_file_gives_the_defaults_advanced_tools_off() {
        let folder = Folder::new("settings-none");
        let store = Store::new(folder.path().join("D3B"));
        assert_eq!(store.settings().ok(), Some(AppSettings { advanced_tools: false }));
    }

    #[test]
    fn settings_saved_are_read_back_from_a_file_with_its_schema() {
        let folder = Folder::new("settings-saved");
        // The player's folder is made by the first save.
        let store = Store::new(folder.path().join("D3B"));
        store.save_settings(&on()).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(store.settings().ok(), Some(on()));
        let text = std::fs::read_to_string(folder.path().join("D3B").join("settings.json")).unwrap_or_default();
        assert_eq!(text, "{\n  \"schema\": 1,\n  \"advancedTools\": true\n}\n");
        store
            .save_settings(&AppSettings::default())
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(store.settings().ok(), Some(AppSettings::default()));
    }

    #[test]
    fn a_setting_missing_from_the_file_takes_its_default() {
        let folder = Folder::new("settings-missing");
        let store = Store::new(folder.path().to_path_buf());
        std::fs::write(folder.path().join("settings.json"), "{\"schema\": 1}")
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(store.settings().ok(), Some(AppSettings::default()));
    }

    #[test]
    fn a_file_that_does_not_read_is_reported_and_never_written_over() {
        for (text, why) in [
            (
                "{\"schema\": 2, \"advancedTools\": true}",
                "written by a newer D3B: schema 2",
            ),
            (
                "{\"advancedTools\": true}",
                "not the app's settings: its schema must be 1",
            ),
            ("{\"schema\": 0}", "not the app's settings: its schema must be 1"),
            ("{\"schema\": 1, \"advancedTools\": \"yes\"}", "not the app's settings"),
            ("not json", "not the app's settings"),
        ] {
            let folder = Folder::new("settings-kept");
            let store = Store::new(folder.path().to_path_buf());
            let path = folder.path().join("settings.json");
            std::fs::write(&path, text).unwrap_or_else(|error| panic!("{error}"));
            let read = store
                .settings()
                .err()
                .map(|error| error.to_string())
                .unwrap_or_default();
            assert!(
                read.contains(&format!("is kept as it is, not written over: {why}")),
                "{text}: {read}"
            );
            assert!(
                matches!(store.save_settings(&on()), Err(SettingsError::Kept { .. })),
                "{text}"
            );
            assert_eq!(std::fs::read_to_string(&path).ok().as_deref(), Some(text));
        }
    }

    #[test]
    fn bytes_that_are_no_text_are_kept_too() {
        let folder = Folder::new("settings-bytes");
        let store = Store::new(folder.path().to_path_buf());
        let path = folder.path().join("settings.json");
        std::fs::write(&path, [0xff, 0xfe]).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(
            store.settings(),
            Err(SettingsError::Kept {
                why: NotRead::NotText,
                ..
            })
        ));
        assert!(store.save_settings(&on()).is_err());
        assert_eq!(std::fs::read(&path).ok(), Some(vec![0xff, 0xfe]));
    }
}
