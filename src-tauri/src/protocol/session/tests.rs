use std::sync::Mutex;

use super::*;
use crate::relay::wire::{self, Field, Value};

fn message(fields: &[Field<'_>]) -> Vec<u8> {
    let mut out = Vec::new();
    wire::encode(fields, &mut out);
    out
}

fn bytes(number: u32, value: &[u8]) -> Field<'_> {
    Field {
        number,
        value: Value::Bytes(value),
    }
}

fn varint(number: u32, value: u64) -> Field<'static> {
    Field {
        number,
        value: Value::Varint(value),
    }
}

/// A server event of the message `name`, holding `value`.
fn event(name: &str, value: &[u8]) -> Vec<u8> {
    let url = format!("type.ankama.com/{name}");
    let any = message(&[bytes(1, url.as_bytes()), bytes(2, value)]);
    message(&[bytes(3, &message(&[bytes(1, &any)]))])
}

/// What a session reported, in order.
type Reports = Arc<Mutex<Vec<Option<Session>>>>;

/// What an observer was told, with its time in milliseconds of paused time: a message by its readable name and the
/// frames dropped before it, a tick, or the end.
#[derive(Debug, Clone, PartialEq, Eq)]
enum Told {
    Message(&'static str, u64, u128),
    Tick(u128),
    Closed,
}

struct Recorder {
    told: Arc<Mutex<Vec<Told>>>,
    start: std::time::Instant,
}

impl Recorder {
    fn push(&self, told: Told) {
        self.told
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .push(told);
    }

    fn millis(&self, at: std::time::Instant) -> u128 {
        at.saturating_duration_since(self.start).as_millis()
    }
}

impl Observer for Recorder {
    fn message(&mut self, message: &Message, at: std::time::Instant, dropped: u64) {
        let name = match message {
            Message::MapCurrentEvent(_) => "MapCurrentEvent",
            _ => "another",
        };
        self.push(Told::Message(name, dropped, self.millis(at)));
    }

    fn tick(&mut self, now: std::time::Instant, _: u64) {
        self.push(Told::Tick(self.millis(now)));
    }

    fn closed(&mut self) {
        self.push(Told::Closed);
    }
}

fn opened(build: Option<&str>, waiting: usize) -> (Tap, Reports) {
    observed(build, waiting).0
}

fn observed(build: Option<&str>, waiting: usize) -> ((Tap, Reports), Arc<Mutex<Vec<Told>>>) {
    let reports = Reports::default();
    let told = Arc::clone(&reports);
    let report = move |session| {
        told.lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .push(session);
    };
    let recorder = Recorder {
        told: Arc::default(),
        start: tokio::time::Instant::now().into_std(),
    };
    let observed = Arc::clone(&recorder.told);
    let tap = open_with(build.map(str::to_owned), waiting, report, Box::new(recorder));
    ((tap, reports), observed)
}

fn told(observed: &Arc<Mutex<Vec<Told>>>) -> Vec<Told> {
    std::mem::take(&mut *observed.lock().unwrap_or_else(std::sync::PoisonError::into_inner))
}

fn taken(reports: &Reports) -> Vec<Option<Session>> {
    std::mem::take(&mut *reports.lock().unwrap_or_else(std::sync::PoisonError::into_inner))
}

/// Lets the task run, then moves paused time on by `millis`.
async fn after(millis: u64) {
    tokio::task::yield_now().await;
    tokio::time::sleep(Duration::from_millis(millis)).await;
    tokio::task::yield_now().await;
}

#[test]
fn the_gate_reads_a_build_with_pins_only() {
    assert_eq!(gate(Some("3.7.13.17")).0, ProtocolState::On);
    assert_eq!(gate(Some("3.7.14.1")).0, ProtocolState::Unmapped);
    assert_eq!(gate(None).0, ProtocolState::UnknownBuild);
}

#[tokio::test(start_paused = true)]
async fn counts_each_outcome_and_keeps_the_current_map() {
    let (tap, reports) = opened(Some("3.7.13.17"), 16);

    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 153_092_354)]))));
    let weight = event("iqq", &message(&[varint(1, 1), varint(2, 2), varint(3, 3)]));
    tap.give(Side::Server, Some(&weight));
    tap.give(Side::Server, Some(&event("jqk", &[])));
    tap.give(Side::Server, Some(&event("jow", &message(&[bytes(1, b"x")]))));
    after(1_000).await;

    let reports = taken(&reports);
    let last = reports
        .last()
        .cloned()
        .flatten()
        .unwrap_or_else(|| panic!("{reports:?}"));
    assert_eq!(
        last.counts,
        Counts {
            understood: 1,
            read: 1,
            unknown: 1,
            not_trusted: 1,
            dropped: 0
        }
    );
    assert_eq!((last.state, last.map_id), (ProtocolState::On, Some(153_092_354)));
    assert_eq!(last.build.as_deref(), Some("3.7.13.17"));
}

#[tokio::test(start_paused = true)]
async fn reports_at_once_then_at_most_once_a_second_then_its_end() {
    let (tap, reports) = opened(Some("3.7.13.17"), 1_024);
    after(0).await;
    assert_eq!(taken(&reports).len(), 1, "told at once");

    for _ in 0..100 {
        tap.give(Side::Server, Some(&event("jqk", &[])));
        after(10).await;
    }
    let during = taken(&reports);
    // A second of frames told once: the tick that comes after them.
    assert_eq!(during.len(), 1, "{during:?}");
    after(5_000).await;
    assert!(taken(&reports).is_empty(), "told again with nothing changed");

    drop(tap);
    after(0).await;
    assert_eq!(taken(&reports), [None]);
}

#[tokio::test(start_paused = true)]
async fn a_frame_that_cannot_wait_is_dropped_and_counted() {
    let (tap, reports) = opened(Some("3.7.13.17"), 1);

    // The task has not run yet: one frame waits, the two after it are dropped.
    for _ in 0..3 {
        tap.give(Side::Server, Some(&event("jqk", &[])));
    }
    after(1_000).await;

    let last = taken(&reports)
        .last()
        .cloned()
        .flatten()
        .unwrap_or_else(|| panic!("no report"));
    assert_eq!((last.counts.unknown, last.counts.dropped), (1, 2));
}

#[tokio::test(start_paused = true)]
async fn a_connection_out_of_frames_is_lost() {
    let (tap, reports) = opened(Some("3.7.13.17"), 16);

    tap.give(Side::Client, None);
    after(1_000).await;

    let last = taken(&reports)
        .last()
        .cloned()
        .flatten()
        .unwrap_or_else(|| panic!("no report"));
    assert_eq!(last.state, ProtocolState::Lost);
}

#[tokio::test(start_paused = true)]
async fn a_connection_lost_reads_nothing_more() {
    let (tap, reports) = opened(Some("3.7.13.17"), 16);

    tap.give(Side::Server, None);
    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 1)]))));
    after(1_000).await;

    let last = taken(&reports)
        .last()
        .cloned()
        .flatten()
        .unwrap_or_else(|| panic!("no report"));
    assert_eq!(
        (last.state, last.counts, last.map_id),
        (ProtocolState::Lost, Counts::default(), None)
    );
}

#[tokio::test(start_paused = true)]
async fn a_build_without_pins_reads_nothing() {
    for (build, state) in [
        (Some("3.7.14.1"), ProtocolState::Unmapped),
        (None, ProtocolState::UnknownBuild),
    ] {
        let (tap, reports) = opened(build, 16);

        tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 1)]))));
        tap.give(Side::Client, None);
        after(5_000).await;
        drop(tap);
        after(0).await;

        let reports = taken(&reports);
        assert_eq!(reports.len(), 2, "{reports:?}");
        let first = reports[0].clone().unwrap_or_else(|| panic!("no session"));
        assert_eq!(
            (first.state, first.counts, first.map_id),
            (state, Counts::default(), None)
        );
        assert_eq!(reports[1], None);
    }
}

#[tokio::test(start_paused = true)]
async fn tells_its_observer_each_message_decoded_the_time_and_the_end() {
    let ((tap, _), observed) = observed(Some("3.7.13.17"), 1);

    // The second frame cannot wait: it is dropped, after the first was queued, so only frames after it carry it.
    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 1)]))));
    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 2)]))));
    after(0).await;
    // A message not pinned is not told.
    tap.give(Side::Server, Some(&event("jqk", &[])));
    after(50).await;
    tap.give(
        Side::Server,
        Some(&event("iqq", &message(&[varint(1, 1), varint(2, 2)]))),
    );
    after(0).await;
    assert_eq!(
        told(&observed),
        [Told::Message("MapCurrentEvent", 0, 0), Told::Message("another", 1, 50)]
    );

    after(950).await;
    let ticks: Vec<_> = [200, 400, 600, 800, 1_000].into_iter().map(Told::Tick).collect();
    assert_eq!(told(&observed), ticks, "a tick every 200 ms, at the time now");

    drop(tap);
    after(0).await;
    assert_eq!(told(&observed), [Told::Closed]);
}

#[tokio::test(start_paused = true)]
async fn a_lost_connection_ticks_its_observer_no_more() {
    let ((tap, _), observed) = observed(Some("3.7.13.17"), 16);
    after(200).await;
    assert_eq!(told(&observed), [Told::Tick(200)]);

    tap.give(Side::Server, None);
    after(1_000).await;
    drop(tap);
    after(0).await;

    assert_eq!(told(&observed), [Told::Closed]);
}

#[tokio::test(start_paused = true)]
async fn a_build_without_pins_tells_its_observer_no_message() {
    let ((tap, _), observed) = observed(Some("3.7.14.1"), 16);

    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 1)]))));
    after(200).await;
    drop(tap);
    after(0).await;

    assert_eq!(told(&observed), [Told::Tick(200), Told::Closed]);
}

/// A frame's name, readable name, secret mark, outcome and size.
type Seen = (Option<String>, Option<&'static str>, bool, Outcome, usize);

/// An observer that keeps what it saw of each frame.
#[derive(Default)]
struct Framed(Arc<Mutex<Vec<Seen>>>);

impl Observer for Framed {
    fn message(&mut self, _: &Message, _: std::time::Instant, _: u64) {}
    fn tick(&mut self, _: std::time::Instant, _: u64) {}
    fn closed(&mut self) {}
    fn frame(&mut self, frame: &Frame<'_>) {
        self.0.lock().unwrap_or_else(std::sync::PoisonError::into_inner).push((
            frame.name.map(str::to_owned),
            frame.readable,
            frame.secret,
            frame.outcome,
            frame.body.len(),
        ));
    }
}

#[tokio::test(start_paused = true)]
async fn tells_its_observer_each_frame_with_its_name_and_whether_it_is_a_secret() {
    let framed = Framed::default();
    let kept = Arc::clone(&framed.0);
    let tap = open_with(Some("3.7.13.17".into()), 16, |_| {}, Box::new(framed));
    let current = event("jow", &message(&[varint(1, 1)]));
    let secret = event("koh", &message(&[bytes(1, b"ab")]));

    tap.give(Side::Server, Some(&current));
    tap.give(Side::Server, Some(&secret));
    tap.give(Side::Server, Some(b"not an envelope"));
    after(0).await;

    let seen = std::mem::take(&mut *kept.lock().unwrap_or_else(std::sync::PoisonError::into_inner));
    assert_eq!(
        seen,
        [
            (
                Some("jow".into()),
                Some("MapCurrentEvent"),
                false,
                Outcome::Understood,
                current.len()
            ),
            (Some("koh".into()), None, true, Outcome::Unknown, secret.len()),
            (None, None, false, Outcome::Unknown, 15),
        ]
    );
}

#[tokio::test(start_paused = true)]
async fn a_connection_without_pins_tells_its_observer_no_frame() {
    let framed = Framed::default();
    let kept = Arc::clone(&framed.0);
    let tap = open_with(Some("3.7.14.1".into()), 16, |_| {}, Box::new(framed));

    tap.give(Side::Server, Some(&event("jow", &message(&[varint(1, 1)]))));
    after(0).await;

    assert!(
        kept.lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .is_empty()
    );
}
