//! The owner's game captures read through the protocol (`docs/protocol.md`, "Tests"): every frame of a build with pins
//! read with them, none "not trusted", and every message of the live view decoded. A capture holds the account's
//! ticket: it prints counts by readable name and outcome only. Ignored unless `D3B_CAPTURES` names their folder.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};
use std::time::Duration;

use super::message::{Message, READABLE};
use super::outcome::Outcome;
use super::pins::Pins;
use super::{decoder, envelope};
use crate::relay::Side;
use crate::relay::capture::{Record, read};
use crate::relay::frame::Frames;

/// The folder `D3B_CAPTURES` names, which the replays read.
pub(crate) fn folder() -> PathBuf {
    PathBuf::from(std::env::var_os("D3B_CAPTURES").unwrap_or_else(|| panic!("set D3B_CAPTURES")))
}

fn captures(folder: &Path) -> Vec<PathBuf> {
    let mut found = Vec::new();
    for entry in std::fs::read_dir(folder).into_iter().flatten().flatten() {
        let path = entry.path();
        if path.is_dir() {
            found.extend(captures(&path));
        } else if path.extension().is_some_and(|extension| extension == "d3bcap") {
            found.push(path);
        }
    }
    found.sort();
    found
}

/// The frames of a capture's records, each with the side that sent it and when the read completing it came, in the
/// order they came whole. A capture cut mid-frame ends at its last whole one; a side whose bytes stop being frames fails
/// the replay, with the capture's `name`.
fn frames(name: &str, records: &[Record], micros: &[u64]) -> Vec<(Side, Duration, Vec<u8>)> {
    let (mut client, mut server) = (Frames::default(), Frames::default());
    let mut frames = Vec::new();
    for (record, &micros) in records.iter().zip(micros) {
        let (side, reader, bytes) = match record {
            Record::FromClient(bytes) => (Side::Client, &mut client, bytes),
            Record::FromServer(bytes) => (Side::Server, &mut server, bytes),
            Record::Other => continue,
        };
        let at = Duration::from_micros(micros);
        let pushed = reader.push(bytes, |body| frames.push((side, at, body.to_vec())));
        pushed.unwrap_or_else(|error| panic!("{name}: the {side:?} side is no longer in frames: {error}"));
    }
    frames
}

/// The game connections of a build with pins in `folder`, each a list of its frames, read with its build's pins: the
/// time each came and the message decoded, if one was.
pub(crate) fn decoded(folder: &Path, mut connection: impl FnMut(&[(Duration, Option<Message>)])) {
    for path in captures(folder) {
        let name = path.strip_prefix(folder).unwrap_or(&path).display().to_string();
        let bytes = std::fs::read(&path).unwrap_or_else(|error| panic!("{name}: {error}"));
        let capture = read(&bytes).unwrap_or_else(|error| panic!("{name}: {error}"));
        let Some(pins) = Pins::embedded(&capture.build) else {
            continue;
        };
        let pins = pins.unwrap_or_else(|error| panic!("{error}"));
        let frames = frames(&name, &capture.records, &capture.micros);
        if !is_a_game_connection(&frames) {
            continue;
        }
        let messages: Vec<_> = frames
            .iter()
            .map(|(side, at, body)| (*at, decoder::read(&pins, *side, body).1))
            .collect();
        connection(&messages);
    }
}

/// A game connection opens with a request in the game's envelope; a connection phase's frames are not one.
fn is_a_game_connection(frames: &[(Side, Duration, Vec<u8>)]) -> bool {
    let first = frames.iter().find(|(side, ..)| *side == Side::Client);
    first.is_some_and(|(side, _, body)| envelope::open(*side, body).is_some())
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_read_through_the_protocol() {
    let folder = folder();
    let mut outcomes: BTreeMap<String, usize> = BTreeMap::new();
    let mut by_name: BTreeMap<(&str, String), usize> = BTreeMap::new();
    let (mut games, mut unmapped) = (0, 0);
    for path in captures(&folder) {
        let name = path.strip_prefix(&folder).unwrap_or(&path).display().to_string();
        let bytes = std::fs::read(&path).unwrap_or_else(|error| panic!("{name}: {error}"));
        let capture = read(&bytes).unwrap_or_else(|error| panic!("{name}: {error}"));
        let frames = frames(&name, &capture.records, &capture.micros);
        if !is_a_game_connection(&frames) {
            continue;
        }
        games += 1;
        let Some(pins) = Pins::embedded(&capture.build) else {
            unmapped += 1;
            continue;
        };
        let pins = pins.unwrap_or_else(|error| panic!("{error}"));
        for (side, _, body) in &frames {
            let (outcome, _) = decoder::read(&pins, *side, body);
            *outcomes.entry(format!("{outcome:?}")).or_default() += 1;
            let readable = envelope::open(*side, body).and_then(|content| pins.readable(content.name));
            if let Some(readable) = readable {
                *by_name.entry((readable, format!("{outcome:?}"))).or_default() += 1;
            }
        }
    }
    eprintln!("{games} game connections ({unmapped} of a build without pins), frames by outcome: {outcomes:?}");
    for ((readable, outcome), count) in &by_name {
        eprintln!("  {readable}: {count} {outcome}");
    }
    assert!(
        games > unmapped,
        "no game capture of a build with pins in {}",
        folder.display()
    );
    let not_trusted: Vec<_> = by_name
        .iter()
        .filter(|((_, outcome), _)| *outcome == format!("{:?}", Outcome::NotTrusted))
        .collect();
    assert!(not_trusted.is_empty(), "not trusted: {not_trusted:?}");
    for readable in READABLE {
        assert!(
            by_name
                .keys()
                .any(|(name, outcome)| name == readable && outcome != "Unknown"),
            "{readable} was never decoded"
        );
    }
}
