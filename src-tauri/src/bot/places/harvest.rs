use std::collections::HashMap;

use crate::data::{ItemTables, SubArea};

use super::{Row, Scored, ranges, richest, worth};

/// A profession's ranges: at each of its levels, the sub-areas whose resources of that profession are worth most,
/// each resource a sub-area lists counted once, at its worth for the profession's level (the owner, 2026-11-17: the
/// data says which resources a sub-area holds, not on how many maps; the maps come with the graphics learned, step 5).
/// Only a sub-area `shown` gives a map is taken ([`super::shown`]).
pub(crate) fn harvest(sub_areas: &[SubArea], items: &ItemTables, shown: &HashMap<i32, usize>, job: i32) -> Vec<Row> {
    let held: Vec<(&SubArea, Vec<u16>)> = sub_areas
        .iter()
        .filter(|sub_area| shown.get(&sub_area.id).is_some_and(|&maps| maps > 0))
        .map(|sub_area| {
            let levels = items
                .resources(&sub_area.harvestables)
                .into_iter()
                .filter(|resource| resource.job.id == job)
                .map(|resource| u16::from(resource.item.level))
                .collect();
            (sub_area, levels)
        })
        .filter(|(_, levels): &(&SubArea, Vec<u16>)| !levels.is_empty())
        .collect();
    ranges(|level| {
        let scored = held.iter().map(|(sub_area, levels)| Scored {
            id: sub_area.id,
            level: sub_area.level,
            worth: levels.iter().map(|&resource| worth::resource(level, resource)).sum(),
        });
        richest(scored, level)
    })
}

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use super::super::tests::{maps, sub_area};
    use super::*;

    fn text(name: &str) -> serde_json::Value {
        serde_json::json!({"fr": name, "en": name, "de": name, "es": name, "pt": name})
    }

    /// A lumberjack (2) cutting ash (303, level 1) and chestnut (473, level 50), an alchemist (26) picking nettles
    /// (421, level 1), and a loot box (8000) no skill gathers.
    fn items() -> ItemTables {
        let item = |id: i32, level: u8| {
            serde_json::from_value(
                serde_json::json!({"id": id, "name": text("x"), "typeId": 1, "level": level, "iconId": 0}),
            )
            .unwrap_or_else(|error| panic!("{error}"))
        };
        let job = |id: i32| {
            serde_json::from_value(serde_json::json!({"id": id, "name": text("x"), "iconId": 1}))
                .unwrap_or_else(|error| panic!("{error}"))
        };
        let skill = |id: i32, job: i32, item: i32, level: i32| {
            serde_json::from_value(serde_json::json!({"id": id, "name": text("x"), "parentJobId": job,
                "gatheredItemId": item, "levelMin": level, "range": 1, "clientDisplay": true}))
            .unwrap_or_else(|error| panic!("{error}"))
        };
        ItemTables {
            items: vec![item(303, 1), item(421, 1), item(473, 50), item(8000, 1)],
            types: Vec::new(),
            jobs: vec![job(2), job(26)],
            skills: vec![skill(6, 2, 303, 1), skill(10, 2, 473, 50), skill(68, 26, 421, 1)],
            item_icons: HashSet::new(),
        }
    }

    #[test]
    fn a_profession_goes_where_its_resources_are_worth_most_at_its_level() {
        let sub_areas = [
            sub_area(97, 10, &[303, 8000], &[], true),
            sub_area(98, 30, &[473], &[], true),
            sub_area(99, 1, &[421], &[], true),
        ];
        let rows = harvest(&sub_areas, &items(), &all_shown(&sub_areas), 2);
        let row = |from, to, sub_areas: &[i32]| Row {
            from,
            to,
            sub_areas: sub_areas.to_vec(),
        };
        // Ash alone below 50; from 50 chestnut is worth 1, ash 2^(-49/20), under a quarter of it.
        assert_eq!(rows, [row(1, 49, &[97]), row(50, 200, &[98])]);
    }

    #[test]
    fn a_profession_without_a_resource_anywhere_has_no_row() {
        let sub_areas = [sub_area(97, 10, &[303, 8000], &[], true)];
        assert_eq!(harvest(&sub_areas, &items(), &all_shown(&sub_areas), 26), []);
        assert_eq!(harvest(&sub_areas, &items(), &all_shown(&sub_areas), 24), []);
    }

    #[test]
    fn a_sub_area_without_a_map_shown_is_never_taken() {
        // A convention's test zone listing every resource, its maps on no world map (1021 on 3.7.13.17).
        let sub_areas = [
            sub_area(97, 10, &[303], &[], true),
            sub_area(1021, 20, &[303, 473], &[], false),
        ];
        let positions = [maps(100, 1, 97, 1), maps(200, 10, 1021, -1)].concat();
        let graph: HashSet<i64> = positions.iter().map(|position| i64::from(position.id)).collect();
        let rows = harvest(&sub_areas, &items(), &super::super::shown(&positions, &graph), 2);
        assert_eq!(
            rows,
            [Row {
                from: 1,
                to: 200,
                sub_areas: vec![97]
            }]
        );
    }

    fn all_shown(sub_areas: &[SubArea]) -> HashMap<i32, usize> {
        sub_areas.iter().map(|sub_area| (sub_area.id, 1)).collect()
    }
}
