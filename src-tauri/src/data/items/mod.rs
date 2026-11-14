//! The items, their types, the jobs and their skills: what a character carries, and what each job's skills gather.
//!
//! The client's `ItemData` and `WeaponData`, `ItemTypeData`, `JobData` and `SkillData` (verified on 3.7.13.17's data,
//! `artifacts/studies/items.md`). The extractor leaves out an item of a type the client lacks and a skill
//! gathering an item the client lacks (the owner, 2026-11-14), so the app refuses a folder that names either. Each
//! item and job has its icon, but four items of 3.7.13.17 whose icon the client lacks.

mod icons;
mod jobs;

use std::collections::HashSet;
use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::manifest::Manifest;
use super::text::Text;
use icons::Icons;
pub use jobs::{Job, Skill};

/// An item, anything a character can carry.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Item {
    pub id: i32,
    pub name: Text,
    pub type_id: i32,
    pub level: u8,
    /// Its icon's number; 0 on 3,130 items, 3,128 of them of type 203, the invisible items (verified on 3.7.13.17,
    /// `artifacts/studies/items.md`).
    pub icon_id: i32,
}

/// A type of item, such as wood.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ItemType {
    pub id: i32,
    pub name: Text,
    /// The inventory's tab its items are shown in, 0 to 5 on 3.7.13.17 (inferred from its types' names only,
    /// `artifacts/studies/items.md`).
    pub category_id: i32,
}

/// The items, their types, the jobs and their skills of a data folder, each in order of id, and which items' icons it
/// holds.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ItemTables {
    pub items: Vec<Item>,
    pub types: Vec<ItemType>,
    pub jobs: Vec<Job>,
    pub skills: Vec<Skill>,
    /// The icons of `icons/items/` the folder holds, whose files [`ItemTables::item_icon`] gives.
    pub item_icons: HashSet<i32>,
}

impl ItemTables {
    /// Reads the four tables that `manifest` lists in `folder`, refusing them when an id is held twice, an item's
    /// type is missing, a skill's job or gathered item is, or a job's icon is.
    pub fn load(folder: &Path, manifest: &Manifest) -> Result<Self, DataError> {
        let tables = Self {
            items: manifest.read(folder, "items")?,
            types: manifest.read(folder, "item-types")?,
            jobs: manifest.read(folder, "jobs")?,
            skills: manifest.read(folder, "skills")?,
            item_icons: icons::listed(manifest, Icons::Items),
        }
        .checked()?;
        icons::check(&tables.jobs, manifest)?;
        Ok(tables)
    }

    fn checked(self) -> Result<Self, DataError> {
        let types = unique("item-types", self.types.iter().map(|kind| kind.id))?;
        let items = unique("items", self.items.iter().map(|item| item.id))?;
        if let Some(item) = self.items.iter().find(|item| !types.contains(&item.type_id)) {
            return Err(DataError::Reference {
                record: format!("item {}", item.id),
                field: "typeId",
                target: item.type_id,
            });
        }
        let jobs = unique("jobs", self.jobs.iter().map(|job| job.id))?;
        unique("skills", self.skills.iter().map(|skill| skill.id))?;
        jobs::check(&self.skills, &jobs, &items)?;
        Ok(self)
    }
}

/// The ids of a table, refused when one is held twice.
fn unique(table: &'static str, ids: impl Iterator<Item = i32>) -> Result<HashSet<i32>, DataError> {
    let mut seen = HashSet::new();
    for id in ids {
        if !seen.insert(id) {
            return Err(DataError::Duplicate { table, id });
        }
    }
    Ok(seen)
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Items/).
    const ITEMS: &str = include_str!("../../../../tools/extractor/tests/fixtures/items/items.json");
    const TYPES: &str = include_str!("../../../../tools/extractor/tests/fixtures/items/item-types.json");
    const JOBS: &str = include_str!("../../../../tools/extractor/tests/fixtures/items/jobs.json");
    const SKILLS: &str = include_str!("../../../../tools/extractor/tests/fixtures/items/skills.json");

    fn read<T: serde::de::DeserializeOwned>(text: &str) -> Vec<T> {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    /// The fixtures' four tables, read but not checked.
    pub(super) fn fixtures() -> ItemTables {
        ItemTables {
            items: read(ITEMS),
            types: read(TYPES),
            jobs: read(JOBS),
            skills: read(SKILLS),
            item_icons: HashSet::new(),
        }
    }

    #[test]
    fn reads_the_items_and_types_the_extractor_writes() {
        let tables = fixtures().checked().unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(tables.items[1].name.fr, "Bois");
        assert_eq!((tables.items[0].type_id, tables.items[0].level), (34, 1));
        assert_eq!(tables.types[0].name.en, "Cereal");
        assert_eq!(tables.types[0].category_id, 2);
    }

    #[test]
    fn refuses_an_item_of_a_type_the_folder_lacks() {
        let mut tables = fixtures();
        tables.types.remove(0);
        assert!(matches!(
            tables.checked(),
            Err(DataError::Reference { record, field: "typeId", target: 34 }) if record == "item 289"
        ));
    }

    #[test]
    fn refuses_an_id_held_twice_in_any_of_the_four_tables() {
        type Change = fn(&mut ItemTables);
        let twice: [(&str, i32, Change); 4] = [
            ("items", 289, |tables| tables.items[1].id = 289),
            ("item-types", 34, |tables| tables.types[1].id = 34),
            ("jobs", 1, |tables| tables.jobs[1].id = 1),
            ("skills", 6, |tables| tables.skills[1].id = 6),
        ];
        for (table, id, change) in twice {
            let mut tables = fixtures();
            change(&mut tables);
            assert!(
                matches!(tables.checked(), Err(DataError::Duplicate { table: found, id: held }) if found == table && held == id),
                "{table}"
            );
        }
    }

    #[test]
    fn loads_a_folder_only_when_its_references_hold() {
        let folder = std::env::temp_dir().join(format!("d3b-data-test-items-{}", std::process::id()));
        std::fs::create_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        let files = [
            ("items", ITEMS),
            ("item-types", TYPES),
            ("jobs", JOBS),
            ("skills", SKILLS),
        ];
        let mut tables: Vec<String> = files
            .iter()
            .map(|(name, _)| format!(r#"{{"name":"{name}","path":"{name}.json","sha256":"00"}}"#))
            .collect();
        // The lumberjack's icon; Base has none.
        tables.push(r#"{"name":"icons/jobs/1","path":"icons/jobs/1.png","sha256":"00"}"#.to_owned());
        let manifest = format!(
            r#"{{"format":{},"build":"0","extractor":"0","tables":[{}]}}"#,
            super::super::FORMAT,
            tables.join(",")
        );
        let manifest = Manifest::parse(&manifest).unwrap_or_else(|error| panic!("{error}"));
        let write = |items: &str| {
            for (name, text) in files {
                let text = if name == "items" { items } else { text };
                std::fs::write(folder.join(format!("{name}.json")), text).unwrap_or_else(|error| panic!("{error}"));
            }
            ItemTables::load(&folder, &manifest)
        };
        let whole = write(ITEMS);
        let broken = write(&ITEMS.replace(r#""typeId":34"#, r#""typeId":264"#));
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(whole.map(|tables| tables.items.len()).ok(), Some(2));
        assert!(matches!(broken, Err(DataError::Reference { target: 264, .. })));
    }

    #[test]
    fn refuses_an_unknown_field() {
        let text = ITEMS.replace("\"iconId\"", "\"price\":1,\"iconId\"");
        assert!(serde_json::from_str::<Vec<Item>>(&text).is_err());
    }
}
