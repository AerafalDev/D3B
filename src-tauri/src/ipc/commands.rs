//! The app's own commands: what it is (its version, its data) and its settings. Each one is thin: it reads the app's
//! state, calls a domain and returns plain data.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;

use super::blueprints::PlayersFiles;
use crate::storage::AppSettings;

/// What the About dialog and the status bar show.
#[derive(Debug, Clone, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct AppInfo {
    /// The app's version, from `Cargo.toml`.
    pub version: String,
    /// The client build whose data is packed in the app, or `None` when it was built without data.
    pub game_build: Option<String>,
    /// Why the packed data could not be read, when it could not.
    pub data_error: Option<String>,
    /// `windows`, `macos` or `linux`.
    pub os: String,
}

/// The packed data, read once at start, or why it could not be, and its folder, whose maps are read on demand.
pub struct LoadedData(
    pub Result<crate::data::GameData, String>,
    pub Option<std::path::PathBuf>,
);

impl LoadedData {
    /// What the data names (the world, the breeds and their heads' folder), when there is data.
    pub(crate) fn named(&self) -> Option<super::character::Named<'_>> {
        let data = self.0.as_ref().ok()?;
        Some(super::character::Named {
            world: &data.world,
            breeds: &data.breeds,
            folder: self.1.as_deref(),
        })
    }

    /// Reads the data folder Tauri bundles as a resource.
    pub fn load(app: &tauri::AppHandle) -> Self {
        let folder = app.path().resource_dir().map(|resources| resources.join("data"));
        match folder {
            Ok(folder) => Self(
                crate::data::GameData::load(&folder).map_err(|error| error.to_string()),
                Some(folder),
            ),
            Err(error) => Self(Err(error.to_string()), None),
        }
    }
}

#[tauri::command]
#[specta::specta]
#[expect(clippy::needless_pass_by_value, reason = "Tauri hands commands their state by value")]
pub fn app_info(data: tauri::State<'_, LoadedData>) -> AppInfo {
    let (game_build, data_error) = match &data.0 {
        Ok(data) => (Some(data.manifest.build.clone()), None),
        Err(error) => (None, Some(error.clone())),
    };
    AppInfo {
        version: env!("CARGO_PKG_VERSION").to_owned(),
        game_build,
        data_error,
        os: std::env::consts::OS.to_owned(),
    }
}

/// The app's settings (`docs/shell.md`, "Settings"), each with its default.
#[derive(Debug, Clone, Default, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct Settings {
    /// Shows the Protocol view and its panel; off by default.
    pub advanced_tools: bool,
}

impl From<AppSettings> for Settings {
    fn from(settings: AppSettings) -> Self {
        Self {
            advanced_tools: settings.advanced_tools,
        }
    }
}

impl From<&Settings> for AppSettings {
    fn from(settings: &Settings) -> Self {
        Self {
            advanced_tools: settings.advanced_tools,
        }
    }
}

/// Why the settings were not saved; they still hold for the session.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum SettingsError {
    /// The system names no folder for the player's files.
    NoFolder,
    /// The system could not write the file, or the file there does not read and is kept: its own words.
    NotSaved { reason: String },
}

/// The app's settings as read at start, and why their file is kept as it is when it does not read.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct SettingsRead {
    pub settings: Settings,
    /// Why `settings.json` does not read: the settings are then the defaults, and the file is never written over.
    pub kept: Option<String>,
}

/// The settings of the player's folder; the defaults when it has none, or when its file does not read, which is
/// reported and kept as it is (ADR 0015).
fn read_settings(files: &PlayersFiles) -> SettingsRead {
    let read = files.store().map(crate::storage::Store::settings);
    match read {
        None => SettingsRead {
            settings: Settings::default(),
            kept: None,
        },
        Some(Ok(settings)) => SettingsRead {
            settings: settings.into(),
            kept: None,
        },
        Some(Err(why)) => {
            tauri_plugin_log::log::warn!("the app's settings are kept as they are: {why}");
            SettingsRead {
                settings: Settings::default(),
                kept: Some(why.to_string()),
            }
        }
    }
}

fn save_settings(files: &PlayersFiles, settings: &Settings) -> Result<(), SettingsError> {
    let store = files.store().ok_or(SettingsError::NoFolder)?;
    store
        .save_settings(&settings.into())
        .map_err(|error| SettingsError::NotSaved {
            reason: error.to_string(),
        })
}

/// Reads the app's settings away from the webview's thread: a sync client may hold the file.
#[tauri::command]
#[specta::specta]
pub async fn app_settings(app: tauri::AppHandle) -> SettingsRead {
    let read = tauri::async_runtime::spawn_blocking(move || read_settings(&app.state::<PlayersFiles>()));
    read.await.unwrap_or_else(|error| SettingsRead {
        settings: Settings::default(),
        kept: Some(error.to_string()),
    })
}

/// Saves the app's settings whole, away from the webview's thread, behind any blueprint being saved. The front end
/// sends one save at a time, so that two quick changes are written in their order.
#[tauri::command]
#[specta::specta]
pub async fn set_app_settings(app: tauri::AppHandle, settings: Settings) -> Result<(), SettingsError> {
    let saved = tauri::async_runtime::spawn_blocking(move || save_settings(&app.state::<PlayersFiles>(), &settings));
    saved.await.unwrap_or_else(|error| {
        Err(SettingsError::NotSaved {
            reason: error.to_string(),
        })
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::storage::Store;
    use crate::storage::tests::Folder;

    const ON: Settings = Settings { advanced_tools: true };

    #[test]
    fn settings_set_are_read_back_and_default_to_advanced_tools_off() {
        let folder = Folder::new("ipc-settings");
        let files = PlayersFiles::new(Some(Store::new(folder.path().to_path_buf())));
        assert_eq!(read_settings(&files).settings, Settings { advanced_tools: false });
        assert_eq!(save_settings(&files, &ON), Ok(()));
        assert_eq!(read_settings(&files).settings, ON);
    }

    #[test]
    fn without_a_folder_the_settings_are_the_defaults_and_not_saved() {
        let files = PlayersFiles::new(None);
        assert_eq!(
            read_settings(&files),
            SettingsRead {
                settings: Settings::default(),
                kept: None
            }
        );
        assert_eq!(save_settings(&files, &ON), Err(SettingsError::NoFolder));
    }

    #[test]
    fn a_file_that_does_not_read_gives_the_defaults_and_refuses_the_save_with_why() {
        let folder = Folder::new("ipc-settings-kept");
        std::fs::write(folder.path().join("settings.json"), "{\"schema\": 9}")
            .unwrap_or_else(|error| panic!("{error}"));
        let files = PlayersFiles::new(Some(Store::new(folder.path().to_path_buf())));
        let read = read_settings(&files);
        assert_eq!(read.settings, Settings::default());
        assert!(
            read.kept.is_some_and(
                |why| why.ends_with("is kept as it is, not written over: written by a newer D3B: schema 9")
            ),
            "the file kept is reported at start"
        );
        let Err(SettingsError::NotSaved { reason }) = save_settings(&files, &ON) else {
            panic!("the save was not refused");
        };
        assert!(
            reason.ends_with("is kept as it is, not written over: written by a newer D3B: schema 9"),
            "{reason}"
        );
    }
}
