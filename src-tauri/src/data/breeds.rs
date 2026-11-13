use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;
use super::text::Text;

/// A breed, the character's class, such as the Iop (`BreedData`, verified on 3.7.13.17's data,
/// `artifacts/studies/breeds.md`). The client draws its head for each sex, which the folder holds as
/// `heads/<id>-<sex>.png`.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Breed {
    pub id: i32,
    pub name: Text,
}

/// The sexes a head is drawn for, 0 the male and 1 the female, as the heads' names and the character's look hold them
/// (verified on 3.7.13.17's data and the owner's captures, `artifacts/studies/breeds.md`).
const SEXES: [u8; 2] = [0, 1];

/// The name, in the manifest, of a breed's head for a sex (0 the male, 1 the female).
pub fn head_name(breed: i32, sex: u8) -> String {
    format!("heads/{breed}-{sex}")
}

/// Reads the breeds that `manifest` lists in `folder`, refusing them when a breed lacks a head.
pub fn load(folder: &Path, manifest: &Manifest) -> Result<Vec<Breed>, DataError> {
    let breeds: Vec<Breed> = manifest.read(folder, "breeds")?;
    for breed in &breeds {
        for sex in SEXES {
            let name = head_name(breed.id, sex);
            if !manifest.tables.iter().any(|table| table.name == name) {
                return Err(DataError::MissingTable(name));
            }
        }
    }
    Ok(breeds)
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write this file byte for byte (tools/extractor/tests/Breeds/BreedTablesTests.cs).
    const BREEDS: &str = include_str!("../../../tools/extractor/tests/fixtures/breeds/breeds.json");

    #[test]
    fn reads_the_breeds_the_extractor_writes() {
        let breeds: Vec<Breed> = serde_json::from_str(BREEDS).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(breeds.len(), 2);
        assert_eq!((breeds[1].id, breeds[1].name.fr.as_str()), (9, "Crâ"));
        assert_eq!(breeds[1].name.es, "Ocra");
    }

    #[test]
    fn names_a_head_by_its_breed_and_sex() {
        assert_eq!(head_name(8, 0), "heads/8-0");
        assert_eq!(head_name(20, 1), "heads/20-1");
    }

    fn folder_with(heads: &[&str]) -> (std::path::PathBuf, Manifest) {
        let folder = std::env::temp_dir().join(format!("d3b-data-test-breeds-{}-{}", std::process::id(), heads.len()));
        std::fs::create_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(folder.join("breeds.json"), BREEDS).unwrap_or_else(|error| panic!("{error}"));
        let mut tables = vec![r#"{"name":"breeds","path":"breeds.json","sha256":"00"}"#.to_owned()];
        tables.extend(
            heads
                .iter()
                .map(|head| format!(r#"{{"name":"{head}","path":"{head}.png","sha256":"00"}}"#)),
        );
        let manifest = format!(
            r#"{{"format":{},"build":"3.7.13.17","extractor":"0","tables":[{}]}}"#,
            super::super::FORMAT,
            tables.join(",")
        );
        let manifest = Manifest::parse(&manifest).unwrap_or_else(|error| panic!("{error}"));
        (folder, manifest)
    }

    #[test]
    fn reads_breeds_whose_heads_the_folder_holds() {
        let (folder, manifest) = folder_with(&["heads/8-0", "heads/8-1", "heads/9-0", "heads/9-1"]);
        let breeds = load(&folder, &manifest);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(breeds.map(|breeds| breeds.len()).ok(), Some(2));
    }

    #[test]
    fn refuses_a_breed_without_its_female_head() {
        let (folder, manifest) = folder_with(&["heads/8-0", "heads/8-1", "heads/9-0"]);
        let breeds = load(&folder, &manifest);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(breeds, Err(DataError::MissingTable(name)) if name == "heads/9-1"));
    }
}
