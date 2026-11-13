//! The frames the protocol inspector shows (`docs/protocol.md`, "The inspector"): the last ones of a connection kept,
//! bounded by count and by bytes, and a frame's detail. A secret's frame keeps its name and size, never its bytes; the
//! text of a frame read without our `.proto` files is never shown, only its length.

use std::collections::VecDeque;
use std::time::Instant;

use super::envelope;
use super::message::Message;
use super::outcome::Outcome;
use super::pins::Pins;
use crate::relay::Side;
use crate::relay::wire::{self, Value};

/// How many frames a connection keeps, at most, and how many of their bytes: the owner's choice (2026-11-12).
pub(crate) const KEPT: usize = 3_000;
pub(crate) const BYTES: usize = 16 * 1024 * 1024;

/// One frame as the protocol read it, handed to the connection's observer.
#[derive(Debug, Clone, Copy)]
pub(crate) struct Frame<'a> {
    pub side: Side,
    pub at: Instant,
    /// Its message's name in the build, when its envelope opened.
    pub name: Option<&'a str>,
    /// The readable name pinned to it.
    pub readable: Option<&'static str>,
    /// Whether it carries a session secret: its bytes are never kept.
    pub secret: bool,
    pub outcome: Outcome,
    pub body: &'a [u8],
}

/// A frame kept for the inspector.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Kept {
    /// Its rank among the connection's frames, from 1.
    pub number: u64,
    pub at: Instant,
    pub side: Side,
    pub name: Option<String>,
    pub readable: Option<&'static str>,
    pub secret: bool,
    pub outcome: Outcome,
    pub size: usize,
    /// Its bytes, empty for a secret.
    body: Vec<u8>,
}

/// The last frames of one connection, at most [`KEPT`] of them and [`BYTES`] of their bytes, the oldest dropped first.
#[derive(Debug, Default)]
pub(crate) struct Frames {
    kept: VecDeque<Kept>,
    bytes: usize,
    numbered: u64,
}

impl Frames {
    /// Keeps `frame`, dropping the oldest ones beyond the bounds; its number.
    pub(crate) fn push(&mut self, frame: &Frame<'_>) -> u64 {
        self.numbered += 1;
        let body = if frame.secret { Vec::new() } else { frame.body.to_vec() };
        self.bytes += body.len();
        self.kept.push_back(Kept {
            number: self.numbered,
            at: frame.at,
            side: frame.side,
            name: frame.name.map(str::to_owned),
            readable: frame.readable,
            secret: frame.secret,
            outcome: frame.outcome,
            size: frame.body.len(),
            body,
        });
        while self.kept.len() > KEPT || (self.bytes > BYTES && self.kept.len() > 1) {
            if let Some(oldest) = self.kept.pop_front() {
                self.bytes -= oldest.body.len();
            }
        }
        self.numbered
    }

    /// The frames kept after the `after`th, in order.
    pub(crate) fn after(&self, after: u64) -> impl Iterator<Item = &Kept> {
        self.kept.iter().filter(move |kept| kept.number > after)
    }

    /// The `number`th frame, while it is kept.
    pub(crate) fn get(&self, number: u64) -> Option<&Kept> {
        let first = self.kept.front()?.number;
        let index = usize::try_from(number.checked_sub(first)?).ok()?;
        self.kept.get(index)
    }

    /// The number of the last frame kept, or 0.
    pub(crate) fn last(&self) -> u64 {
        self.numbered
    }
}

/// A line of a frame's detail.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Line {
    /// A line of the message decoded: a field by its name, nested ones indented.
    Decoded(String),
    /// A top-level field by its number: one our `.proto` does not read, or any of a message read without it.
    Field { number: u32, value: FieldValue },
    /// What the wire walker could not read further.
    Unreadable,
}

/// A field's value as the wire walker reads it: anything length-delimited by its length alone, since a text cannot be
/// told from a nested message there.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum FieldValue {
    Varint(u64),
    Fixed64(u64),
    Fixed32(u32),
    Bytes(usize),
}

/// The lines that detail `kept`: its message decoded with `pins` when it is pinned, its fields by their names; else
/// the fields of its message at their top level, by number. A secret has none.
pub(crate) fn detail(pins: Option<&Pins>, kept: &Kept) -> Vec<Line> {
    if kept.secret {
        return Vec::new();
    }
    let content = envelope::open(kept.side, &kept.body);
    let decoded = content.zip(kept.readable).and_then(|(content, readable)| {
        pins?;
        Message::decode(readable, content.value)?.ok()
    });
    let value = content.map_or(kept.body.as_slice(), |content| content.value);
    if let Some(message) = decoded {
        // Under the message decoded, the top-level fields our `.proto` does not read, which prost dropped.
        let encoded = message.encode();
        let declared: Vec<u32> = wire::fields(&encoded).flatten().map(|field| field.number).collect();
        let mut lines: Vec<Line> = message.fields().into_iter().map(Line::Decoded).collect();
        lines.extend(fields(value, |number| !declared.contains(&number)));
        return lines;
    }
    fields(value, |_| true)
}

/// The top-level fields of `value` that `wanted` keeps, by number.
fn fields(value: &[u8], wanted: impl Fn(u32) -> bool) -> Vec<Line> {
    wire::fields(value)
        .filter(|field| field.as_ref().map_or(true, |field| wanted(field.number)))
        .map(|field| match field {
            Ok(field) => Line::Field {
                number: field.number,
                value: match field.value {
                    Value::Varint(value) => FieldValue::Varint(value),
                    Value::Fixed64(value) => FieldValue::Fixed64(value),
                    Value::Fixed32(value) => FieldValue::Fixed32(value),
                    Value::Bytes(bytes) => FieldValue::Bytes(bytes.len()),
                },
            },
            Err(_) => Line::Unreadable,
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::relay::wire::Field;

    fn message(fields: &[Field<'_>]) -> Vec<u8> {
        let mut out = Vec::new();
        wire::encode(fields, &mut out);
        out
    }

    /// A server event of the message `name`, holding `value`.
    fn event(name: &str, value: &[u8]) -> Vec<u8> {
        let url = format!("type.ankama.com/{name}");
        let bytes = |number, value| Field {
            number,
            value: Value::Bytes(value),
        };
        let any = message(&[bytes(1, url.as_bytes()), bytes(2, value)]);
        message(&[bytes(3, &message(&[bytes(1, &any)]))])
    }

    fn frame<'a>(body: &'a [u8], name: Option<&'a str>, readable: Option<&'static str>, secret: bool) -> Frame<'a> {
        Frame {
            side: Side::Server,
            at: Instant::now(),
            name,
            readable,
            secret,
            outcome: Outcome::Unknown,
            body,
        }
    }

    #[test]
    fn keeps_the_last_frames_by_count_and_finds_them_by_number() {
        let mut frames = Frames::default();
        for _ in 0..KEPT + 2 {
            frames.push(&frame(b"x", None, None, false));
        }

        assert_eq!(frames.last(), 3_002);
        assert_eq!(frames.get(2), None, "dropped");
        assert_eq!(frames.get(3).map(|kept| kept.number), Some(3));
        assert_eq!(frames.get(3_002).map(|kept| kept.number), Some(3_002));
        assert_eq!(frames.get(3_003), None);
        assert_eq!(frames.after(3_000).count(), 2);
    }

    #[test]
    fn keeps_no_more_bytes_than_its_bound() {
        let large = vec![0; BYTES / 2 + 1];
        let mut frames = Frames::default();
        frames.push(&frame(&large, None, None, false));
        frames.push(&frame(&large, None, None, false));

        // The first is dropped for the second; the one left is always kept, however large.
        assert_eq!(frames.after(0).map(|kept| kept.number).collect::<Vec<_>>(), [2]);
    }

    #[test]
    fn a_secret_keeps_its_name_and_size_never_its_bytes() {
        let body = event("koh", b"\x0a\x04abcd");
        let mut frames = Frames::default();
        let number = frames.push(&frame(&body, Some("koh"), None, true));

        let kept = frames.get(number).unwrap_or_else(|| panic!("not kept"));
        assert_eq!(
            (kept.name.as_deref(), kept.size, kept.body.len()),
            (Some("koh"), body.len(), 0)
        );
        assert_eq!(detail(None, kept), Vec::<Line>::new());
    }

    #[test]
    fn details_an_unpinned_frame_by_its_fields_and_lengths_never_its_text() {
        let value = message(&[
            Field {
                number: 1,
                value: Value::Varint(4),
            },
            Field {
                number: 2,
                value: Value::Bytes(b"SECRETNAME"),
            },
        ]);
        let body = event("lok", &value);
        let mut frames = Frames::default();
        let number = frames.push(&frame(&body, Some("lok"), None, false));

        let lines = detail(None, frames.get(number).unwrap_or_else(|| panic!("not kept")));

        assert_eq!(
            lines,
            [
                Line::Field {
                    number: 1,
                    value: FieldValue::Varint(4)
                },
                Line::Field {
                    number: 2,
                    value: FieldValue::Bytes(10)
                },
            ]
        );
    }

    #[test]
    fn details_a_pinned_frame_and_the_fields_our_proto_does_not_read() {
        let pins = Pins::embedded("3.7.13.17").and_then(Result::ok);
        let varint = |number, value| Field {
            number,
            value: Value::Varint(value),
        };
        // `InventoryWeightEvent` declares fields 1 and 2; 3 is not ours.
        let body = event("iqq", &message(&[varint(1, 812), varint(2, 2_350), varint(3, 7)]));
        let mut frames = Frames::default();
        let number = frames.push(&frame(&body, Some("iqq"), Some("InventoryWeightEvent"), false));

        let lines = detail(pins.as_ref(), frames.get(number).unwrap_or_else(|| panic!("not kept")));

        assert!(lines.contains(&Line::Decoded("weight: 812".into())), "{lines:?}");
        assert_eq!(
            lines.last(),
            Some(&Line::Field {
                number: 3,
                value: FieldValue::Varint(7)
            })
        );
        assert!(
            !lines.iter().any(|line| matches!(line, Line::Field { number: 1, .. })),
            "a field we read, listed again: {lines:?}"
        );
    }

    #[test]
    fn details_a_pinned_frame_decoded() {
        let pins = Pins::embedded("3.7.13.17").and_then(Result::ok);
        let body = event(
            "jow",
            &message(&[Field {
                number: 1,
                value: Value::Varint(153_092_354),
            }]),
        );
        let mut frames = Frames::default();
        let number = frames.push(&frame(&body, Some("jow"), Some("MapCurrentEvent"), false));

        let lines = detail(pins.as_ref(), frames.get(number).unwrap_or_else(|| panic!("not kept")));

        // Its fields alone: the list names the message already.
        assert_eq!(lines, [Line::Decoded("map_id: 153092354".into())]);
    }
}
