//! The protocol of one game connection (`docs/protocol.md`, "The tap" and "In the app"): the gate on its build, and a
//! task that reads the frames the relay's tap hands it through a bounded channel, never waited for, and reports what
//! it read at most once a second. Each message decoded goes to the connection's [`Observer`], which the protocol knows
//! nothing more of (`docs/game.md`).

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

/// How often the observer is told the time, so that what it shows can follow a clock (a life regenerating).
const TICK: Duration = Duration::from_millis(200);

/// How many ticks, at most, between two reports of what changed: once a second.
const REPORT_EVERY: u32 = 5;

/// What one connection's decoded messages feed: told each as it is read, the time at every [`TICK`] while the
/// connection lasts, and its end.
pub(crate) trait Observer: Send + 'static {
    /// `message`, read at `at` while the connection had dropped `dropped` frames.
    fn message(&mut self, message: &Message, at: std::time::Instant, dropped: u64);
    fn tick(&mut self, now: std::time::Instant, dropped: u64);
    fn closed(&mut self);
}

/// An observer that keeps nothing: what a connection feeds until the game follows it.
pub(crate) struct Unobserved;

impl Observer for Unobserved {
    fn message(&mut self, _: &Message, _: std::time::Instant, _: u64) {}
    fn tick(&mut self, _: std::time::Instant, _: u64) {}
    fn closed(&mut self) {}
}

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
    /// Whether the gate let the connection be read: one that is not copies nothing.
    reading: bool,
    shared: Arc<Shared>,
    /// The task reading what the tap gives; it ends once the tap is dropped with its connection.
    _task: JoinHandle<()>,
}

impl Tap {
    /// Hands the body of a frame `side` sent to the protocol, copied only once there is room for it, or counts it
    /// dropped when too many wait; `None` says a side stopped being in frames, which stops the reading for good
    /// (`docs/protocol.md`, "The tap").
    pub(crate) fn give(&self, side: Side, body: Option<&[u8]>) {
        let Some(body) = body else {
            self.shared.lost.store(true, Ordering::Relaxed);
            return;
        };
        if !self.reading || self.shared.lost.load(Ordering::Relaxed) {
            return;
        }
        match self.frames.try_reserve() {
            Ok(permit) => permit.send((side, body.to_vec())),
            Err(mpsc::error::TrySendError::Full(())) => {
                self.shared.dropped.fetch_add(1, Ordering::Relaxed);
            }
            // The task is gone (only a bug ends it first): there is no one to count for.
            Err(mpsc::error::TrySendError::Closed(())) => {}
        }
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
pub(crate) fn open(
    build: Option<String>,
    report: impl Fn(Option<Session>) + Send + 'static,
    observer: Box<dyn Observer>,
) -> Tap {
    open_with(build, WAITING, report, observer)
}

fn open_with(
    build: Option<String>,
    waiting: usize,
    report: impl Fn(Option<Session>) + Send + 'static,
    observer: Box<dyn Observer>,
) -> Tap {
    let (state, pins) = gate(build.as_deref());
    let (frames, received) = mpsc::channel(waiting);
    let shared = Arc::new(Shared::default());
    let session = Session {
        build,
        state,
        counts: Counts::default(),
        map_id: None,
    };
    let task = tokio::spawn(run(session, pins, received, Arc::clone(&shared), report, observer));
    Tap {
        frames,
        reading: state == ProtocolState::On,
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
    mut observer: Box<dyn Observer>,
) {
    report(Some(session.clone()));
    let mut tick = tokio::time::interval(TICK);
    tick.set_missed_tick_behavior(MissedTickBehavior::Delay);
    tick.tick().await;
    let (mut changed, mut ticks) = (false, 0_u32);
    loop {
        tokio::select! {
            frame = frames.recv() => {
                let Some((side, body)) = frame else { break };
                if let Some(pins) = &pins {
                    let dropped = shared.dropped.load(Ordering::Relaxed);
                    if let Some(message) = read(&mut session, pins, side, &body) {
                        observer.message(&message, tokio::time::Instant::now().into_std(), dropped);
                    }
                    changed = true;
                }
            }
            now = tick.tick() => {
                let dropped = shared.dropped.load(Ordering::Relaxed);
                observer.tick(now.into_std(), dropped);
                ticks = (ticks + 1) % REPORT_EVERY;
                if ticks != 0 {
                    continue;
                }
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
    observer.closed();
    report(None);
}

/// Reads one frame into `session`: its outcome counted, and the current map kept; the message, when one was decoded.
fn read(session: &mut Session, pins: &Pins, side: Side, body: &[u8]) -> Option<Message> {
    let (outcome, message) = decoder::read(pins, side, body);
    let counts = &mut session.counts;
    match outcome {
        Outcome::Understood => counts.understood += 1,
        Outcome::Read => counts.read += 1,
        Outcome::Unknown => counts.unknown += 1,
        Outcome::NotTrusted => counts.not_trusted += 1,
    }
    if let Some(Message::MapCurrentEvent(current)) = &message {
        session.map_id = Some(current.map_id);
    }
    message
}

#[cfg(test)]
mod tests;
