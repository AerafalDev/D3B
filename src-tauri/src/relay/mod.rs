//! The relay between the official client and Ankama's servers, on the same machine (`docs/relay.md`, ADR 0011).
//!
//! At start it points the client's `ConfigUrl` at a copy of Ankama's configuration whose connection host is the relay
//! on loopback, and puts the line back at quit. Every frame passes through untouched but the server-selection answer,
//! whose game host and ports become the relay's. Nothing is sent to the game of the relay's own.

mod ankama_config;
mod connection_phase;
mod error;
mod frame;
mod game_connection;
mod handover;
mod redirect;
mod version_file;
mod wire;

pub use error::RelayError;

use std::net::Ipv4Addr;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use tokio::net::TcpListener;
use tokio::task::JoinHandle;

use ankama_config::ConnectionHost;
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
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
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
        }
    }

    fn apply(&mut self, change: Change) {
        match change {
            Change::Phase(Handed::NotUnderstood) => self.not_understood += 1,
            Change::Phase(Handed::Over | Handed::NotAsked) | Change::Game(Game::Refused) => {}
            Change::Game(Game::Opened) => self.game_connections += 1,
            Change::Game(Game::Closed) => self.game_connections = self.game_connections.saturating_sub(1),
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
    notify: Box<dyn Fn(Status) + Send + Sync>,
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

    /// A relay not started yet, telling `notify` each change of its status.
    pub fn new(notify: impl Fn(Status) + Send + Sync + 'static) -> Self {
        Self {
            inner: Arc::new(Inner {
                status: Mutex::new(Status::new()),
                notify: Box::new(notify),
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
    /// redirected until [`Relay::stop`]. A port that cannot be taken stops it before anything is redirected.
    pub async fn start(&self, client: PathBuf, state: PathBuf) {
        // Windows only, until a tester measures the others (the owner, 2026-11-11).
        if !cfg!(target_os = "windows") {
            self.update(|status| status.state = State::Unsupported);
            return;
        }
        let redirect = Redirect::new(&client, state);
        let build = redirect.client_build();
        self.update(|status| status.client_build = build);
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
        let host = match apply(&redirect).await {
            Ok(host) => host,
            Err(error) => {
                self.update(|status| status.state = State::Failed(error.to_string()));
                return;
            }
        };
        *lock(&self.inner.redirect) = Some(redirect.clone());

        let handover = Arc::new(Mutex::new(Handover::default()));
        let game = listeners.pop();
        let mut tasks = Vec::new();
        for listener in listeners {
            let relay = self.clone();
            let report = move |handed| relay.update(|status| status.apply(Change::Phase(handed)));
            tasks.push(tokio::spawn(connection_phase::serve(
                listener,
                host.host.clone(),
                Arc::clone(&handover),
                report,
            )));
        }
        if let Some(listener) = game {
            let relay = self.clone();
            let report = move |game| relay.update(|status| status.apply(Change::Game(game)));
            tasks.push(tokio::spawn(game_connection::serve(listener, handover, report)));
        }
        let relay = self.clone();
        tasks.push(tokio::spawn(async move { relay.keep_redirected(redirect).await }));
        *lock(&self.inner.tasks) = tasks;
        self.update(|status| status.state = State::Ready);
    }

    /// Redirects again whenever the launcher rewrote the `version` file (an update or a repair, both measured by the
    /// owner on 3.7.13.17, 2026-11-11).
    async fn keep_redirected(&self, redirect: Redirect) {
        let mut poll = tokio::time::interval(Self::POLL);
        loop {
            poll.tick().await;
            if !matches!(redirect.is_applied(), Ok(false)) {
                continue;
            }
            let state = match apply(&redirect).await {
                Ok(_) => State::Ready,
                Err(error) => State::Failed(error.to_string()),
            };
            self.update(|status| status.state = state);
        }
    }

    /// Ends every connection and puts Ankama's URL back in the `version` file. A write Windows refuses (another
    /// process holding the file) is tried again for a moment, since a line left redirected stops the next client.
    pub fn stop(&self) {
        for task in lock(&self.inner.tasks).drain(..) {
            task.abort();
        }
        let redirect = lock(&self.inner.redirect).take();
        let mut result = Ok(());
        if let Some(redirect) = redirect {
            for attempt in 0..5 {
                result = redirect.restore().map(|_| ());
                if !matches!(result, Err(RelayError::Io { .. })) {
                    break;
                }
                std::thread::sleep(Duration::from_millis(100 << attempt));
            }
        }
        self.update(|status| {
            status.game_connections = 0;
            status.state = match &result {
                Ok(()) => State::Stopped,
                Err(error) => State::Failed(error.to_string()),
            };
        });
    }

    /// Says the relay cannot start, for `reason`.
    pub fn fail(&self, reason: &str) {
        self.update(|status| status.state = State::Failed(reason.to_owned()));
    }

    fn update(&self, change: impl FnOnce(&mut Status)) {
        let status = {
            let mut status = lock(&self.inner.status);
            change(&mut status);
            status.clone()
        };
        (self.inner.notify)(status);
    }
}

/// Downloads Ankama's configuration and redirects the client to D3B's copy of it.
async fn apply(redirect: &Redirect) -> Result<ConnectionHost, RelayError> {
    let original = redirect.original_url()?;
    let downloaded = redirect::download(&original.url).await;
    redirect.apply(&original, downloaded.as_deref())
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
    fn counts_the_answers_not_understood() {
        let mut status = Status::new();

        for handed in [Handed::Over, Handed::NotAsked, Handed::NotUnderstood] {
            status.apply(Change::Phase(handed));
        }

        assert_eq!(status.not_understood, 1);
    }

    #[test]
    fn stops_with_nothing_to_restore_and_says_so() {
        let seen = Arc::new(Mutex::new(Vec::new()));
        let relay = {
            let seen = Arc::clone(&seen);
            Relay::new(move |status| lock(&seen).push(status.state))
        };

        relay.stop();

        assert_eq!(relay.status().state, State::Stopped);
        assert_eq!(*lock(&seen), vec![State::Stopped]);
    }
}

#[cfg(test)]
mod tests;
