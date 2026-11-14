use std::path::{Path, PathBuf};

use serde::Deserialize;
use serde::de::DeserializeOwned;

/// What a data folder holds, as the extractor describes it.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Manifest {
    /// The folder's format; it must equal [`super::FORMAT`].
    pub format: u32,
    /// The client build the data was extracted from, such as `3.7.13.17`.
    pub build: String,
    /// The extractor's version, for bug reports.
    pub extractor: String,
    /// Every table of the folder.
    pub tables: Vec<Table>,
}

/// One file of a data folder: a JSON table, a shard of one, or a world map tile.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Table {
    /// Its path without the extension, such as `areas` or `maps/37`; unique in the folder.
    pub name: String,
    /// Its path, relative to the folder, with forward slashes.
    pub path: String,
    /// The SHA-256 of the file, in lowercase hexadecimal.
    pub sha256: String,
}

/// Why a data folder cannot be read.
#[derive(Debug, thiserror::Error)]
pub enum DataError {
    #[error("no data folder at {0}")]
    Missing(PathBuf),
    #[error("cannot read {path}: {source}")]
    Io {
        path: PathBuf,
        #[source]
        source: std::io::Error,
    },
    #[error("{path} is not a valid manifest: {source}")]
    Invalid {
        path: PathBuf,
        #[source]
        source: serde_json::Error,
    },
    #[error("the data is in format {found}, this version of D3B reads format {expected}")]
    Format { found: u32, expected: u32 },
    #[error("the manifest lists no table {0}")]
    MissingTable(String),
    #[error("map {map} has {found} cells, where every map has {expected}")]
    Cells { map: u32, found: usize, expected: usize },
    #[error("{path} is not a valid table: {source}")]
    Table {
        path: PathBuf,
        #[source]
        source: serde_json::Error,
    },
    #[error("{table} holds {id} twice")]
    Duplicate { table: &'static str, id: i32 },
    #[error("{record} names {field} {target}, which the data does not hold")]
    Reference {
        record: String,
        field: &'static str,
        target: i32,
    },
}

impl Manifest {
    /// The manifest's file name inside a data folder.
    pub const FILE: &'static str = "manifest.json";

    /// Reads and checks the manifest of the data folder `folder`.
    pub fn load(folder: &Path) -> Result<Self, DataError> {
        let path = folder.join(Self::FILE);
        if !path.is_file() {
            return Err(DataError::Missing(folder.to_path_buf()));
        }
        let text = std::fs::read_to_string(&path).map_err(|source| DataError::Io {
            path: path.clone(),
            source,
        })?;
        Self::parse(&text).map_err(|error| match error {
            DataError::Invalid { source, .. } => DataError::Invalid { path, source },
            other => other,
        })
    }

    /// Reads the table `name` of the data folder `folder`, as the manifest lists it.
    pub fn read<T: DeserializeOwned>(&self, folder: &Path, name: &str) -> Result<Vec<T>, DataError> {
        let table = self
            .tables
            .iter()
            .find(|table| table.name == name)
            .ok_or_else(|| DataError::MissingTable(name.to_owned()))?;
        let path = folder.join(&table.path);
        let text = std::fs::read_to_string(&path).map_err(|source| DataError::Io {
            path: path.clone(),
            source,
        })?;
        serde_json::from_str(&text).map_err(|source| DataError::Table { path, source })
    }

    /// Parses and checks a manifest's text.
    pub fn parse(text: &str) -> Result<Self, DataError> {
        let manifest: Self = serde_json::from_str(text).map_err(|source| DataError::Invalid {
            path: PathBuf::from(Self::FILE),
            source,
        })?;
        if manifest.format != super::FORMAT {
            return Err(DataError::Format {
                found: manifest.format,
                expected: super::FORMAT,
            });
        }
        Ok(manifest)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const VALID: &str = r#"{
        "format": 3,
        "build": "3.7.13.17",
        "extractor": "0.1.0",
        "tables": [{ "name": "items", "path": "items.json", "sha256": "00" }]
    }"#;

    #[test]
    fn reads_a_valid_manifest() {
        let manifest = Manifest::parse(VALID).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(manifest.build, "3.7.13.17");
        assert_eq!(manifest.tables.len(), 1);
    }

    #[test]
    fn reads_what_the_extractor_writes() {
        // The extractor's own tests write this file byte for byte (tools/extractor/tests/DataFolderTests.cs).
        let text = include_str!("../../../tools/extractor/tests/fixtures/empty/manifest.json");
        let manifest = Manifest::parse(text).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(manifest.build, "3.7.13.17");
        assert_eq!(manifest.tables, Vec::<Table>::new());
    }

    #[test]
    fn refuses_another_format() {
        let text = VALID.replace("\"format\": 3", "\"format\": 2");
        assert!(matches!(
            Manifest::parse(&text),
            Err(DataError::Format { found: 2, expected: 3 })
        ));
    }

    #[test]
    fn refuses_an_unknown_field() {
        let text = VALID.replace("\"build\"", "\"unknown\": 0, \"build\"");
        assert!(matches!(Manifest::parse(&text), Err(DataError::Invalid { .. })));
    }

    #[test]
    fn says_which_table_the_manifest_lacks() {
        let manifest = Manifest::parse(VALID).unwrap_or_else(|error| panic!("{error}"));
        let folder = std::env::temp_dir();
        assert!(matches!(
            manifest.read::<u32>(&folder, "zaaps"),
            Err(DataError::MissingTable(name)) if name == "zaaps"
        ));
    }

    #[test]
    fn says_which_listed_table_is_missing_or_unreadable() {
        let folder = std::env::temp_dir().join(format!("d3b-data-test-tables-{}", std::process::id()));
        std::fs::create_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(folder.join("items.json"), "{").unwrap_or_else(|error| panic!("{error}"));
        let manifest = Manifest::parse(VALID).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(
            manifest.read::<u32>(&folder, "items"),
            Err(DataError::Table { .. })
        ));
        std::fs::remove_file(folder.join("items.json")).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(
            manifest.read::<u32>(&folder, "items"),
            Err(DataError::Io { .. })
        ));
        std::fs::remove_dir(&folder).unwrap_or_else(|error| panic!("{error}"));
    }

    #[test]
    fn says_when_the_folder_has_no_manifest() {
        let folder = std::env::temp_dir().join("d3b-data-test-no-manifest");
        assert!(matches!(Manifest::load(&folder), Err(DataError::Missing(_))));
    }
}
