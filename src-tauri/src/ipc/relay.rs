//! The relay's commands and event: its status for the status bar, and the restore the updater awaits before it
//! installs, since on Windows the installer ends the app before its exit hook runs (`docs/relay.md`).

use std::path::PathBuf;
use std::sync::Arc;

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use super::LoadedData;
use crate::data::{GameData, MapPosition};
use crate::protocol::{ProtocolState, Session};
use crate::relay::{self, Relay};

/// What the status bar shows of the relay.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct RelayStatus {
    pub state: RelayState,
    /// Why it does not redirect, when it failed.
    pub reason: Option<String>,
    /// The client's build, from its `version` file.
    pub client_build: Option<String>,
    /// The game connections relayed now.
    pub game_connections: u32,
    /// The selection answers not understood since start, whose game went straight to Ankama.
    pub not_understood: u32,
    /// What the protocol read of each game connection, in the order they opened.
    pub connections: Vec<GameConnectionStatus>,
}

/// A game connection as the relay's tooltip shows it (`docs/protocol.md`, "In the app"): its counts saturate at
/// `u32::MAX`, more frames than a connection carries in years.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct GameConnectionStatus {
    pub number: u32,
    /// The client's build its connection phase named.
    pub build: Option<String>,
    pub protocol: GameProtocol,
    pub understood: u32,
    pub read: u32,
    pub unknown: u32,
    pub not_trusted: u32,
    /// Frames the protocol fell too far behind to read.
    pub dropped: u32,
    /// Where its current map stands, when the game data is of its build: only then are the coordinates its own.
    pub map: Option<MapAt>,
}

/// A map's coordinates in the world, as the game shows them.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
pub struct MapAt {
    pub x: i16,
    pub y: i16,
}

/// Whether a game connection's frames are read.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum GameProtocol {
    On,
    /// Its build has no pins.
    Unmapped,
    /// Its connection phase named no build.
    UnknownBuild,
    /// Its bytes stopped being frames.
    Lost,
}

impl GameConnectionStatus {
    fn new(number: u64, session: Session, data: Option<&GameData>) -> Self {
        let map = data.and_then(|data| {
            map_at(
                &data.world.positions,
                &data.manifest.build,
                session.build.as_deref(),
                session.map_id,
            )
        });
        let count = |count: u64| u32::try_from(count).unwrap_or(u32::MAX);
        let counts = session.counts;
        Self {
            number: u32::try_from(number).unwrap_or(u32::MAX),
            build: session.build,
            protocol: match session.state {
                ProtocolState::On => GameProtocol::On,
                ProtocolState::Unmapped => GameProtocol::Unmapped,
                ProtocolState::UnknownBuild => GameProtocol::UnknownBuild,
                ProtocolState::Lost => GameProtocol::Lost,
            },
            understood: count(counts.understood),
            read: count(counts.read),
            unknown: count(counts.unknown),
            not_trusted: count(counts.not_trusted),
            dropped: count(counts.dropped),
            map,
        }
    }
}

/// The coordinates of `map_id` among `positions`, which are sorted by map id, when the data's build is the
/// connection's: another build may number its maps otherwise.
fn map_at(positions: &[MapPosition], data_build: &str, build: Option<&str>, map_id: Option<i64>) -> Option<MapAt> {
    if build != Some(data_build) {
        return None;
    }
    let id = u32::try_from(map_id?).ok()?;
    let index = positions.binary_search_by_key(&id, |position| position.id).ok()?;
    Some(MapAt {
        x: positions[index].x,
        y: positions[index].y,
    })
}

/// Where the relay stands.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum RelayState {
    Starting,
    Ready,
    Failed,
    Unsupported,
    Stopped,
}

/// The relay's status changed.
#[derive(Debug, Clone, Serialize, Deserialize, Type, tauri_specta::Event)]
pub struct RelayChanged(pub RelayStatus);

impl RelayStatus {
    /// The relay's `status` as the status bar shows it, its maps found in `data`.
    fn new(status: relay::Status, data: Option<&GameData>) -> Self {
        let (state, reason) = match status.state {
            relay::State::Starting => (RelayState::Starting, None),
            relay::State::Ready => (RelayState::Ready, None),
            relay::State::Failed(reason) => (RelayState::Failed, Some(reason)),
            relay::State::Unsupported => (RelayState::Unsupported, None),
            relay::State::Stopped => (RelayState::Stopped, None),
        };
        Self {
            state,
            reason,
            client_build: status.client_build,
            game_connections: status.game_connections,
            not_understood: status.not_understood,
            connections: status
                .connections
                .into_iter()
                .map(|connection| GameConnectionStatus::new(connection.number, connection.session, data))
                .collect(),
        }
    }
}

/// The relay's status now.
#[tauri::command]
#[specta::specta]
#[expect(clippy::needless_pass_by_value, reason = "Tauri hands commands their state by value")]
pub fn relay_status(relay: tauri::State<'_, Relay>, data: tauri::State<'_, LoadedData>) -> RelayStatus {
    RelayStatus::new(relay.status(), data.0.as_ref().ok())
}

/// Ends the relayed connections and puts Ankama's configuration back in the client, before an update installs; why it
/// could not, when the `version` file stayed redirected. Off the main thread, since a refused write is tried again.
#[tauri::command]
#[specta::specta]
pub async fn relay_restore(relay: tauri::State<'_, Relay>) -> Result<(), String> {
    let relay = relay.inner().clone();
    tauri::async_runtime::spawn_blocking(move || relay.stop())
        .await
        .map_err(|error| error.to_string())?
        .map_err(|error| error.to_string())
}

/// Makes the app's relay, which tells the webview each change, and starts it on the installed client.
pub fn start(app: &tauri::App) {
    let handle = app.handle().clone();
    let inspector = Arc::new(super::inspector::Inspector::default());
    app.manage(Arc::clone(&inspector));
    let relay = Relay::new(
        move |status| {
            // The data is managed before the relay starts (`lib.rs`); without it, no map is shown.
            let data = handle.try_state::<LoadedData>();
            let data = data.as_deref().and_then(|data| data.0.as_ref().ok());
            let _ = RelayChanged(RelayStatus::new(status, data)).emit(&handle);
        },
        super::character::follow(app.handle().clone(), Arc::clone(&inspector)),
    );
    app.manage(relay.clone());
    let paths = app.path();
    // The client's folder, as the extractor finds it (`docs/data.md`): `DOFUS_CLIENT`, else where the launcher
    // installs it on Windows.
    let client = std::env::var_os("DOFUS_CLIENT").map(PathBuf::from).or_else(|| {
        paths
            .local_data_dir()
            .ok()
            .map(|folder| folder.join("Ankama").join("Dofus-dofus3"))
    });
    let state = paths.app_local_data_dir().ok().map(|folder| folder.join("redirect"));
    // Captures of the game connections, for the maintainer only: they hold the account's ticket (docs/protocol.md).
    // An empty or relative folder would put them wherever the app was started from: only an absolute one is taken.
    let record = std::env::var_os("D3B_RECORD")
        .map(PathBuf::from)
        .filter(|folder| folder.is_absolute());
    tauri::async_runtime::spawn(async move {
        match (client, state) {
            (Some(client), Some(state)) => relay.start(client, state, record).await,
            _ => relay.fail("the app's folders cannot be found"),
        }
    });
}

/// Puts the client's configuration back as the app ends.
pub fn stop(app: &tauri::AppHandle) {
    if let Some(relay) = app.try_state::<Relay>() {
        // Nothing is left to tell at exit: the status already says it failed.
        let _ = relay.stop();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn position(id: u32, x: i16, y: i16) -> MapPosition {
        MapPosition {
            id,
            x,
            y,
            sub_area_id: 0,
            world_map: 1,
            name: None,
            flags: Vec::new(),
        }
    }

    #[test]
    fn finds_the_map_of_a_connection_of_the_datas_build() {
        let positions = [
            position(5, 1, 2),
            position(153_092_354, 4, -18),
            position(153_092_355, 5, -18),
        ];

        assert_eq!(
            map_at(&positions, "3.7.13.17", Some("3.7.13.17"), Some(153_092_354)),
            Some(MapAt { x: 4, y: -18 })
        );
    }

    #[test]
    fn a_connections_counts_saturate_and_its_state_is_named() {
        let session = Session {
            build: Some("3.7.13.17".into()),
            state: ProtocolState::Lost,
            counts: crate::protocol::Counts {
                understood: u64::from(u32::MAX) + 1,
                read: 2,
                unknown: 3,
                not_trusted: 4,
                dropped: 5,
            },
            map_id: Some(153_092_354),
        };

        let status = GameConnectionStatus::new(u64::from(u32::MAX) + 7, session, None);

        assert_eq!((status.number, status.understood), (u32::MAX, u32::MAX));
        assert_eq!(
            (status.read, status.unknown, status.not_trusted, status.dropped),
            (2, 3, 4, 5)
        );
        assert_eq!((status.protocol, status.map), (GameProtocol::Lost, None));
    }

    #[test]
    fn shows_no_map_of_another_build_or_one_it_does_not_know() {
        let positions = [position(5, 1, 2)];

        assert_eq!(map_at(&positions, "3.7.13.17", Some("3.7.14.1"), Some(5)), None);
        assert_eq!(map_at(&positions, "3.7.13.17", None, Some(5)), None);
        assert_eq!(map_at(&positions, "3.7.13.17", Some("3.7.13.17"), Some(6)), None);
        assert_eq!(map_at(&positions, "3.7.13.17", Some("3.7.13.17"), Some(-5)), None);
        assert_eq!(map_at(&positions, "3.7.13.17", Some("3.7.13.17"), None), None);
    }
}
