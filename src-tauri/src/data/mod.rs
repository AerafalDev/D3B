//! The game data packed in the app.
//!
//! The .NET extractor (`tools/extractor`) writes one folder per client build: a `manifest.json` and one JSON file per
//! table, world map tiles included. A `client-<build>` release asset carries that folder; the release workflow puts it
//! in `data/`, which Tauri bundles as a resource. Nothing extracted from the client is ever committed (`docs/data.md`).

mod manifest;

pub use manifest::Manifest;

/// The format of the folder this version of the code reads. The extractor writes the same number in the manifest; a
/// different one is refused rather than half read.
pub const FORMAT: u32 = 1;
