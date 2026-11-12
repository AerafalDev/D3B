//! The owner's game captures read through the protocol (`docs/protocol.md`, "Tests"): every frame of a build with pins
//! read with them, none "not trusted", and every message of the live view decoded. A capture holds the account's
//! ticket: it prints counts by readable name and outcome only. Ignored unless `D3B_CAPTURES` names their folder.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use super::message::READABLE;
use super::outcome::Outcome;
use super::pins::Pins;
use super::{decoder, envelope};
use crate::relay::Side;
use crate::relay::capture::{Record, read};
use crate::relay::frame::Frames;

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

/// The frames of a capture's records, each with the side that sent it, in the order they came whole.
fn frames(records: &[Record]) -> Vec<(Side, Vec<u8>)> {
    let (mut client, mut server) = (Frames::default(), Frames::default());
    let mut frames = Vec::new();
    for record in records {
        let (side, reader, bytes) = match record {
            Record::FromClient(bytes) => (Side::Client, &mut client, bytes),
            Record::FromServer(bytes) => (Side::Server, &mut server, bytes),
            Record::Other => continue,
        };
        // The relay's replay checks every byte is in frames; a capture cut mid-frame ends at its last whole one.
        let _ = reader.push(bytes, |body| frames.push((side, body.to_vec())));
    }
    frames
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_read_through_the_protocol() {
    let folder = PathBuf::from(std::env::var_os("D3B_CAPTURES").unwrap_or_else(|| panic!("set D3B_CAPTURES")));
    let mut outcomes: BTreeMap<String, usize> = BTreeMap::new();
    let mut by_name: BTreeMap<(&str, String), usize> = BTreeMap::new();
    let (mut games, mut unmapped) = (0, 0);
    for path in captures(&folder) {
        let name = path.strip_prefix(&folder).unwrap_or(&path).display().to_string();
        let bytes = std::fs::read(&path).unwrap_or_else(|error| panic!("{name}: {error}"));
        let capture = read(&bytes).unwrap_or_else(|error| panic!("{name}: {error}"));
        let frames = frames(&capture.records);
        // A game connection opens with a request in the game's envelope; a connection phase's frames are not one.
        let first = frames.iter().find(|(side, _)| *side == Side::Client);
        if first.is_none_or(|(side, body)| envelope::open(*side, body).is_none()) {
            continue;
        }
        games += 1;
        let Some(pins) = Pins::embedded(&capture.build) else {
            unmapped += 1;
            continue;
        };
        let pins = pins.unwrap_or_else(|error| panic!("{error}"));
        for (side, body) in &frames {
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
