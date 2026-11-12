//! The protocol of one game connection (`docs/protocol.md`, "The tap" and "In the app"): the gate on its build, and a
//! task that reads the frames the relay's tap hands it through a bounded channel, never waited for, and reports what
//! it read at most once a second.

use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::time::Duration;

use tokio::sync::mpsc;
use tokio::task::JoinHandle;
use tokio::time::MissedTickBehavior;

use super::decoder;
use super::message::Message;
use super::outcome::Outcome;
use super::pins::Pins;
use crate::relay::Side;

/// How many frames may wait for the protocol before one more is dropped and counted (`docs/protocol.md`, "The tap").
const WAITING: usize = 1024;

/// How often, at most, a connection's protocol tells what changed.
const REPORT_EVERY: Duration = Duration::from_secs(1);

/// Whether a connection's frames are read.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ProtocolState {
    /// Read with its build's pins.
    On,
    /// Its build has no pins: relayed untouched, not read.
    Unmapped,
    /// Its connection phase named no build.
    UnknownBuild,
    /// Its bytes stopped being frames: the copy goes on, the reading stopped.
    Lost,
}

/// How many frames gave each outcome, and how many were dropped because the protocol fell behind.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Counts {
    pub understood: u64,
    pub read: u64,
    pub unknown: u64,
    pub not_trusted: u64,
    pub dropped: u64,
}

/// What the protocol knows of one game connection.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Session {
    pub build: Option<String>,
    pub state: ProtocolState,
    pub counts: Counts,
    /// The current map, from the last `MapCurrentEvent`; a dropped one leaves the last shown.
    pub map_id: Option<i64>,
}

/// What the tap and the task share without a lock.
#[derive(Debug, Default)]
struct Shared {
    dropped: AtomicU64,
    lost: AtomicBool,
}

/// The relay's end of a connection's protocol: it hands frames over without waiting.
#[derive(Debug)]
pub(crate) struct Tap {
    frames: mpsc::Sender<(Side, Vec<u8>)>,
    shared: Arc<Shared>,
    /// The task reading what the tap gives; it ends once the tap is dropped with its connection.
    _task: JoinHandle<()>,
}

impl Tap {
    /// Hands the body of a frame `side` sent to the protocol, or counts it dropped when too many wait.
    pub(crate) fn frame(&self, side: Side, body: &[u8]) {
        if let Err(mpsc::error::TrySendError::Full(_)) = self.frames.try_send((side, body.to_vec())) {
            self.shared.dropped.fetch_add(1, Ordering::Relaxed);
        }
    }

    /// Says the connection's bytes stopped being frames: no more will come.
    pub(crate) fn lost(&self) {
        self.shared.lost.store(true, Ordering::Relaxed);
    }
}

/// The gate (`docs/protocol.md`, "Decoding"): a build with pins is read with them; one without, or no build, is not.
fn gate(build: Option<&str>) -> (ProtocolState, Option<Pins>) {
    match build.map(Pins::embedded) {
        None => (ProtocolState::UnknownBuild, None),
        // Embedded pins that do not check are a bug the tests catch; the connection is not read rather than misread.
        Some(None | Some(Err(_))) => (ProtocolState::Unmapped, None),
        Some(Some(Ok(pins))) => (ProtocolState::On, Some(pins)),
    }
}

/// Opens the protocol of a game connection of `build`, on the current Tokio runtime: its tap, and a task telling
/// `report` the session at once, then at most once a second while it changes, and `None` once the tap is dropped.
pub(crate) fn open(build: Option<String>, report: impl Fn(Option<Session>) + Send + 'static) -> Tap {
    open_with(build, WAITING, report)
}

fn open_with(build: Option<String>, waiting: usize, report: impl Fn(Option<Session>) + Send + 'static) -> Tap {
    let (state, pins) = gate(build.as_deref());
    let (frames, received) = mpsc::channel(waiting);
    let shared = Arc::new(Shared::default());
    let session = Session {
        build,
        state,
        counts: Counts::default(),
        map_id: None,
    };
    let task = tokio::spawn(run(session, pins, received, Arc::clone(&shared), report));
    Tap {
        frames,
        shared,
        _task: task,
    }
}

async fn run(
    mut session: Session,
    pins: Option<Pins>,
    mut frames: mpsc::Receiver<(Side, Vec<u8>)>,
    shared: Arc<Shared>,
    report: impl Fn(Option<Session>),
) {
    report(Some(session.clone()));
    let mut tick = tokio::time::interval(REPORT_EVERY);
    tick.set_missed_tick_behavior(MissedTickBehavior::Delay);
    tick.tick().await;
    let mut changed = false;
    loop {
        tokio::select! {
            frame = frames.recv() => {
                let Some((side, body)) = frame else { break };
                // A connection not read takes its frames all the same, so that none is counted dropped.
                if let Some(pins) = &pins {
                    read(&mut session, pins, side, &body);
                    changed = true;
                }
            }
            _ = tick.tick() => {
                let dropped = shared.dropped.load(Ordering::Relaxed);
                let lost = shared.lost.load(Ordering::Relaxed) && session.state == ProtocolState::On;
                if lost {
                    session.state = ProtocolState::Lost;
                }
                if changed || lost || dropped != session.counts.dropped {
                    session.counts.dropped = dropped;
                    report(Some(session.clone()));
                    changed = false;
                }
            }
        }
    }
    report(None);
}

/// Reads one frame into `session`: its outcome counted, and the current map kept.
fn read(session: &mut Session, pins: &Pins, side: Side, body: &[u8]) {
    let (outcome, message) = decoder::read(pins, side, body);
    let counts = &mut session.counts;
    match outcome {
        Outcome::Understood => counts.understood += 1,
        Outcome::Read => counts.read += 1,
        Outcome::Unknown => counts.unknown += 1,
        Outcome::NotTrusted => counts.not_trusted += 1,
    }
    if let Some(Message::MapCurrentEvent(current)) = message {
        session.map_id = Some(current.map_id);
    }
}

#[cfg(test)]
mod tests;
