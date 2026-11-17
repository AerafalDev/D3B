//! The monsters, their races and super races: whom a character fights, at which levels, and what they drop.
//!
//! The client's `MonsterData`, `MonsterRaceData` and `MonsterSuperRaceData` (measured on 3.7.13.17 for format 5,
//! `docs/data.md`). A monster keeps its grades' levels, its drops and its flags (the owner, 2026-11-17): what D3B's
//! default blueprints compute their ranges from, and Chasseur its meats. The extractor leaves out a race of a super
//! race the client lacks and its monsters (race 347 and 6 monsters no sub-area lists, the owner, 2026-11-17), so the
//! app refuses a folder that names one.

mod flag;

use std::collections::HashSet;
use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::items::unique;
use super::manifest::Manifest;
use super::text::Text;
use super::world::SubArea;
pub use flag::MonsterFlag;

/// The grades a drop gives chances for, whatever the monster's grades: the client's `percentDropForGrade1` to `5`.
pub const DROP_GRADES: usize = 5;

/// A monster, such as the Bouftou.
#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Monster {
    pub id: i32,
    pub name: Text,
    /// Its race, its family as a player names it, such as the bouftous.
    pub race_id: i32,
    pub flags: Vec<MonsterFlag>,
    /// The level of each of its grades, grade 1 first: 1 to 20 grades, 5 on 4,037 of 5,129 monsters, levels 1 to 2,400,
    /// not always rising (Wabbit Gm's fall from grade 5 to 6) (measured on 3.7.13.17, `docs/data.md`).
    pub levels: Vec<u16>,
    pub drops: Vec<MonsterDrop>,
}

/// An item a monster drops; a monster may drop one item twice, under two criteria (93 monsters on 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MonsterDrop {
    pub item_id: i32,
    /// Its chances in percent for grades 1 to 5, even for a monster of more grades or fewer, as the client gives them:
    /// what a grade beyond the fifth draws is not measured.
    pub chances: [f32; DROP_GRADES],
    /// The client's criterion as it writes it, such as `Pj>41,9&CU>0` for a hunter's meat (job 41 above level 9, on
    /// 1,374 drops of 3.7.13.17), or `None` for none.
    pub criterion: Option<String>,
}

/// A race of monsters, a family as a player names it.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MonsterRace {
    pub id: i32,
    pub name: Text,
    pub super_race_id: i32,
}

/// A super race of monsters: races grouped by where they live.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct MonsterSuperRace {
    pub id: i32,
    pub name: Text,
}

/// The monsters, their races and super races of a data folder, each in order of id.
#[derive(Debug, Clone, PartialEq)]
pub struct MonsterTables {
    pub monsters: Vec<Monster>,
    pub races: Vec<MonsterRace>,
    pub super_races: Vec<MonsterSuperRace>,
}

impl MonsterTables {
    /// Reads the three tables that `manifest` lists in `folder`, refusing them when an id is held twice, a monster's
    /// race is missing, a race's super race is, a drop's item is missing from `items`, or a monster a sub-area of
    /// `sub_areas` names is.
    pub fn load(
        folder: &Path,
        manifest: &Manifest,
        items: &HashSet<i32>,
        sub_areas: &[SubArea],
    ) -> Result<Self, DataError> {
        Self {
            monsters: manifest.read(folder, "monsters")?,
            races: manifest.read(folder, "monster-races")?,
            super_races: manifest.read(folder, "monster-super-races")?,
        }
        .checked(items, sub_areas)
    }

    fn checked(self, items: &HashSet<i32>, sub_areas: &[SubArea]) -> Result<Self, DataError> {
        let monsters = unique("monsters", self.monsters.iter().map(|monster| monster.id))?;
        let races = unique("monster-races", self.races.iter().map(|race| race.id))?;
        let super_races = unique("monster-super-races", self.super_races.iter().map(|race| race.id))?;
        let reference = |record: String, field, target| Err(DataError::Reference { record, field, target });
        if let Some(monster) = self.monsters.iter().find(|monster| !races.contains(&monster.race_id)) {
            return reference(format!("monster {}", monster.id), "raceId", monster.race_id);
        }
        if let Some(race) = self
            .races
            .iter()
            .find(|race| !super_races.contains(&race.super_race_id))
        {
            return reference(format!("monster race {}", race.id), "superRaceId", race.super_race_id);
        }
        for monster in &self.monsters {
            if let Some(drop) = monster.drops.iter().find(|drop| !items.contains(&drop.item_id)) {
                return reference(format!("monster {}", monster.id), "drops", drop.item_id);
            }
        }
        for sub_area in sub_areas {
            if let Some(&monster) = sub_area.monsters.iter().find(|monster| !monsters.contains(monster)) {
                return reference(format!("sub-area {}", sub_area.id), "monsters", monster);
            }
        }
        Ok(self)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Monsters/).
    const MONSTERS: &str = include_str!("../../../../tools/extractor/tests/fixtures/monsters/monsters.json");
    const RACES: &str = include_str!("../../../../tools/extractor/tests/fixtures/monsters/monster-races.json");
    const SUPER_RACES: &str =
        include_str!("../../../../tools/extractor/tests/fixtures/monsters/monster-super-races.json");

    fn read<T: serde::de::DeserializeOwned>(text: &str) -> Vec<T> {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn fixtures() -> MonsterTables {
        MonsterTables {
            monsters: read(MONSTERS),
            races: read(RACES),
            super_races: read(SUPER_RACES),
        }
    }

    fn items() -> HashSet<i32> {
        HashSet::from([303, 289])
    }

    fn sub_area(monsters: Vec<i32>) -> SubArea {
        let text = r#"{"id":1,"name":null,"areaId":0,"level":1,"zaapMapId":null,"basicAccountAllowed":true,
            "customWorldMapId":0,"harvestables":[],"monsters":[]}"#;
        SubArea {
            monsters,
            ..serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
        }
    }

    #[test]
    fn reads_the_monsters_races_and_super_races_the_extractor_writes() {
        let tables = fixtures()
            .checked(&items(), &[sub_area(vec![31, 47])])
            .unwrap_or_else(|error| panic!("{error}"));
        let larva = &tables.monsters[0];
        assert_eq!((larva.name.fr.as_str(), larva.race_id), ("Larve Bleue", 11));
        assert_eq!(larva.flags, [MonsterFlag::UseSummonSlot, MonsterFlag::CanPlay]);
        assert_eq!(larva.levels, [16, 17, 18, 19, 20]);
        assert_eq!(larva.drops[0].criterion.as_deref(), Some("Pj>41,9&CU>0"));
        assert_eq!(larva.drops[1].chances, [9.24, 9.5, 10.0, 10.5, 11.0]);
        assert_eq!(larva.drops[1].criterion, None);
        assert_eq!(tables.monsters[1].levels, [80, 75, 130]);
        assert_eq!(
            (tables.races[1].name.en.as_str(), tables.races[1].super_race_id),
            ("Larvae", 2)
        );
        assert_eq!(tables.super_races[1].name.en, "Field creatures");
    }

    #[test]
    fn refuses_a_drop_without_five_chances_or_an_unknown_flag() {
        let four = MONSTERS.replace("[55,55,55,55,55]", "[55,55,55,55]");
        assert!(serde_json::from_str::<Vec<Monster>>(&four).is_err());
        let unknown = MONSTERS.replace("\"canPlay\"", "\"canFly\"");
        assert!(serde_json::from_str::<Vec<Monster>>(&unknown).is_err());
    }

    #[test]
    fn refuses_a_reference_the_folder_lacks() {
        type Change = fn(&mut MonsterTables);
        let lacking: [(&str, &str, i32, Change); 2] = [
            ("monster 31", "raceId", 11, |tables| {
                tables.races.remove(1);
            }),
            ("monster race 11", "superRaceId", 2, |tables| {
                tables.super_races.remove(1);
            }),
        ];
        for (expected, expected_field, expected_target, change) in lacking {
            let mut tables = fixtures();
            change(&mut tables);
            assert!(matches!(
                tables.checked(&items(), &[]),
                Err(DataError::Reference { record, field, target })
                    if record == expected && field == expected_field && target == expected_target
            ));
        }
        assert!(matches!(
            fixtures().checked(&HashSet::from([303]), &[]),
            Err(DataError::Reference { record, field: "drops", target: 289 }) if record == "monster 31"
        ));
        assert!(matches!(
            fixtures().checked(&items(), &[sub_area(vec![31, 48])]),
            Err(DataError::Reference { record, field: "monsters", target: 48 }) if record == "sub-area 1"
        ));
    }

    #[test]
    fn refuses_an_id_held_twice_in_any_of_the_three_tables() {
        type Change = fn(&mut MonsterTables);
        let twice: [(&str, i32, Change); 3] = [
            ("monsters", 31, |tables| tables.monsters[1].id = 31),
            ("monster-races", -1, |tables| tables.races[1].id = -1),
            ("monster-super-races", 1, |tables| tables.super_races[1].id = 1),
        ];
        for (expected, id, change) in twice {
            let mut tables = fixtures();
            change(&mut tables);
            assert!(matches!(
                tables.checked(&items(), &[]),
                Err(DataError::Duplicate { table, id: found }) if table == expected && found == id
            ));
        }
    }
}
