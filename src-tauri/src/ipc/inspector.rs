//! The protocol inspector (`docs/protocol.md`, "The inspector"): each game connection's last frames kept, the one
//! connection watched streamed to the webview at most five batches a second, and a frame's detail on demand.

use std::collections::HashMap;
use std::sync::{Arc, Mutex, PoisonError};
use std::time::Instant;

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri_specta::Event;

use crate::protocol::{FieldValue, Frame, Frames, Kept, Line, Message, Observer, Outcome, Pins, detail};
use crate::relay::Side;

/// How many summaries one batch carries at most: the newest, the others counted as skipped.
const BATCH: usize = 1_000;

/// A frame as the inspector lists it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct FrameSummary {
    pub number: u32,
    /// Milliseconds since the connection opened.
    pub millis: u32,
    pub from_client: bool,
    /// Its message's name in the build, when its envelope opened.
    pub name: Option<String>,
    /// The readable name pinned to it.
    pub readable: Option<String>,
    pub outcome: FrameOutcome,
    pub size: u32,
}

/// A frame's outcome (`docs/protocol.md`, "Decoding"), or a secret, which is never decoded.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum FrameOutcome {
    Understood,
    Read,
    Unknown,
    NotTrusted,
    Secret,
}

/// A line of a frame's detail (`protocol::detail`). A 64-bit value is written out, as JavaScript's numbers would round
/// it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum DetailLine {
    /// A line of the message decoded: a field by its name, nested ones indented.
    Decoded {
        text: String,
    },
    Varint {
        number: u32,
        value: String,
    },
    Fixed64 {
        number: u32,
        value: String,
    },
    Fixed32 {
        number: u32,
        value: u32,
    },
    /// Anything length-delimited, by its length alone: a text is never shown.
    Bytes {
        number: u32,
        length: u32,
    },
    /// What the wire walker could not read further.
    Unreadable,
}

impl From<Line> for DetailLine {
    fn from(line: Line) -> Self {
        match line {
            Line::Decoded(text) => Self::Decoded { text },
            Line::Field { number, value } => match value {
                FieldValue::Varint(value) => Self::Varint {
                    number,
                    value: value.to_string(),
                },
                FieldValue::Fixed64(value) => Self::Fixed64 {
                    number,
                    value: value.to_string(),
                },
                FieldValue::Fixed32(value) => Self::Fixed32 { number, value },
                FieldValue::Bytes(length) => Self::Bytes {
                    number,
                    length: saturated(length),
                },
            },
            Line::Unreadable => Self::Unreadable,
        }
    }
}

/// What `inspector_frame` answers: a frame's lines (none for a secret), or why it has none.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum FrameAnswer {
    Detail {
        lines: Vec<DetailLine>,
    },
    /// Its connection closed, and its frames with it.
    Closed,
    /// It is no longer among the frames kept.
    Gone,
}

/// New frames of the connection watched; `skipped` more came than one batch carries.
#[derive(Debug, Clone, Serialize, Deserialize, Type, tauri_specta::Event)]
pub struct InspectorFrames {
    pub connection: u32,
    pub frames: Vec<FrameSummary>,
    pub skipped: u32,
}

/// One connection's frames, its build for a detail's pins, and the last frame streamed.
struct Watched {
    frames: Frames,
    build: Option<String>,
    opened: Instant,
    sent: u64,
}

/// Every connection's frames, and the one connection watched.
#[derive(Default)]
pub struct Inspector {
    state: Mutex<(HashMap<u32, Watched>, Option<u32>)>,
}

fn saturated(value: impl TryInto<u32>) -> u32 {
    value.try_into().unwrap_or(u32::MAX)
}

fn summary(kept: &Kept, opened: Instant) -> FrameSummary {
    FrameSummary {
        number: saturated(kept.number),
        millis: saturated(kept.at.saturating_duration_since(opened).as_millis()),
        from_client: kept.side == Side::Client,
        name: kept.name.clone(),
        readable: kept.readable.map(str::to_owned),
        outcome: if kept.secret {
            FrameOutcome::Secret
        } else {
            match kept.outcome {
                Outcome::Understood => FrameOutcome::Understood,
                Outcome::Read => FrameOutcome::Read,
                Outcome::Unknown => FrameOutcome::Unknown,
                Outcome::NotTrusted => FrameOutcome::NotTrusted,
            }
        },
        size: saturated(kept.size),
    }
}

impl Inspector {
    fn with<T>(&self, act: impl FnOnce(&mut HashMap<u32, Watched>, &mut Option<u32>) -> T) -> T {
        let mut state = self.state.lock().unwrap_or_else(PoisonError::into_inner);
        let (connections, watched) = &mut *state;
        act(connections, watched)
    }

    fn open(&self, connection: u32, build: Option<String>, opened: Instant) {
        self.with(|connections, _| {
            connections.insert(
                connection,
                Watched {
                    frames: Frames::default(),
                    build,
                    opened,
                    sent: 0,
                },
            );
        });
    }

    fn push(&self, connection: u32, frame: &Frame<'_>) {
        self.with(|connections, _| connections.get_mut(&connection).map(|kept| kept.frames.push(frame)));
    }

    fn close(&self, connection: u32) {
        self.with(|connections, watched| {
            connections.remove(&connection);
            if *watched == Some(connection) {
                *watched = None;
            }
        });
    }

    /// Watches `connection`, or none: the frames it keeps now, the next ones streamed from them on.
    pub(crate) fn watch(&self, connection: Option<u32>) -> Vec<FrameSummary> {
        self.with(|connections, watched| {
            *watched = connection;
            let Some(kept) = connection.and_then(|connection| connections.get_mut(&connection)) else {
                return Vec::new();
            };
            kept.sent = kept.frames.last();
            kept.frames.after(0).map(|frame| summary(frame, kept.opened)).collect()
        })
    }

    /// The frames of `connection` not streamed yet, when it is the one watched: at most [`BATCH`], the newest, and how
    /// many came before them.
    fn fresh(&self, connection: u32) -> Option<(Vec<FrameSummary>, u32)> {
        self.with(|connections, watched| {
            if *watched != Some(connection) {
                return None;
            }
            let kept = connections.get_mut(&connection)?;
            let fresh: Vec<_> = kept.frames.after(kept.sent).collect();
            kept.sent = kept.frames.last();
            let skipped = fresh.len().saturating_sub(BATCH);
            let frames = fresh[skipped..]
                .iter()
                .map(|frame| summary(frame, kept.opened))
                .collect();
            Some((frames, saturated(skipped)))
        })
    }

    /// The detail of the `number`th frame of `connection`, or why there is none.
    pub(crate) fn detail(&self, connection: u32, number: u32) -> FrameAnswer {
        let found = self.with(|connections, _| {
            let Some(connection) = connections.get(&connection) else {
                return Err(FrameAnswer::Closed);
            };
            let kept = connection.frames.get(u64::from(number)).ok_or(FrameAnswer::Gone)?;
            Ok((kept.clone(), connection.build.clone()))
        });
        let (kept, build) = match found {
            Ok(found) => found,
            Err(answer) => return answer,
        };
        // Decoded out of the lock: the session tasks push frames meanwhile.
        let pins = build.as_deref().and_then(Pins::embedded).and_then(Result::ok);
        FrameAnswer::Detail {
            lines: detail(pins.as_ref(), &kept).into_iter().map(DetailLine::from).collect(),
        }
    }
}

/// An observer that keeps each frame of its connection for the inspector, and streams them while watched, around
/// another observer it hands the rest to.
pub(crate) struct Inspected {
    pub(crate) inner: Box<dyn Observer>,
    pub(crate) connection: u32,
    pub(crate) inspector: Arc<Inspector>,
    pub(crate) send: Box<dyn Fn(&InspectorFrames) + Send>,
}

impl Inspected {
    pub(crate) fn new(
        inner: Box<dyn Observer>,
        connection: u32,
        build: Option<String>,
        inspector: Arc<Inspector>,
        send: Box<dyn Fn(&InspectorFrames) + Send>,
    ) -> Self {
        inspector.open(connection, build, Instant::now());
        Self {
            inner,
            connection,
            inspector,
            send,
        }
    }
}

impl Observer for Inspected {
    fn message(&mut self, message: &Message, at: Instant, dropped: u64) {
        self.inner.message(message, at, dropped);
    }

    fn tick(&mut self, now: Instant, dropped: u64) {
        self.inner.tick(now, dropped);
        if let Some((frames, skipped)) = self.inspector.fresh(self.connection)
            && !frames.is_empty()
        {
            (self.send)(&InspectorFrames {
                connection: self.connection,
                frames,
                skipped,
            });
        }
    }

    fn closed(&mut self) {
        self.inspector.close(self.connection);
        self.inner.closed();
    }

    fn frame(&mut self, frame: &Frame<'_>) {
        self.inspector.push(self.connection, frame);
    }
}

/// Watches the frames of `connection`, or none: those kept now; the next come as `InspectorFrames`. The front end
/// watches none as it starts, so that a reload leaves nothing streamed to no one.
#[tauri::command]
#[specta::specta]
#[expect(clippy::needless_pass_by_value, reason = "Tauri hands commands their state by value")]
pub fn inspector_watch(connection: Option<u32>, inspector: tauri::State<'_, Arc<Inspector>>) -> Vec<FrameSummary> {
    inspector.watch(connection)
}

/// The detail of the `number`th frame of `connection`, or why there is none.
#[tauri::command]
#[specta::specta]
#[expect(clippy::needless_pass_by_value, reason = "Tauri hands commands their state by value")]
pub fn inspector_frame(connection: u32, number: u32, inspector: tauri::State<'_, Arc<Inspector>>) -> FrameAnswer {
    inspector.detail(connection, number)
}

/// Sends `frames` to the webview.
pub(crate) fn emit(handle: &tauri::AppHandle, frames: &InspectorFrames) {
    let _ = frames.emit(handle);
}

#[cfg(test)]
mod tests {
    use super::*;

    fn frame(body: &[u8], secret: bool) -> Frame<'_> {
        Frame {
            side: Side::Server,
            at: Instant::now(),
            name: Some(if secret { "koh" } else { "lok" }),
            readable: None,
            secret,
            outcome: Outcome::Unknown,
            body,
        }
    }

    #[test]
    fn streams_the_connection_watched_only_from_where_it_was_watched() {
        let inspector = Inspector::default();
        inspector.open(1, None, Instant::now());
        inspector.open(2, None, Instant::now());
        inspector.push(1, &frame(b"a", false));
        assert_eq!(inspector.fresh(1), None, "not watched");

        let backlog = inspector.watch(Some(1));
        assert_eq!(backlog.len(), 1);
        inspector.push(1, &frame(b"b", false));
        inspector.push(2, &frame(b"c", false));
        let (fresh, skipped) = inspector.fresh(1).unwrap_or_default();
        assert_eq!(
            (fresh.iter().map(|frame| frame.number).collect::<Vec<_>>(), skipped),
            (vec![2], 0)
        );
        assert_eq!(
            inspector.fresh(1).map(|(frames, _)| frames.len()),
            Some(0),
            "nothing new"
        );
        assert_eq!(inspector.fresh(2), None, "another connection");

        inspector.watch(None);
        inspector.push(1, &frame(b"d", false));
        assert_eq!(inspector.fresh(1), None, "watched no more");
    }

    #[test]
    fn a_batch_carries_the_newest_and_counts_the_others() {
        let inspector = Inspector::default();
        inspector.open(1, None, Instant::now());
        inspector.watch(Some(1));
        for _ in 0..BATCH + 5 {
            inspector.push(1, &frame(b"x", false));
        }

        let (fresh, skipped) = inspector.fresh(1).unwrap_or_default();

        assert_eq!((fresh.len(), skipped), (BATCH, 5));
        assert_eq!(fresh.first().map(|frame| frame.number), Some(6));
    }

    #[test]
    fn a_closed_connection_keeps_nothing_and_is_watched_no_more() {
        let inspector = Inspector::default();
        inspector.open(1, None, Instant::now());
        inspector.push(1, &frame(b"a", false));
        inspector.watch(Some(1));

        inspector.close(1);

        assert_eq!(inspector.detail(1, 1), FrameAnswer::Closed);
        assert_eq!(inspector.fresh(1), None);
    }

    #[test]
    fn details_a_secret_by_no_line_and_says_when_a_frame_is_gone() {
        let inspector = Inspector::default();
        inspector.open(1, Some("3.7.13.17".into()), Instant::now());
        inspector.push(1, &frame(b"secret bytes", true));

        assert_eq!(inspector.detail(1, 1), FrameAnswer::Detail { lines: Vec::new() });
        let listed = inspector.watch(Some(1));
        assert_eq!(
            listed.iter().map(|one| (one.outcome, one.size)).collect::<Vec<_>>(),
            [(FrameOutcome::Secret, 12)]
        );
        assert_eq!(inspector.detail(1, 2), FrameAnswer::Gone);
    }

    #[test]
    fn writes_out_a_64_bit_value_javascript_would_round() {
        let line = DetailLine::from(Line::Field {
            number: 2,
            value: FieldValue::Varint(u64::MAX),
        });

        assert_eq!(
            line,
            DetailLine::Varint {
                number: 2,
                value: "18446744073709551615".into()
            }
        );
    }
}
