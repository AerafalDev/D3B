use std::path::{Path, PathBuf};

use serde::Deserialize;

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

/// One JSON file of a data folder.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Table {
    /// The table's name, such as `items`.
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
        "format": 1,
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
    fn refuses_another_format() {
        let text = VALID.replace("\"format\": 1", "\"format\": 2");
        assert!(matches!(
            Manifest::parse(&text),
            Err(DataError::Format { found: 2, expected: 1 })
        ));
    }

    #[test]
    fn refuses_an_unknown_field() {
        let text = VALID.replace("\"build\"", "\"unknown\": 0, \"build\"");
        assert!(matches!(Manifest::parse(&text), Err(DataError::Invalid { .. })));
    }

    #[test]
    fn says_when_the_folder_has_no_manifest() {
        let folder = std::env::temp_dir().join("d3b-data-test-no-manifest");
        assert!(matches!(Manifest::load(&folder), Err(DataError::Missing(_))));
    }
}
