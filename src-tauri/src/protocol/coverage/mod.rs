//! The protocol's coverage report, `docs/protocol-coverage.md` (ADR 0021, `docs/play.md`): each message the C# version
//! reads, by family, and per build whether D3B pins it, decodes it, encodes it, how many of its frames the last local
//! replay read, and whether it is measured in game; with the Lua calls it backs. `cargo test` writes it, as it writes
//! `src/bindings.ts`, and CI fails when it is stale.

mod catalogue;

pub(crate) mod replayed;

use std::collections::{BTreeMap, HashSet};
use std::fmt::Write as _;

use catalogue::Family;
use replayed::Replayed;

/// One build as the report shows it: its pins, and its last replay if it had one.
#[derive(Debug, Clone, PartialEq, Eq)]
struct Build {
    name: String,
    /// The build's name of each readable name it pins.
    pinned: BTreeMap<String, String>,
    replayed: Option<Replayed>,
}

/// A report that cannot be written: the catalogue and the code disagree.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
enum CoverageError {
    #[error("{0} is listed twice in the catalogue")]
    Twice(String),
    #[error("{0} is neither a request, an event nor a response")]
    Side(String),
    #[error("{0} is decoded, encoded, pinned or replayed, but not in the catalogue")]
    Missing(String),
    #[error("{name} is measured on {build}, which does not pin it")]
    Measured { name: String, build: String },
}

/// Who sends a message, by its readable name's end, as the C# version names them.
fn side(name: &str) -> Result<&'static str, CoverageError> {
    if name.ends_with("Request") {
        Ok("client")
    } else if name.ends_with("Event") || name.ends_with("Response") {
        Ok("server")
    } else {
        Err(CoverageError::Side(name.to_owned()))
    }
}

/// The catalogue checked against the code: no message twice, each with its side, every message decoded, encoded,
/// pinned or replayed in it, every measure on a build that pins it.
fn check(families: &[Family], builds: &[Build], decoded: &[&str], encoded: &[&str]) -> Result<(), CoverageError> {
    let mut listed = HashSet::new();
    for entry in families.iter().flat_map(|family| family.messages) {
        if !listed.insert(entry.name) {
            return Err(CoverageError::Twice(entry.name.to_owned()));
        }
        side(entry.name)?;
        for (build, _) in entry.measured {
            let pins = builds.iter().find(|known| known.name == *build);
            if !pins.is_some_and(|pins| pins.pinned.contains_key(entry.name)) {
                return Err(CoverageError::Measured {
                    name: entry.name.to_owned(),
                    build: (*build).to_owned(),
                });
            }
        }
    }
    let used = decoded
        .iter()
        .chain(encoded)
        .map(|name| (*name).to_owned())
        .chain(builds.iter().flat_map(|build| {
            let replayed = build.replayed.iter().flat_map(|replayed| replayed.messages.keys());
            build.pinned.keys().chain(replayed).cloned()
        }));
    for name in used {
        if !listed.contains(name.as_str()) {
            return Err(CoverageError::Missing(name));
        }
    }
    Ok(())
}

/// The report, from the catalogue, the builds' pins and replays, and the messages decoded and encoded.
fn report(families: &[Family], builds: &[Build], decoded: &[&str], encoded: &[&str]) -> Result<String, CoverageError> {
    check(families, builds, decoded, encoded)?;
    let total: usize = families.iter().map(|family| family.messages.len()).sum();
    let mut out = String::from(INTRODUCTION);
    out.push_str("\n| Build | Pinned | Decoded | Encoded | Replayed | Measured in game |\n");
    out.push_str("| --- | ---: | ---: | ---: | --- | ---: |\n");
    for build in builds {
        let entries = || families.iter().flat_map(|family| family.messages);
        let pinned = |name: &str| build.pinned.contains_key(name);
        let count = |names: &[&str]| names.iter().filter(|name| pinned(name)).count();
        let measured = entries()
            .filter(|entry| measured_on(entry, &build.name).is_some())
            .count();
        let replayed = build.replayed.as_ref().map_or_else(
            || "not replayed".to_owned(),
            |replayed| {
                format!(
                    "{} connections, {} frames",
                    thousands(replayed.connections),
                    thousands(replayed.frames)
                )
            },
        );
        let _ = writeln!(
            out,
            "| {} | {} of {total} | {} | {} | {replayed} | {measured} |",
            build.name,
            build.pinned.len(),
            count(decoded),
            count(encoded),
        );
    }
    for family in families {
        let _ = write!(out, "\n## {}\n\n| Message | Side |", family.name);
        for build in builds {
            let _ = write!(out, " {} |", build.name);
        }
        out.push_str(" Lua |\n| --- | --- |");
        out.push_str(&" --- |".repeat(builds.len() + 1));
        out.push('\n');
        for entry in family.messages {
            let _ = write!(out, "| {} | {} |", entry.name, side(entry.name)?);
            for build in builds {
                let _ = write!(out, " {} |", cell(entry, build, decoded, encoded));
            }
            let lua: Vec<String> = entry.lua.iter().map(|call| format!("`{call}`")).collect();
            let lua = if lua.is_empty() {
                "—".to_owned()
            } else {
                lua.join(", ")
            };
            let _ = writeln!(out, " {lua} |");
        }
    }
    Ok(out)
}

const INTRODUCTION: &str = "\
# Protocol coverage

<!-- Generated by `cargo test` (`protocol::coverage`): never edit it by hand. -->

What D3B reads and sends of the game connection, by family and by build (ADR 0021, [play.md](play.md)): every message
the C# version reads, from its `.proto` files, and those D3B pinned beyond them. Per build, a message shows its name in
that build once pinned, whether D3B decodes it and encodes it (the bot's requests, from step 4's F3), how many of its
frames the last local replay of the owner's captures read, and the checklist that measured it in game; then the
MoonBot Lua calls it backs. Two families of the C# version hold no frame of their own: actors (the shapes maps and
fights hold) and game (the envelope, which `protocol::envelope` reads); authentication, the ticket's exchange, is left
out: its request is a secret, never decoded.

To change it: the catalogue (`src-tauri/src/protocol/coverage/catalogue.rs`), the pins, the code, then `cargo test`;
CI fails when this page is stale. The replay's counts come from `src-tauri/protocol/replay/<build>.json`, which the
protocol's replay writes (`D3B_CAPTURES`, [protocol.md](protocol.md#tests)); CI does not check them.
";

/// The checklist that measured `entry` on `build`, if one did.
fn measured_on<'a>(entry: &'a catalogue::Entry, build: &str) -> Option<&'a str> {
    entry
        .measured
        .iter()
        .find(|(measured, _)| *measured == build)
        .map(|(_, checklist)| *checklist)
}

/// What one build does with one message: its name there, decoded, encoded, its frames replayed, its measure; `—` for
/// none of them.
fn cell(entry: &catalogue::Entry, build: &Build, decoded: &[&str], encoded: &[&str]) -> String {
    let mut parts = Vec::new();
    let is_decoded = decoded.contains(&entry.name);
    if let Some(name) = build.pinned.get(entry.name) {
        parts.push(format!("`{name}`"));
        if is_decoded {
            parts.push("decoded".to_owned());
        }
        if encoded.contains(&entry.name) {
            parts.push("encoded".to_owned());
        }
    } else if is_decoded {
        parts.push("decoded, not pinned".to_owned());
    }
    let tally = build
        .replayed
        .as_ref()
        .and_then(|replayed| replayed.messages.get(entry.name));
    if let Some(tally) = tally {
        let frames = tally.understood + tally.read;
        parts.push(format!(
            "{} frame{}",
            thousands(frames),
            if frames == 1 { "" } else { "s" }
        ));
        if tally.not_trusted > 0 {
            parts.push(format!("{} not trusted", thousands(tally.not_trusted)));
        }
    }
    if let Some(checklist) = measured_on(entry, &build.name) {
        parts.push(format!("[measured](checklists/{checklist})"));
    }
    if parts.is_empty() {
        "—".to_owned()
    } else {
        parts.join(", ")
    }
}

/// `value` with a comma between each group of three digits, as the docs write counts.
fn thousands(value: u64) -> String {
    let digits = value.to_string();
    let mut out = String::new();
    for (index, digit) in digits.chars().enumerate() {
        if index > 0 && (digits.len() - index).is_multiple_of(3) {
            out.push(',');
        }
        out.push(digit);
    }
    out
}

#[cfg(test)]
mod tests;
