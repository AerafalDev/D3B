//! Items found by their name, for the editor's item field (the owner, 2026-11-17: every item but the invisible ones,
//! searched by name, accents and case aside).

use super::{Item, ItemTables};

/// The type of the invisible items, which no player chooses (verified on 3.7.13.17, `artifacts/studies/items.md`).
pub const INVISIBLE: i32 = 203;

/// The most items one search gives: enough to choose from, few enough to draw at once.
pub const LIMIT: usize = 50;

impl ItemTables {
    /// Up to [`LIMIT`] items whose French or English name holds `query`, accents and case aside, the invisible ones
    /// left out: those whose name starts with it first, then by level, then by id; none for a blank query.
    pub fn find(&self, query: &str) -> Vec<&Item> {
        let wanted = folded(query.trim());
        if wanted.is_empty() {
            return Vec::new();
        }
        let mut found: Vec<(bool, &Item)> = self
            .items
            .iter()
            .filter(|item| item.type_id != INVISIBLE)
            .filter_map(|item| {
                let names = [folded(&item.name.fr), folded(&item.name.en)];
                let starts = names.iter().any(|name| name.starts_with(&wanted));
                (starts || names.iter().any(|name| name.contains(&wanted))).then_some((starts, item))
            })
            .collect();
        found.sort_by_key(|(starts, item)| (!starts, item.level, item.id));
        found.into_iter().take(LIMIT).map(|(_, item)| item).collect()
    }

    /// The items of `ids` this data holds, in that order: what a value already chosen names.
    pub fn by_ids(&self, ids: &[i32]) -> Vec<&Item> {
        // The tables are in order of id (the extractor writes them so).
        let one = |id: &i32| self.items.binary_search_by_key(id, |item| item.id).ok();
        ids.iter().filter_map(one).map(|at| &self.items[at]).collect()
    }
}

/// `text` as a search compares it: lowercase, its accents dropped.
fn folded(text: &str) -> String {
    text.chars()
        .flat_map(char::to_lowercase)
        .map(|letter| match letter {
            'à' | 'â' | 'ä' | 'á' | 'ã' => 'a',
            'é' | 'è' | 'ê' | 'ë' => 'e',
            'î' | 'ï' | 'í' => 'i',
            'ô' | 'ö' | 'ó' | 'õ' => 'o',
            'ù' | 'û' | 'ü' | 'ú' => 'u',
            'ç' => 'c',
            'ñ' => 'n',
            'ÿ' => 'y',
            other => other,
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::data::items::tests::fixtures;
    use crate::data::text::Text;

    fn item(id: i32, fr: &str, type_id: i32, level: u8) -> Item {
        let name = Text {
            fr: fr.into(),
            en: fr.into(),
            de: String::new(),
            es: String::new(),
            pt: String::new(),
        };
        Item {
            id,
            name,
            type_id,
            level,
            icon_id: 1,
        }
    }

    fn tables() -> ItemTables {
        let mut tables = fixtures();
        // Blé (289) and Bois (303) are the fixtures'; in order of id, as the extractor writes them.
        tables.items.extend([
            item(400, "Graine de blé", 34, 5),
            item(401, "Bléchette invisible", INVISIBLE, 1),
            item(402, "Pain au BLE", 34, 1),
        ]);
        tables
    }

    fn ids(found: &[&Item]) -> Vec<i32> {
        found.iter().map(|item| item.id).collect()
    }

    #[test]
    fn a_name_is_found_by_any_part_of_it_accents_and_case_aside_those_starting_with_it_first() {
        let tables = tables();
        assert_eq!(ids(&tables.find("ble")), [289, 402, 400]);
        assert_eq!(ids(&tables.find("  WHEAT ")), [289], "the English name too");
        assert_eq!(ids(&tables.find("graine")), [400]);
    }

    #[test]
    fn the_invisible_items_a_blank_query_and_what_no_name_holds_find_nothing() {
        let tables = tables();
        assert!(!ids(&tables.find("bléchette")).contains(&401));
        assert!(tables.find("  ").is_empty() && tables.find("épée").is_empty());
    }

    #[test]
    fn a_search_gives_at_most_its_limit() {
        let mut tables = fixtures();
        tables.items = (0..80).map(|id| item(1000 + id, "Fleur", 34, 1)).collect();
        assert_eq!(tables.find("fleur").len(), LIMIT);
    }

    #[test]
    fn items_are_given_back_by_their_ids_in_that_order_those_held() {
        let tables = tables();
        assert_eq!(ids(&tables.by_ids(&[402, 289, 9999])), [402, 289]);
    }
}
