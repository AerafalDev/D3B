//! Where D3B's blueprints go by default: ranges computed from the game data as the app runs, never written in the
//! blueprints' files (`docs/bot.md`, "D3B's blueprints"; the owner, 2026-11-16 and 2026-11-17). For each level from 1
//! to [`HIGHEST_LEVEL`], the sub-areas worth most, the richest first; consecutive levels of the same sub-areas make one
//! row, as the player's own ranges are written. A profession's ranges follow its level, Combat's the character's;
//! [`fits`] leaves out, as the bot plays, a sub-area above the character's level or closed to its account.

pub(crate) mod combat;
pub(crate) mod harvest;
mod worth;

use std::cmp::Ordering;
use std::collections::{HashMap, HashSet};

use serde_json::{Value, json};

use crate::data::{MapPosition, SubArea};

/// The highest level of a character and of a profession in Dofus 3.
pub(crate) const HIGHEST_LEVEL: u16 = 200;

/// The most sub-areas a row holds: the C# version's rotation drew from the 10 richest (D391).
pub(crate) const MOST_PLACES: usize = 10;

/// The least share of the richest sub-area's worth another must reach to be kept, as the C# version's rotation left
/// out those worth under a quarter of the richest (D391).
pub(crate) const LEAST_SHARE: f64 = 0.25;

/// A row of computed ranges: from a level to another, both included, the sub-areas worth most, the richest first.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Row {
    pub(crate) from: u16,
    pub(crate) to: u16,
    pub(crate) sub_areas: Vec<i32>,
}

/// The rows as a blueprint's ranges of a list of zones hold them (`bot::json`, schema 2): `{"from", "to", "value"}`,
/// each value the game's sub-areas, `{"sub_area": <id>}`.
pub(crate) fn to_value(rows: &[Row]) -> Value {
    rows.iter()
        .map(|row| {
            let zones: Vec<Value> = row.sub_areas.iter().map(|id| json!({"sub_area": id})).collect();
            json!({"from": row.from, "to": row.to, "value": zones})
        })
        .collect()
}

/// How many maps of each sub-area the world graph reaches among those drawn on a world map. A sub-area of none is
/// never taken: on 3.7.13.17 they are dungeons, a mine of one resource, the tutorial, and a convention's test zone
/// listing 85 resources, which topped every profession from level 100 (the owner, 2026-11-17).
pub(crate) fn shown(positions: &[MapPosition], graph_maps: &HashSet<i64>) -> HashMap<i32, usize> {
    let mut shown: HashMap<i32, usize> = HashMap::new();
    for position in positions {
        if position.world_map >= 0 && graph_maps.contains(&i64::from(position.id)) {
            *shown.entry(position.sub_area_id).or_default() += 1;
        }
    }
    shown
}

/// Whether a character of `level` may go to `sub_area`: not above its level, where the monsters attack, and only one
/// a basic account enters when its account is not subscribed (the C# version's D166 and D245; the owner, 2026-11-17).
pub(crate) fn fits(sub_area: &SubArea, level: u16, subscribed: bool) -> bool {
    sub_area.level <= u32::from(level) && (subscribed || sub_area.basic_account_allowed)
}

/// Where to go at `level`: the sub-areas of its row that `fits` keeps, in their order, else those of the nearest row
/// below that holds one, else none. A low character of a high profession harvests the richest place it may enter
/// rather than nothing (the owner, 2026-11-17); `fits` is [`fits`] for the character, by sub-area id.
pub(crate) fn reachable(rows: &[Row], level: u16, fits: impl Fn(i32) -> bool) -> Vec<i32> {
    rows.iter()
        .rev()
        .filter(|row| row.from <= level)
        .map(|row| row.sub_areas.iter().copied().filter(|&id| fits(id)).collect::<Vec<_>>())
        .find(|kept| !kept.is_empty())
        .unwrap_or_default()
}

/// The ranges `rank` gives level by level, consecutive levels of the same sub-areas in one row; a level where no
/// sub-area is worth anything is in no row.
fn ranges(rank: impl Fn(u16) -> Vec<i32>) -> Vec<Row> {
    let mut rows: Vec<Row> = Vec::new();
    for level in 1..=HIGHEST_LEVEL {
        let sub_areas = rank(level);
        if sub_areas.is_empty() {
            continue;
        }
        match rows.last_mut() {
            Some(row) if row.to + 1 == level && row.sub_areas == sub_areas => row.to = level,
            _ => rows.push(Row {
                from: level,
                to: level,
                sub_areas,
            }),
        }
    }
    rows
}

/// A sub-area and what it is worth at a level.
#[derive(Debug, Clone, Copy)]
struct Scored {
    id: i32,
    level: u32,
    worth: f64,
}

/// The sub-areas worth most at `level`, the richest first, then the one whose level is nearest, then by id: at most
/// [`MOST_PLACES`], each worth at least [`LEAST_SHARE`] of the richest.
fn richest(scored: impl Iterator<Item = Scored>, level: u16) -> Vec<i32> {
    let mut scored: Vec<Scored> = scored.filter(|place| place.worth > 0.0).collect();
    let gap = |place: &Scored| place.level.abs_diff(u32::from(level));
    scored.sort_by(|a, b| {
        b.worth
            .total_cmp(&a.worth)
            .then_with(|| gap(a).cmp(&gap(b)))
            .then_with(|| a.id.cmp(&b.id))
    });
    let Some(best) = scored.first().map(|place| place.worth) else {
        return Vec::new();
    };
    scored
        .iter()
        .take_while(|place| place.worth.total_cmp(&(best * LEAST_SHARE)) != Ordering::Less)
        .take(MOST_PLACES)
        .map(|place| place.id)
        .collect()
}

#[cfg(test)]
pub(super) mod tests {
    use super::*;
    use crate::bot::variable::Shape;
    use crate::bot::{Type, ValueType};

    /// A sub-area of `level` listing these harvestables and monsters, open to a basic account or not.
    pub(crate) fn sub_area(id: i32, level: u32, harvestables: &[i32], monsters: &[i32], basic: bool) -> SubArea {
        let text = r#"{"id":0,"name":null,"areaId":0,"level":1,"zaapMapId":null,"basicAccountAllowed":true,
            "customWorldMapId":-1,"harvestables":[],"monsters":[]}"#;
        SubArea {
            id,
            level,
            harvestables: harvestables.to_vec(),
            monsters: monsters.to_vec(),
            basic_account_allowed: basic,
            ..serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
        }
    }

    /// `count` maps of `sub_area`, from map `first` on, drawn on world `world` or none (-1).
    pub(crate) fn maps(first: u32, count: u32, sub_area: i32, world: i32) -> Vec<MapPosition> {
        (first..first + count)
            .map(|id| {
                serde_json::from_value(
                    json!({"id": id, "x": 0, "y": 0, "subAreaId": sub_area, "worldMap": world,
                    "name": null, "flags": []}),
                )
                .unwrap_or_else(|error| panic!("{error}"))
            })
            .collect()
    }

    fn scored(id: i32, level: u32, worth: f64) -> Scored {
        Scored { id, level, worth }
    }

    #[test]
    fn a_sub_area_counts_its_maps_on_a_world_map_that_the_world_graph_reaches() {
        let positions = [maps(100, 3, 1, 1), maps(200, 4, 1, -1), maps(300, 2, 2, 1)].concat();
        let graph: HashSet<i64> = [100, 101, 200, 201, 300].into_iter().collect();
        assert_eq!(shown(&positions, &graph), HashMap::from([(1, 2), (2, 1)]));
    }

    #[test]
    fn the_richest_come_first_then_the_nearest_level_then_the_lowest_id() {
        let places = [
            scored(3, 10, 1.0),
            scored(1, 40, 2.0),
            scored(2, 30, 1.0),
            scored(4, 22, 1.0),
        ];
        assert_eq!(richest(places.into_iter(), 20), [1, 4, 2, 3]);
    }

    #[test]
    fn only_the_ten_richest_worth_a_quarter_of_the_richest_are_kept() {
        let many = (1..=12).map(|id| scored(id, 1, 1.0));
        assert_eq!(richest(many, 1), (1..=10).collect::<Vec<_>>());
        let thin = [
            scored(1, 1, 4.0),
            scored(2, 1, 1.0),
            scored(3, 1, 0.99),
            scored(4, 1, 0.0),
        ];
        assert_eq!(richest(thin.into_iter(), 1), [1, 2]);
        assert_eq!(richest(std::iter::empty(), 1), Vec::<i32>::new());
    }

    #[test]
    fn consecutive_levels_of_the_same_sub_areas_make_one_row_and_a_level_of_none_no_row() {
        let rows = ranges(|level| match level {
            1..=4 => vec![7],
            5..=9 => Vec::new(),
            10..=19 => vec![8, 7],
            _ => vec![9],
        });
        let row = |from, to, sub_areas: &[i32]| Row {
            from,
            to,
            sub_areas: sub_areas.to_vec(),
        };
        assert_eq!(rows, [row(1, 4, &[7]), row(10, 19, &[8, 7]), row(20, 200, &[9])]);
        // The same sub-areas on both sides of levels of none make two rows, never one over the levels of none.
        let split = ranges(|level| if (5..=9).contains(&level) { Vec::new() } else { vec![7] });
        assert_eq!(split, [row(1, 4, &[7]), row(10, 200, &[7])]);
    }

    #[test]
    fn the_rows_are_ranges_of_a_list_of_zones_a_blueprint_holds() {
        let rows = [
            Row {
                from: 1,
                to: 19,
                sub_areas: vec![95, 97],
            },
            Row {
                from: 20,
                to: 200,
                sub_areas: vec![445],
            },
        ];
        let value = to_value(&rows);
        assert_eq!(
            value[0],
            json!({"from": 1, "to": 19, "value": [{"sub_area": 95}, {"sub_area": 97}]})
        );
        let zones = Type::new(ValueType::Zone, Shape::new(true, true));
        assert!(zones.holds(&value));
        assert!(!zones.overlaps(&value));
    }

    #[test]
    fn a_sub_area_above_the_character_or_closed_to_a_basic_account_does_not_fit() {
        let open = sub_area(95, 12, &[], &[], true);
        let closed = sub_area(96, 12, &[], &[], false);
        assert!(fits(&open, 12, false));
        assert!(!fits(&open, 11, true));
        assert!(!fits(&closed, 50, false));
        assert!(fits(&closed, 50, true));
    }

    #[test]
    fn a_row_with_no_sub_area_that_fits_gives_way_to_the_rows_below() {
        let rows = [
            Row {
                from: 1,
                to: 19,
                sub_areas: vec![1, 2],
            },
            Row {
                from: 20,
                to: 99,
                sub_areas: vec![3, 4],
            },
            Row {
                from: 100,
                to: 200,
                sub_areas: vec![5, 6],
            },
        ];
        // A lumberjack of 100 whose character reaches only sub-areas 2 and 4: its row's sub-areas that fit, in order.
        assert_eq!(reachable(&rows, 100, |id| id != 6), [5]);
        assert_eq!(reachable(&rows, 100, |id| id == 2 || id == 4), [4]);
        assert_eq!(reachable(&rows, 150, |id| id == 2), [2]);
        assert_eq!(reachable(&rows, 100, |_| false), Vec::<i32>::new());
        // A level of no row reads the rows below it.
        assert_eq!(reachable(&rows[..2], 150, |_| true), [3, 4]);
        assert_eq!(reachable(&rows[1..], 10, |_| true), Vec::<i32>::new());
    }

    #[test]
    fn computes_the_ranges_of_the_folder_the_app_ships_when_there_is_one() {
        // `data/` holds a real extraction only on a machine that fetched or extracted one: elsewhere there is nothing.
        let folder = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../data");
        if !folder.join(crate::data::Manifest::FILE).is_file() {
            return;
        }
        let data = crate::data::GameData::load(&folder).unwrap_or_else(|error| panic!("{error}"));
        let graph: HashSet<i64> = data.graph.vertices.iter().map(|vertex| vertex.map_id).collect();
        let shown = shown(&data.world.positions, &graph);
        let name = |id: &i32| {
            let sub_area = data.world.sub_areas.iter().find(|sub_area| sub_area.id == *id);
            sub_area
                .and_then(|sub_area| sub_area.name.as_ref())
                .map_or_else(String::new, |name| name.fr.clone())
        };
        let zones = Type::new(ValueType::Zone, Shape::new(true, true));
        // Every level has a row, of at most ten sub-areas, which a blueprint holds as ranges of zones.
        let check = |what: &str, rows: &[Row]| {
            let levels: Vec<u16> = rows.iter().flat_map(|row| row.from..=row.to).collect();
            assert_eq!(levels, (1..=HIGHEST_LEVEL).collect::<Vec<_>>(), "{what}");
            assert!(
                rows.iter().all(|row| (1..=MOST_PLACES).contains(&row.sub_areas.len())),
                "{what}"
            );
            assert!(
                zones.holds(&to_value(rows)) && !zones.overlaps(&to_value(rows)),
                "{what}"
            );
        };
        let combat = combat::combat(&data.world.sub_areas, &data.monsters.monsters, &shown);
        check("Combat", &combat);
        for job in [2, 24, 26, 28, 36] {
            check(
                &format!("job {job}"),
                &harvest::harvest(&data.world.sub_areas, &data.items, &shown, job),
            );
        }
        // As the C# version measured on 3.7.12.12 (D166): a lumberjack of 80 to 109 goes first to the Bois de Litneg.
        let lumberjack = harvest::harvest(&data.world.sub_areas, &data.items, &shown, 2);
        let at = |level: u16| {
            lumberjack
                .iter()
                .find(|row| (row.from..=row.to).contains(&level))
                .map(|row| name(&row.sub_areas[0]))
        };
        assert_eq!(at(100).as_deref(), Some("Bois de Litneg"));
        // No sub-area without a map shown: the convention's test zone (1021) and the tutorial (536) are in no row.
        assert!(
            combat
                .iter()
                .chain(&lumberjack)
                .all(|row| !row.sub_areas.contains(&1021) && !row.sub_areas.contains(&536))
        );
    }
}
