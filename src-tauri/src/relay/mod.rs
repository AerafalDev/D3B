//! The relay between the official client and Ankama's servers, on the same machine (`docs/relay.md`, ADR 0011).
//!
//! At start it points the client's `ConfigUrl` at a copy of Ankama's configuration whose connection host is the relay
//! on loopback, and puts the line back at quit. Every frame passes through untouched but the server-selection answer,
//! whose game host and ports become the relay's. Nothing is sent to the game of the relay's own.

mod ankama_config;
pub(crate) mod capture;
mod connection_phase;
mod error;
pub(crate) mod frame;
mod game_connection;
mod handover;
mod redirect;
mod version_file;
pub(crate) mod wire;

pub use error::RelayError;
pub(crate) use game_connection::Side;

use std::net::Ipv4Addr;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::net::TcpListener;
use tokio::task::JoinHandle;
use tokio::time::MissedTickBehavior;

use crate::protocol::Session;
use ankama_config::ConnectionHost;
use capture::Recording;
use connection_phase::Handed;
use game_connection::Game;
use handover::Handover;
use redirect::Redirect;

/// The loopback address the relay listens on: not `127.0.0.1`, so that no other local program's port is taken
/// (inferred, `main:docs/client.md`, D34 and D36, on Windows x64).
pub(crate) const ADDRESS: Ipv4Addr = Ipv4Addr::new(127, 0, 13, 59);

/// The port of the relay's game listener, which the server-selection answer names in place of Ankama's (inferred,
/// `main:docs/client-game.md`, "The game connection", measured on Windows x64).
pub(crate) const GAME_PORT: u16 = 5556;

/// What the relay is doing, as the status bar shows it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Status {
    pub state: State,
    /// The client's build, from its `version` file, once it was read.
    pub client_build: Option<String>,
    /// The game connections relayed now.
    pub game_connections: u32,
    /// The connection phases whose selection answer was not understood since start, so whose game went straight to
    /// Ankama.
    pub not_understood: u32,
    /// What the protocol read of each game connection, in the order they opened (`docs/protocol.md`, "In the app").
    pub connections: Vec<GameConnection>,
}

/// A game connection as the protocol reads it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GameConnection {
    /// Its rank among the game connections the relay accepted since start.
    pub number: u64,
    pub session: Session,
}

/// Where the relay stands.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum State {
    Starting,
    /// Listening, with the client redirected.
    Ready,
    /// Not redirecting, for this reason; the poll tries again every 2 s once it listens.
    Failed(String),
    /// Not measured on this platform yet: nothing is redirected (`docs/relay.md`, "Platforms").
    Unsupported,
    Stopped,
}

/// What changes the status while the relay runs.
#[derive(Debug, Clone, PartialEq, Eq)]
enum Change {
    Phase(Handed),
    Game(Game),
}

impl Status {
    fn new() -> Self {
        Self {
            state: State::Starting,
            client_build: None,
            game_connections: 0,
            not_understood: 0,
            connections: Vec::new(),
        }
    }

    fn apply(&mut self, change: Change) {
        match change {
            Change::Phase(Handed::NotUnderstood) => self.not_understood += 1,
            Change::Phase(Handed::Over | Handed::NotAsked) | Change::Game(Game::Refused) => {}
            Change::Game(Game::Opened) => self.game_connections += 1,
            Change::Game(Game::Closed) => self.game_connections = self.game_connections.saturating_sub(1),
            Change::Game(Game::Protocol(number, None)) => {
                self.connections.retain(|connection| connection.number != number);
            }
            Change::Game(Game::Protocol(number, Some(session))) => {
                match self
                    .connections
                    .iter_mut()
                    .find(|connection| connection.number == number)
                {
                    Some(connection) => connection.session = session,
                    None => self.connections.push(GameConnection { number, session }),
                }
            }
        }
    }
}

/// The relay for the app's life: redirects the client and carries its connections from [`Relay::start`] to
/// [`Relay::stop`]. Every change of its [`Status`] is told to the `notify` it was made with.
#[derive(Clone)]
pub struct Relay {
    inner: Arc<Inner>,
}

struct Inner {
    status: Mutex<Status>,
    /// Told each change of the status, under its lock: it must not ask the relay for anything.
    notify: Box<dyn Fn(Status) + Send + Sync>,
    /// Set once by [`Relay::stop`]: nothing is redirected after it.
    stopped: AtomicBool,
    /// The redirect applied, to restore; its lock orders every write of `version`.
    redirect: Mutex<Option<Redirect>>,
    tasks: Mutex<Vec<JoinHandle<()>>>,
}

impl std::fmt::Debug for Relay {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        formatter
            .debug_struct("Relay")
            .field("status", &self.status())
            .finish_non_exhaustive()
    }
}

impl Relay {
    /// How often the `version` file is read again, to redirect again after the launcher rewrote it.
    const POLL: Duration = Duration::from_secs(2);
    /// How long a redirect that failed waits before it is tried again, so that a lasting refusal (a configuration D3B
    /// cannot relay) does not download Ankama's every 2 s.
    const RETRY: Duration = Duration::from_secs(30);
    /// How long a refused write of `version` waits before it is tried again, at most four times.
    const RESTORE_RETRY: Duration = Duration::from_millis(250);

    /// A relay not started yet, telling `notify` each change of its status.
    pub fn new(notify: impl Fn(Status) + Send + Sync + 'static) -> Self {
        Self {
            inner: Arc::new(Inner {
                status: Mutex::new(Status::new()),
                notify: Box::new(notify),
                stopped: AtomicBool::new(false),
                redirect: Mutex::new(None),
                tasks: Mutex::new(Vec::new()),
            }),
        }
    }

    /// The relay's status now.
    pub fn status(&self) -> Status {
        lock(&self.inner.status).clone()
    }

    /// Listens on the relay's ports, redirects the client installed in `client` (its state in `state`), and keeps it
    /// redirected until [`Relay::stop`]. A port that cannot be taken stops it before anything is redirected; a stop
    /// while it starts leaves nothing redirected.
    /// With `record`, each game connection is recorded in that folder (`D3B_RECORD`, `docs/protocol.md`).
    pub async fn start(&self, client: PathBuf, state: PathBuf, record: Option<PathBuf>) {
        // Windows only, until a tester measures the others (the owner, 2026-11-11).
        if !cfg!(target_os = "windows") {
            self.update(|status| status.state = State::Unsupported);
            return;
        }
        let redirect = Redirect::new(&client, state);
        let build = redirect.client_build();
        self.update(|status| status.client_build.clone_from(&build));
        let mut listeners = Vec::new();
        for port in [5555, 443, GAME_PORT] {
            match TcpListener::bind((ADDRESS, port)).await {
                Ok(listener) => listeners.push(listener),
                Err(error) => {
                    let reason = format!("port {port} of {ADDRESS} cannot be taken: {error}");
                    self.update(|status| status.state = State::Failed(reason));
                    return;
                }
            }
        }
        let host = match self.redirect_now(&redirect).await {
            Some(Ok(host)) => Arc::new(Mutex::new(host.host)),
            Some(Err(error)) => {
                self.update(|status| status.state = State::Failed(error.to_string()));
                return;
            }
            None => return,
        };

        let handover = Arc::new(Mutex::new(Handover::default()));
        let game = listeners.pop();
        let mut tasks = Vec::new();
        for listener in listeners {
            let relay = self.clone();
            let report = move |handed| relay.update(|status| status.apply(Change::Phase(handed)));
            tasks.push(tokio::spawn(connection_phase::serve(
                listener,
                Arc::clone(&host),
                Arc::clone(&handover),
                report,
            )));
        }
        if let Some(listener) = game {
            let relay = self.clone();
            let report = move |game| relay.update(|status| status.apply(Change::Game(game)));
            let recording = record.map(|folder| Recording { folder });
            tasks.push(tokio::spawn(game_connection::serve(
                listener, handover, recording, report,
            )));
        }
        let relay = self.clone();
        tasks.push(tokio::spawn(async move { relay.keep_redirected(redirect, host).await }));
        lock(&self.inner.tasks).extend(tasks);
        // A stop that came while the tasks were spawned found none to end: end them now.
        if self.inner.stopped.load(Ordering::SeqCst) {
            for task in lock(&self.inner.tasks).drain(..) {
                task.abort();
            }
            return;
        }
        self.update(|status| status.state = State::Ready);
    }

    /// Redirects again whenever the launcher rewrote the `version` file (an update or a repair, both measured by the
    /// owner on 3.7.13.17, 2026-11-11), with the connection host Ankama's configuration names then.
    async fn keep_redirected(&self, redirect: Redirect, host: Arc<Mutex<String>>) {
        let mut poll = tokio::time::interval(Self::POLL);
        poll.set_missed_tick_behavior(MissedTickBehavior::Delay);
        let mut not_before = tokio::time::Instant::now();
        loop {
            poll.tick().await;
            if tokio::time::Instant::now() < not_before || !matches!(redirect.is_applied(), Ok(false)) {
                continue;
            }
            let state = match self.redirect_now(&redirect).await {
                None => return,
                Some(Ok(found)) => {
                    *lock(&host) = found.host;
                    State::Ready
                }
                Some(Err(error)) => {
                    not_before = tokio::time::Instant::now() + Self::RETRY;
                    State::Failed(error.to_string())
                }
            };
            self.update(|status| status.state = state);
        }
    }

    /// Downloads Ankama's configuration and redirects the client to D3B's copy of it; `None`, writing nothing, once
    /// the relay is stopped. The write is made under the lock [`Relay::stop`] restores under, so that no redirect
    /// lands after the restore.
    async fn redirect_now(&self, redirect: &Redirect) -> Option<Result<ConnectionHost, RelayError>> {
        let original = match redirect.original_url() {
            Ok(original) => original,
            Err(error) => return Some(Err(error)),
        };
        let downloaded = redirect::download(&original.url).await;
        let mut applied = lock(&self.inner.redirect);
        if self.inner.stopped.load(Ordering::SeqCst) {
            return None;
        }
        let result = redirect.apply(&original, downloaded.as_deref());
        if result.is_ok() {
            *applied = Some(redirect.clone());
        }
        Some(result)
    }

    /// Ends every connection and puts Ankama's URL back in the `version` file, for good: the relay does not start
    /// again. A write Windows refuses (another process holding the file) is tried again for a moment, since a line
    /// left redirected stops the next client; a restore that still fails is kept for the next call to try.
    pub fn stop(&self) -> Result<(), RelayError> {
        self.stop_with(Self::RESTORE_RETRY)
    }

    fn stop_with(&self, retry: Duration) -> Result<(), RelayError> {
        self.inner.stopped.store(true, Ordering::SeqCst);
        for task in lock(&self.inner.tasks).drain(..) {
            task.abort();
        }
        let result = {
            let mut applied = lock(&self.inner.redirect);
            let mut result = Ok(());
            if let Some(redirect) = applied.as_ref() {
                for attempt in 0..5 {
                    if attempt > 0 {
                        std::thread::sleep(retry);
                    }
                    result = redirect.restore().map(|_| ());
                    if !matches!(result, Err(RelayError::Io { .. })) {
                        break;
                    }
                }
            }
            if result.is_ok() {
                *applied = None;
            }
            result
        };
        self.update(|status| {
            status.game_connections = 0;
            status.state = match &result {
                Ok(()) => State::Stopped,
                Err(error) => State::Failed(error.to_string()),
            };
        });
        result
    }

    /// Says the relay cannot start, for `reason`.
    pub fn fail(&self, reason: &str) {
        self.update(|status| status.state = State::Failed(reason.to_owned()));
    }

    /// Changes the status and tells it, under the status's lock, so that changes are told in the order they happened.
    fn update(&self, change: impl FnOnce(&mut Status)) {
        let mut status = lock(&self.inner.status);
        change(&mut status);
        (self.inner.notify)(status.clone());
    }
}

fn lock<T>(mutex: &Mutex<T>) -> std::sync::MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(std::sync::PoisonError::into_inner)
}

#[cfg(test)]
mod status_tests {
    use super::*;

    #[test]
    fn counts_the_game_connections_open() {
        let mut status = Status::new();

        status.apply(Change::Game(Game::Opened));
        status.apply(Change::Game(Game::Opened));
        status.apply(Change::Game(Game::Closed));
        status.apply(Change::Game(Game::Refused));

        assert_eq!(status.game_connections, 1);
    }

    #[test]
    fn never_counts_below_zero() {
        let mut status = Status::new();

        status.apply(Change::Game(Game::Closed));

        assert_eq!(status.game_connections, 0);
    }

    #[test]
    fn keeps_each_connections_protocol_until_it_ends() {
        let mut status = Status::new();
        let session = |map_id| Session {
            build: Some("3.7.13.17".into()),
            state: crate::protocol::ProtocolState::On,
            counts: crate::protocol::Counts::default(),
            map_id,
        };

        status.apply(Change::Game(Game::Protocol(1, Some(session(None)))));
        status.apply(Change::Game(Game::Protocol(2, Some(session(None)))));
        status.apply(Change::Game(Game::Protocol(1, Some(session(Some(7))))));
        status.apply(Change::Game(Game::Protocol(2, None)));

        assert_eq!(
            status.connections,
            [GameConnection {
                number: 1,
                session: session(Some(7))
            }]
        );
    }

    #[test]
    fn counts_the_answers_not_understood() {
        let mut status = Status::new();

        for handed in [Handed::Over, Handed::NotAsked, Handed::NotUnderstood] {
            status.apply(Change::Phase(handed));
        }

        assert_eq!(status.not_understood, 1);
    }

    #[test]
    fn stop_puts_ankamas_url_back_once() {
        let root = std::env::temp_dir().join(format!("d3b-relay-stop-{}", std::process::id()));
        let streaming = root.join("client").join("Dofus_Data").join("StreamingAssets");
        std::fs::create_dir_all(&streaming).unwrap_or_else(|error| panic!("{error}"));
        let ankamas = b"Version=3.7.13.17
ConfigUrl=https://config.example.com/config.json
";
        std::fs::write(streaming.join("version"), ankamas).unwrap_or_else(|error| panic!("{error}"));
        let redirect = Redirect::new(&root.join("client"), root.join("state"));
        let original = redirect.original_url().unwrap_or_else(|error| panic!("{error}"));
        let config = r#"{"connectionHosts":["production:connection.example.com:5555,443"]}"#;
        redirect
            .apply(&original, Some(config))
            .unwrap_or_else(|error| panic!("{error}"));
        let relay = Relay::new(|_| {});
        *lock(&relay.inner.redirect) = Some(redirect);

        let first = relay.stop_with(Duration::ZERO);
        let second = relay.stop_with(Duration::ZERO);
        let version = std::fs::read(streaming.join("version")).unwrap_or_default();
        let _ = std::fs::remove_dir_all(&root);

        assert!(first.is_ok() && second.is_ok());
        assert_eq!(version, ankamas);
        assert_eq!(relay.status().state, State::Stopped);
    }

    #[test]
    fn stops_with_nothing_to_restore_and_says_so() {
        let seen = Arc::new(Mutex::new(Vec::new()));
        let relay = {
            let seen = Arc::clone(&seen);
            Relay::new(move |status| lock(&seen).push(status.state))
        };

        assert!(relay.stop().is_ok());

        assert_eq!(relay.status().state, State::Stopped);
        assert_eq!(*lock(&seen), vec![State::Stopped]);
    }
}

#[cfg(test)]
mod tests;
