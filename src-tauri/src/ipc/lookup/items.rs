//! Items found for the editor's item field (the owner, 2026-11-17): every item but the invisible ones, searched by name
//! in Rust, each with its icon, level and type; and the items a value already names, by their ids.

use std::path::Path;

use serde::Serialize;
use specta::Type;
use tauri::Manager;

use super::super::atlas::AtlasError;
use super::super::character::Names;
use super::super::commands::LoadedData;
use crate::data::{Item, ItemTables};

/// An item as the field shows it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct ItemView {
    pub id: i32,
    pub name: Names,
    pub level: u8,
    /// Its type's name, such as "Céréale".
    pub kind: Names,
    /// The file of its icon, in the data folder; `None` when the folder lacks it.
    pub icon: Option<String>,
}

fn names(text: &crate::data::Text) -> Names {
    Names {
        fr: text.fr.clone(),
        en: text.en.clone(),
    }
}

/// `items` as the field shows them, their icons in `folder`.
fn views(tables: &ItemTables, items: Vec<&Item>, folder: &Path) -> Vec<ItemView> {
    let kind = |item: &Item| tables.types.iter().find(|kind| kind.id == item.type_id);
    let views = items.into_iter().map(|item| ItemView {
        id: item.id,
        name: names(&item.name),
        level: item.level,
        kind: kind(item).map_or_else(
            || Names {
                fr: String::new(),
                en: String::new(),
            },
            |it| names(&it.name),
        ),
        icon: tables.item_icon(item).map(|file| {
            file.split('/')
                .fold(folder.to_path_buf(), |path, part| path.join(part))
                .to_string_lossy()
                .into_owned()
        }),
    });
    views.collect()
}

/// The items whose name holds `query`, or, when `ids` are given, those items: what a value already chosen names.
fn found(tables: &ItemTables, folder: &Path, query: &str, ids: &[i32]) -> Vec<ItemView> {
    let items = if ids.is_empty() {
        tables.find(query)
    } else {
        tables.by_ids(ids)
    };
    views(tables, items, folder)
}

/// Up to 50 items whose French or English name holds `query`, accents and case aside, the invisible ones left out;
/// or, when `ids` are given, those items, in that order; or why the game data is not there.
#[tauri::command]
#[specta::specta]
pub async fn find_items(app: tauri::AppHandle, query: String, ids: Vec<i32>) -> Result<Vec<ItemView>, AtlasError> {
    let data = app.state::<LoadedData>();
    match (&data.0, data.1.as_deref()) {
        (Ok(game), Some(folder)) => Ok(found(&game.items, folder, &query, &ids)),
        (Err(reason), _) => Err(AtlasError::NoData { reason: reason.clone() }),
        (Ok(_), None) => Err(AtlasError::NoData {
            reason: "the game data's folder is unknown".into(),
        }),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn read<T: serde::de::DeserializeOwned>(text: &str) -> Vec<T> {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn tables() -> ItemTables {
        ItemTables {
            items: read(include_str!(
                "../../../../tools/extractor/tests/fixtures/items/items.json"
            )),
            types: read(include_str!(
                "../../../../tools/extractor/tests/fixtures/items/item-types.json"
            )),
            jobs: Vec::new(),
            skills: Vec::new(),
            item_icons: [42011].into(),
        }
    }

    #[test]
    fn an_item_found_by_name_comes_with_its_type_level_and_icon_in_the_folder() {
        let found = found(&tables(), Path::new("data"), "ble", &[]);
        assert_eq!(found.len(), 1);
        let wheat = &found[0];
        assert_eq!((wheat.id, wheat.name.fr.as_str(), wheat.level), (289, "Blé", 1));
        assert!(!wheat.kind.fr.is_empty(), "its type named");
        let icon = wheat.icon.as_deref().unwrap_or_default();
        assert!(icon.starts_with("data") && icon.contains("42011"), "{icon}");
    }

    #[test]
    fn items_asked_by_id_come_back_and_one_without_its_icon_has_none() {
        let found = found(&tables(), Path::new("data"), "", &[303, 289]);
        assert_eq!(found.iter().map(|it| it.id).collect::<Vec<_>>(), [303, 289]);
        assert_eq!(found[0].icon, None, "Bois has no icon in the folder");
    }
}
