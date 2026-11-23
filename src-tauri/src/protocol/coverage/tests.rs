use std::collections::BTreeMap;
use std::path::Path;

use super::catalogue::{CATALOGUE, Entry, Family};
use super::replayed::{self, Replayed, ReplayedError, Tally};
use super::{Build, CoverageError, ENCODED, cell, check, report, thousands};
use crate::protocol::Pins;
use crate::protocol::message::READABLE;

const fn entry(name: &'static str) -> Entry {
    Entry {
        name,
        lua: &[],
        measured: &[],
    }
}

const WALKS: Entry = Entry {
    name: "MapMovementEvent",
    lua: &["map.cellId"],
    measured: &[("3.7.13.17", "protocol.md")],
};

const MOVEMENT: &[Family] = &[Family {
    name: "Movement",
    messages: &[WALKS, entry("MapMovementRequest")],
}];

fn build(name: &str, pinned: &[(&str, &str)], replayed: Option<Replayed>) -> Build {
    Build {
        name: name.to_owned(),
        pinned: pinned
            .iter()
            .map(|(readable, name)| ((*readable).to_owned(), (*name).to_owned()))
            .collect(),
        replayed,
    }
}

fn walks_replayed(understood: u64, read: u64, not_trusted: u64) -> Replayed {
    Replayed {
        build: "3.7.13.17".to_owned(),
        connections: 54,
        frames: 3_048_392,
        messages: BTreeMap::from([(
            "MapMovementEvent".to_owned(),
            Tally {
                understood,
                read,
                not_trusted,
            },
        )]),
    }
}

#[test]
fn a_message_listed_twice_is_refused() {
    const TWICE: &[Family] = &[Family {
        name: "Movement",
        messages: &[entry("MapMovementRequest"), entry("MapMovementRequest")],
    }];
    let refused = check(TWICE, &[], &[], &[]);
    assert_eq!(refused, Err(CoverageError::Twice("MapMovementRequest".to_owned())));
}

#[test]
fn a_name_neither_request_event_nor_response_is_refused() {
    const SHAPES: &[Family] = &[Family {
        name: "Actors",
        messages: &[entry("RoleplayActor")],
    }];
    assert_eq!(
        check(SHAPES, &[], &[], &[]),
        Err(CoverageError::Side("RoleplayActor".to_owned()))
    );
}

#[test]
fn a_message_decoded_pinned_or_replayed_but_not_listed_is_refused() {
    let walks = [build("3.7.13.17", &[("MapMovementEvent", "jnz")], None)];
    let missing = |name: &str| Err(CoverageError::Missing(name.to_owned()));
    assert_eq!(
        check(MOVEMENT, &walks, &["MapCurrentEvent"], &[]),
        missing("MapCurrentEvent")
    );
    assert_eq!(
        check(MOVEMENT, &walks, &[], &["MapChangeRequest"]),
        missing("MapChangeRequest")
    );
    let pinned = [build(
        "3.7.13.17",
        &[("MapMovementEvent", "jnz"), ("MapCurrentEvent", "jow")],
        None,
    )];
    assert_eq!(check(MOVEMENT, &pinned, &[], &[]), missing("MapCurrentEvent"));
    let mut replayed = walks_replayed(1, 0, 0);
    replayed.messages.insert("FightEndEvent".to_owned(), Tally::default());
    let replayed = [build("3.7.13.17", &[("MapMovementEvent", "jnz")], Some(replayed))];
    assert_eq!(check(MOVEMENT, &replayed, &[], &[]), missing("FightEndEvent"));
    assert_eq!(check(MOVEMENT, &walks, &["MapMovementEvent"], &[]), Ok(()));
}

#[test]
fn a_measure_on_a_build_that_does_not_pin_it_is_refused() {
    let unpinned = [build("3.7.13.17", &[], None)];
    let measured = Err(CoverageError::Measured {
        name: "MapMovementEvent".to_owned(),
        build: "3.7.13.17".to_owned(),
    });
    assert_eq!(check(MOVEMENT, &unpinned, &[], &[]), measured);
    assert_eq!(check(MOVEMENT, &[], &[], &[]), measured, "a build without pins");
    let pinned = [build("3.7.13.17", &[("MapMovementEvent", "jnz")], None)];
    assert_eq!(check(MOVEMENT, &pinned, &["MapMovementEvent"], &[]), Ok(()));
}

#[test]
fn a_cell_says_the_pin_decoded_encoded_frames_and_measure() {
    let build = build(
        "3.7.13.17",
        &[("MapMovementEvent", "jnz")],
        Some(walks_replayed(90_000, 1_224, 0)),
    );
    let decoded = &["MapMovementEvent"];
    assert_eq!(
        cell(&WALKS, &build, decoded, decoded),
        "`jnz`, decoded, encoded, 91,224 frames, [measured](checklists/protocol.md)"
    );
    assert_eq!(
        cell(&WALKS, &build, &[], &[]),
        "`jnz`, 91,224 frames, [measured](checklists/protocol.md)"
    );
}

#[test]
fn a_cell_counts_the_frames_not_trusted() {
    let build = build(
        "3.7.13.17",
        &[("MapMovementEvent", "jnz")],
        Some(walks_replayed(1, 0, 2)),
    );
    assert_eq!(
        cell(&WALKS, &build, &[], &[]),
        "`jnz`, 1 frame, 2 not trusted, [measured](checklists/protocol.md)"
    );
}

#[test]
fn a_message_decoded_but_not_pinned_in_a_build_says_so_and_nothing_known_is_a_dash() {
    let other = build("3.7.14.18", &[], None);
    assert_eq!(cell(&WALKS, &other, &["MapMovementEvent"], &[]), "decoded, not pinned");
    assert_eq!(cell(&WALKS, &other, &[], &[]), "—");
}

#[test]
fn the_report_sums_each_build_then_lists_each_family() {
    let builds = [
        build(
            "3.7.13.17",
            &[("MapMovementEvent", "jnz")],
            Some(walks_replayed(5, 0, 0)),
        ),
        build("3.7.14.18", &[("MapMovementEvent", "jnz")], None),
    ];
    let report = report(MOVEMENT, &builds, &["MapMovementEvent"], &[]).unwrap_or_else(|error| panic!("{error}"));
    assert!(report.contains("| 3.7.13.17 | 1 of 2 | 1 | 0 | 54 connections, 3,048,392 frames | 1 |"));
    assert!(report.contains("| 3.7.14.18 | 1 of 2 | 1 | 0 | not replayed | 0 |"));
    assert!(report.contains("## Movement\n\n| Message | Side | 3.7.13.17 | 3.7.14.18 | Lua |"));
    assert!(report.contains("| MapMovementRequest | client | — | — | — |"));
    assert!(report.contains("| MapMovementEvent | server | `jnz`, decoded, 5 frames, [measured]"));
    assert!(report.ends_with("| `jnz`, decoded | `map.cellId` |\n| MapMovementRequest | client | — | — | — |\n"));
}

#[test]
fn counts_are_grouped_by_three_digits() {
    assert_eq!(thousands(0), "0");
    assert_eq!(thousands(1_000), "1,000");
    assert_eq!(thousands(3_048_392), "3,048,392");
}

fn scratch(test: &str) -> std::path::PathBuf {
    let folder = std::env::temp_dir().join(format!("d3b-coverage-{}-{test}", std::process::id()));
    let _ = std::fs::remove_dir_all(&folder);
    folder
}

#[test]
fn a_replay_written_reads_back_and_one_never_made_reads_none() {
    let folder = scratch("back");
    let replayed = walks_replayed(90_000, 1_224, 0);
    replayed::write(&folder, &replayed).unwrap_or_else(|error| panic!("{error}"));
    let read = replayed::read(&folder, "3.7.13.17").unwrap_or_else(|error| panic!("{error}"));
    let never = replayed::read(&folder, "3.7.14.18").unwrap_or_else(|error| panic!("{error}"));
    let _ = std::fs::remove_dir_all(&folder);
    assert_eq!(read, Some(replayed));
    assert_eq!(never, None);
}

#[test]
fn a_replay_of_another_build_or_shape_is_refused() {
    let folder = scratch("refused");
    replayed::write(&folder, &walks_replayed(1, 0, 0)).unwrap_or_else(|error| panic!("{error}"));
    std::fs::rename(folder.join("3.7.13.17.json"), folder.join("3.7.14.18.json")).unwrap_or_else(|e| panic!("{e}"));
    let other = replayed::read(&folder, "3.7.14.18");
    std::fs::write(folder.join("3.7.13.17.json"), r#"{"build": "3.7.13.17"}"#).unwrap_or_else(|e| panic!("{e}"));
    let shape = replayed::read(&folder, "3.7.13.17");
    std::fs::create_dir_all(folder.join("1.2.3.json")).unwrap_or_else(|e| panic!("{e}"));
    let unreadable = replayed::read(&folder, "1.2.3");
    let _ = std::fs::remove_dir_all(&folder);
    assert!(matches!(other, Err(ReplayedError::Build { named, .. }) if named == "3.7.13.17"));
    assert!(matches!(shape, Err(ReplayedError::Shape { .. })));
    assert!(matches!(unreadable, Err(ReplayedError::Unreadable { .. })));
}

#[test]
fn every_measure_names_a_checklist() {
    let checklists = Path::new(env!("CARGO_MANIFEST_DIR")).join("../docs/checklists");
    for entry in CATALOGUE.iter().flat_map(|family| family.messages) {
        for (build, checklist) in entry.measured {
            assert!(
                checklists.join(checklist).is_file(),
                "{} on {build}: no checklist {checklist}",
                entry.name
            );
        }
    }
}

/// The builds the app pins, with their last replays.
fn builds() -> Vec<Build> {
    Pins::builds()
        .map(|name| {
            let pins = Pins::embedded(name)
                .and_then(Result::ok)
                .unwrap_or_else(|| panic!("the pins of {name}"));
            let pinned = pins
                .pinned()
                .map(|(readable, pinned)| (readable.to_owned(), pinned.to_owned()))
                .collect();
            let replayed = replayed::read(&replayed::folder(), name).unwrap_or_else(|error| panic!("{error}"));
            Build {
                name: name.to_owned(),
                pinned,
                replayed,
            }
        })
        .collect()
}

#[test]
fn the_coverage_report_is_current() {
    let report = report(CATALOGUE, &builds(), READABLE, ENCODED).unwrap_or_else(|error| panic!("{error}"));
    let path = Path::new(env!("CARGO_MANIFEST_DIR")).join("../docs/protocol-coverage.md");
    std::fs::write(&path, report).unwrap_or_else(|error| panic!("{}: {error}", path.display()));
}
