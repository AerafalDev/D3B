//! The commands the webview calls. Each one is thin: it reads the app's state, calls a domain crate and returns plain
//! data.

use serde::Serialize;
use specta::Type;
use tauri::Manager;

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
