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

#[tauri::command]
#[specta::specta]
#[expect(
    clippy::needless_pass_by_value,
    reason = "Tauri hands commands their AppHandle by value"
)]
pub fn app_info(app: tauri::AppHandle) -> AppInfo {
    let data = app
        .path()
        .resource_dir()
        .map_err(|error| error.to_string())
        .and_then(|resources| crate::data::Manifest::load(&resources.join("data")).map_err(|error| error.to_string()));
    let (game_build, data_error) = match data {
        Ok(manifest) => (Some(manifest.build), None),
        Err(error) => (None, Some(error)),
    };
    AppInfo {
        version: env!("CARGO_PKG_VERSION").to_owned(),
        game_build,
        data_error,
        os: std::env::consts::OS.to_owned(),
    }
}
