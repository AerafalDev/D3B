use std::collections::{BTreeSet, HashMap};

use crate::data::{Monster, SubArea};

use super::{Row, Scored, ranges, richest, worth};

/// The least maps a sub-area draws on a world map, among those the world graph holds, for Combat to go there by
/// default, so that a tour finds groups and does not land in a dungeon (the C# version's D245, measured in game).
pub(crate) const LEAST_MAPS_SHOWN: usize = 10;

/// Combat's ranges: at each of the character's levels, the sub-areas not above it whose monsters are worth most, each
/// monster a sub-area lists counted once. Only those drawing at least [`LEAST_MAPS_SHOWN`] maps on a world map
/// (`shown`, [`super::shown`]) are taken, unless none of them holds a monster worth fighting at that level: then those
/// drawing at least one are (the C# version measured none below level 5 on 3.7.12.12, D245).
pub(crate) fn combat(sub_areas: &[SubArea], monsters: &[Monster], shown: &HashMap<i32, usize>) -> Vec<Row> {
    let levels: HashMap<i32, &[u16]> = monsters
        .iter()
        .map(|monster| (monster.id, monster.levels.as_slice()))
        .collect();
    let living: Vec<(&SubArea, Vec<&[u16]>, bool)> = sub_areas
        .iter()
        .filter_map(|sub_area| {
            // In order of id, so that the worths add up the same way on every run and the ranges never differ.
            let kinds: BTreeSet<i32> = sub_area.monsters.iter().copied().collect();
            let grades: Vec<&[u16]> = kinds.iter().filter_map(|id| levels.get(id).copied()).collect();
            let maps = shown.get(&sub_area.id).copied().unwrap_or_default();
            (!grades.is_empty() && maps > 0).then_some((sub_area, grades, maps >= LEAST_MAPS_SHOWN))
        })
        .collect();
    ranges(|level| {
        let scored = |wide: bool| {
            living
                .iter()
                .filter(move |(sub_area, _, shown)| *shown == wide && sub_area.level <= u32::from(level))
                .map(move |(sub_area, grades, _)| Scored {
                    id: sub_area.id,
                    level: sub_area.level,
                    worth: grades.iter().map(|grades| worth::monster(level, grades)).sum(),
                })
        };
        let wide = richest(scored(true), level);
        if wide.is_empty() {
            richest(scored(false), level)
        } else {
            wide
        }
    })
}

#[cfg(test)]
mod tests {
    use super::super::shown;
    use super::super::tests::{maps, sub_area};
    use super::*;
    use std::collections::HashSet;

    fn monster(id: i32, levels: &[u16]) -> Monster {
        let name = serde_json::json!({"fr": "x", "en": "x", "de": "x", "es": "x", "pt": "x"});
        serde_json::from_value(
            serde_json::json!({"id": id, "name": name, "raceId": 1, "flags": [], "levels": levels, "drops": []}),
        )
        .unwrap_or_else(|error| panic!("{error}"))
    }

    fn row(from: u16, to: u16, sub_areas: &[i32]) -> Row {
        Row {
            from,
            to,
            sub_areas: sub_areas.to_vec(),
        }
    }

    fn all_reached(positions: &[crate::data::MapPosition]) -> HashSet<i64> {
        positions.iter().map(|position| i64::from(position.id)).collect()
    }

    #[test]
    fn combat_goes_where_the_monsters_are_worth_most_and_never_above_the_character() {
        // Larvae of 1 to 5 in a field of level 1 and in a cave of level 30; Bouftous of 30 to 34 in a field of level 30;
        // all of 12 maps shown.
        let sub_areas = [
            sub_area(1, 1, &[], &[31, 31], true),
            sub_area(2, 30, &[], &[101, 102], true),
            sub_area(3, 30, &[], &[31], true),
        ];
        let monsters = [
            monster(31, &[1, 2, 3, 4, 5]),
            monster(101, &[30, 34]),
            monster(102, &[31, 33]),
        ];
        let positions = [maps(100, 12, 1, 1), maps(200, 12, 2, 1), maps(300, 12, 3, 1)].concat();
        let rows = combat(&sub_areas, &monsters, &shown(&positions, &all_reached(&positions)));
        // Below 30 both sub-areas of level 30 are above the character, the cave's larvae worth fighting all the same. At
        // 30 only the Bouftou of 30 counts, 102's lowest grade being above, and the larvae, 2^(-25/20) from their grade
        // 5, are worth more than a quarter of it, the cave's first, its level the nearest; from 31 the two Bouftous are
        // worth 2 and the larvae less than a quarter of them.
        assert_eq!(rows, [row(1, 29, &[1]), row(30, 30, &[2, 3, 1]), row(31, 200, &[2])]);
    }

    #[test]
    fn a_sub_area_of_few_maps_shown_is_taken_only_when_no_wider_one_holds_a_monster_worth_fighting() {
        // A dungeon of 30 maps on no world map, never taken; a cave of 3 maps shown; a field of 14 maps shown from 5.
        let sub_areas = [
            sub_area(1, 1, &[], &[31], true),
            sub_area(2, 1, &[], &[31], true),
            sub_area(3, 5, &[], &[40], true),
        ];
        let monsters = [monster(31, &[1, 3]), monster(40, &[5, 8])];
        let positions = [
            maps(100, 30, 1, -1),
            maps(200, 3, 2, 1),
            maps(300, 9, 3, 1),
            maps(400, 5, 3, 1),
        ]
        .concat();
        let rows = combat(&sub_areas, &monsters, &shown(&positions, &all_reached(&positions)));
        assert_eq!(rows, [row(1, 4, &[2]), row(5, 200, &[3])]);
        // Five of the field's maps are not in the world graph: with nine shown it is no wider than the cave, and both
        // are taken, the cave's larvae worth more than a quarter of the field's monster.
        let graph: HashSet<i64> = all_reached(&positions).into_iter().filter(|&id| id < 400).collect();
        let rows = combat(&sub_areas, &monsters, &shown(&positions, &graph));
        assert_eq!(rows, [row(1, 4, &[2]), row(5, 200, &[3, 2])]);
    }

    #[test]
    fn the_order_a_sub_area_lists_its_monsters_in_changes_nothing() {
        let monsters: Vec<Monster> = (1..=40)
            .map(|id| monster(id, &[1, 2 + u16::try_from(id).unwrap_or(0)]))
            .collect();
        let listed: Vec<i32> = (1..=40).collect();
        let reversed: Vec<i32> = listed.iter().rev().copied().collect();
        let positions = [maps(100, 12, 1, 1), maps(200, 12, 2, 1)].concat();
        let shown = shown(&positions, &all_reached(&positions));
        let rows = |second: &[i32]| {
            combat(
                &[sub_area(1, 1, &[], &listed, true), sub_area(2, 1, &[], second, true)],
                &monsters,
                &shown,
            )
        };
        // Both sub-areas hold the same monsters: equal worth, so the lower id comes first at every level, whatever order.
        assert_eq!(rows(&reversed), rows(&listed));
        assert_eq!(rows(&listed), [row(1, 200, &[1, 2])]);
    }

    #[test]
    fn a_sub_area_naming_no_monster_the_data_holds_is_never_taken() {
        let sub_areas = [sub_area(1, 1, &[], &[999], true)];
        let positions = maps(100, 12, 1, 1);
        assert_eq!(
            combat(&sub_areas, &[], &shown(&positions, &all_reached(&positions))),
            []
        );
    }
}
