//! The world's atlas, what the World view and tab draw (`docs/bot.md`, "In the app"): the worlds the client shows,
//! each zoom with the files of its tiles and its hints, and the game's sub-areas grouped by area, each with the maps it
//! covers on each of those worlds, the resources it holds and how many monsters live there.

use std::collections::{BTreeMap, BTreeSet};
use std::path::Path;

use serde::Serialize;
use specta::Type;
use tauri::Manager;

use super::LoadedData;
use super::character::Names;
use crate::data::{self, Hint, ItemTables, MapFlag, World, WorldMap};

/// The worlds and the sub-areas the World view lists, in order of id.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
pub struct Atlas {
    pub worlds: Vec<AtlasWorld>,
    pub areas: Vec<AtlasArea>,
    /// The professions whose resources a sub-area holds, in order of id.
    pub jobs: Vec<AtlasJob>,
    /// The resources a sub-area holds, in order of profession, then level, then id.
    pub resources: Vec<AtlasResource>,
}

/// A harvesting profession, and its icon's file.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct AtlasJob {
    pub id: i32,
    pub name: Names,
    pub icon: Option<String>,
}

/// A resource: an item a profession gathers, its level, and its icon's file, none for an item whose icon the client
/// lacks.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct AtlasResource {
    pub id: i32,
    pub name: Names,
    pub level: u8,
    pub job: i32,
    pub icon: Option<String>,
}

/// A world the client shows, as its map is drawn: map `x, y` covers the image from `origin + (x, y) * mapSize`, at
/// zoom 100 (3.7.13.17, inferred from `WorldMapsDataRoot` and checked by eye: Forêt d'Astrub's maps drawn so fall on
/// the forest of the tiles, mock-up 10 of 2026-11-15).
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct AtlasWorld {
    pub id: i32,
    pub name: Option<Names>,
    pub origin: [i32; 2],
    pub map_size: [f32; 2],
    /// The image's size at zoom 100.
    pub size: [i32; 2],
    /// The client's bounds on the zoom, and the zoom it opens at, 1 being 100.
    pub min_scale: f32,
    pub max_scale: f32,
    pub start_scale: f32,
    /// Its levels of zoom, largest first.
    pub zooms: Vec<AtlasZoom>,
    /// The hints the client draws on it, by kind then id.
    pub hints: Vec<AtlasHint>,
}

/// A hint of the world map: what it is, its name, its map's coordinates, a dungeon's level (0 for most others), and its
/// picture's file, none for the one the client lacks (475, "Portail vers la Source", on 3.7.13.17).
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct AtlasHint {
    pub id: i32,
    pub kind: HintKind,
    pub name: Names,
    pub at: [i32; 2],
    pub level: u32,
    pub picture: Option<String>,
}

/// What a hint is, in order of use to a bot (`data::HintKind`).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub enum HintKind {
    Zaap,
    Bank,
    Dungeon,
    Workshop,
    AuctionHouse,
    ClassPlace,
    Transport,
    Place,
    Passage,
}

impl From<data::HintKind> for HintKind {
    fn from(kind: data::HintKind) -> Self {
        match kind {
            data::HintKind::Zaap => Self::Zaap,
            data::HintKind::Bank => Self::Bank,
            data::HintKind::Dungeon => Self::Dungeon,
            data::HintKind::Workshop => Self::Workshop,
            data::HintKind::AuctionHouse => Self::AuctionHouse,
            data::HintKind::ClassPlace => Self::ClassPlace,
            data::HintKind::Transport => Self::Transport,
            data::HintKind::Place => Self::Place,
            data::HintKind::Passage => Self::Passage,
        }
    }
}

/// A level of a world's image: its size, and its tiles' files row by row from the top left, 1,024 pixels a side but for
/// the last column and row.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct AtlasZoom {
    pub percent: u32,
    pub width: u32,
    pub height: u32,
    pub columns: u32,
    pub rows: u32,
    /// Each tile's file as the platform writes it, which the webview loads through the asset protocol.
    pub tiles: Vec<String>,
}

/// An area and its sub-areas.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct AtlasArea {
    pub id: i32,
    pub name: Names,
    pub sub_areas: Vec<AtlasSubArea>,
}

/// A sub-area, a ready-made zone, and the maps it covers on each world shown; none when no world shows it (211 on
/// 3.7.13.17: dungeons, houses, hidden worlds' and those the client parks at 0, 0), and never more than one world there, so its maps say its world.
#[derive(Debug, Clone, PartialEq, Serialize, Type)]
pub struct AtlasSubArea {
    pub id: i32,
    pub name: Names,
    pub level: u32,
    pub maps: Vec<WorldMaps>,
    /// The resources it holds, ids of the atlas's, in their order.
    pub resources: Vec<i32>,
    /// How many kinds of monsters live there; the data names none of them yet.
    pub monsters: u32,
}

/// The maps of a sub-area on one world, by their coordinates, each once.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
pub struct WorldMaps {
    pub world: i32,
    pub at: Vec<[i16; 2]>,
    /// Those of `at` where one of its maps has the client's priority on the world map, which a click there chooses
    /// over a sub-area without it (the Hakam tree's trunk over Village des Zoths; 3.7.13.17, inferred from the data
    /// and checked on the World tab).
    pub priority: Vec<[i16; 2]>,
}

/// Why there is no atlas.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum AtlasError {
    /// The app has no game data, or does not know its folder: why, as it came.
    NoData { reason: String },
}

/// The atlas of the packed game data, or why there is none; built off the main thread, its 15,000 positions grouped.
#[tauri::command]
#[specta::specta]
pub async fn world_atlas(app: tauri::AppHandle) -> Result<Atlas, AtlasError> {
    let built = tauri::async_runtime::spawn_blocking(move || {
        let data = app.state::<LoadedData>();
        let game = data
            .0
            .as_ref()
            .map(|game| (&game.world, game.worlds.as_slice(), game.hints.as_slice(), &game.items));
        read(game, data.1.as_deref())
    });
    built.await.unwrap_or_else(|error| {
        Err(AtlasError::NoData {
            reason: error.to_string(),
        })
    })
}

/// The coordinates of a sub-area's maps on one world, and those where one of them has the priority.
type Covered = (BTreeSet<[i16; 2]>, BTreeSet<[i16; 2]>);

/// The game data's world, worlds, hints and items.
type Game<'a> = (&'a World, &'a [WorldMap], &'a [Hint], &'a ItemTables);

/// The atlas of the game data, whose tiles and pictures are in `folder`, or why there is none.
fn read(game: Result<Game, &String>, folder: Option<&Path>) -> Result<Atlas, AtlasError> {
    match (game, folder) {
        (Ok(game), Some(folder)) => Ok(atlas(game, folder)),
        (Err(reason), _) => Err(AtlasError::NoData { reason: reason.clone() }),
        (Ok(_), None) => Err(AtlasError::NoData {
            reason: "the game data's folder is unknown".into(),
        }),
    }
}

fn names(text: &crate::data::Text) -> Names {
    Names {
        fr: text.fr.clone(),
        en: text.en.clone(),
    }
}

/// The atlas of the game data, whose tiles and pictures are in `folder`: the worlds the client shows with their hints,
/// and the named areas' named sub-areas that hold at least one map, their maps on the worlds shown and their resources.
fn atlas((world, worlds, hints, items): Game, folder: &Path) -> Atlas {
    let shown: Vec<&WorldMap> = worlds.iter().filter(|one| one.visible).collect();
    let shown_ids: BTreeSet<i32> = shown.iter().map(|one| one.id).collect();
    // Each sub-area's maps, by world then by coordinates: a map's floors share them; and those with the priority.
    let mut maps: BTreeMap<i32, BTreeMap<i32, Covered>> = BTreeMap::new();
    let mut held = BTreeSet::new();
    for position in &world.positions {
        held.insert(position.sub_area_id);
        let priority = position.flags.contains(&MapFlag::HasPriorityOnWorldmap);
        // The client parks at 0, 0 the maps it draws nowhere (1,820 on the Twelve: dungeons, houses, guild halls); only
        // those with the priority are truly there (Village d'Amakna's). 3.7.13.17, inferred from the data and checked on
        // the World tab, not read from the client's code.
        let parked = (position.x, position.y) == (0, 0) && !priority;
        if shown_ids.contains(&position.world_map) && !parked {
            let (at, first) = maps
                .entry(position.sub_area_id)
                .or_default()
                .entry(position.world_map)
                .or_default();
            at.insert([position.x, position.y]);
            if priority {
                first.insert([position.x, position.y]);
            }
        }
    }
    // The professions and resources the sub-areas listed hold, gathered as they are listed.
    let mut jobs = BTreeMap::new();
    let mut resources = BTreeMap::new();
    let areas = world
        .areas
        .iter()
        .filter_map(|area| {
            // Named first, so that what a sub-area left out holds never enters the tables.
            let name = area.name.as_ref()?;
            let sub_areas: Vec<AtlasSubArea> = world
                .sub_areas
                .iter()
                .filter(|sub_area| sub_area.area_id == area.id && held.contains(&sub_area.id))
                .filter_map(|sub_area| {
                    let name = sub_area.name.as_ref()?;
                    let held = items.resources(&sub_area.harvestables);
                    for resource in &held {
                        jobs.entry(resource.job.id).or_insert(resource.job);
                        resources
                            .entry((resource.job.id, resource.item.level, resource.item.id))
                            .or_insert(resource.item);
                    }
                    Some(AtlasSubArea {
                        id: sub_area.id,
                        name: names(name),
                        level: sub_area.level,
                        maps: maps
                            .get(&sub_area.id)
                            .into_iter()
                            .flatten()
                            .map(|(world, (at, first))| WorldMaps {
                                world: *world,
                                at: at.iter().copied().collect(),
                                priority: first.iter().copied().collect(),
                            })
                            .collect(),
                        resources: held.iter().map(|resource| resource.item.id).collect(),
                        monsters: u32::try_from(sub_area.monsters.iter().collect::<BTreeSet<_>>().len())
                            .unwrap_or(u32::MAX),
                    })
                })
                .collect();
            (!sub_areas.is_empty()).then(|| AtlasArea {
                id: area.id,
                name: names(name),
                sub_areas,
            })
        })
        .collect();
    Atlas {
        worlds: shown.into_iter().map(|one| atlas_world(one, hints, folder)).collect(),
        areas,
        jobs: jobs
            .into_values()
            .map(|job| AtlasJob {
                id: job.id,
                name: names(&job.name),
                icon: ItemTables::job_icon(job).map(|file| in_folder(folder, &file)),
            })
            .collect(),
        resources: resources
            .into_iter()
            .map(|((job, _, _), item)| AtlasResource {
                id: item.id,
                name: names(&item.name),
                level: item.level,
                job,
                icon: items.item_icon(item).map(|file| in_folder(folder, &file)),
            })
            .collect(),
    }
}

/// The file of the data folder's `file`, a path relative to it, as the platform writes it.
fn in_folder(folder: &Path, file: &str) -> String {
    file.split('/')
        .fold(folder.to_path_buf(), |path, part| path.join(part))
        .to_string_lossy()
        .into_owned()
}

fn atlas_world(world: &WorldMap, hints: &[Hint], folder: &Path) -> AtlasWorld {
    let tiles = folder.join("tiles").join(world.id.to_string());
    let pictures = folder.join("icons").join("hints");
    let mut drawn: Vec<&Hint> = hints.iter().filter(|hint| hint.world_map_id == world.id).collect();
    drawn.sort_by_key(|hint| (hint.kind(), hint.id));
    let drawn = drawn
        .into_iter()
        .map(|hint| {
            let picture = pictures.join(format!("{}.png", hint.gfx));
            AtlasHint {
                id: hint.id,
                kind: hint.kind().into(),
                name: names(&hint.name),
                at: [hint.x, hint.y],
                level: hint.level,
                picture: picture.is_file().then(|| picture.to_string_lossy().into_owned()),
            }
        })
        .collect();
    AtlasWorld {
        id: world.id,
        name: world.name.as_ref().map(names),
        origin: [world.origin_x, world.origin_y],
        map_size: [world.map_width, world.map_height],
        size: [world.width, world.height],
        min_scale: world.min_scale,
        max_scale: world.max_scale,
        start_scale: world.start_scale,
        zooms: world
            .zooms
            .iter()
            .map(|zoom| {
                let level = tiles.join(zoom.percent.to_string());
                AtlasZoom {
                    percent: zoom.percent,
                    width: zoom.width,
                    height: zoom.height,
                    columns: zoom.columns,
                    rows: zoom.rows,
                    tiles: (1..=zoom.columns * zoom.rows)
                        .map(|n| level.join(format!("{n}.jpg")).to_string_lossy().into_owned())
                        .collect(),
                }
            })
            .collect(),
        hints: drawn,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn parse<T: serde::de::DeserializeOwned>(json: &str) -> T {
        serde_json::from_str(json).unwrap_or_else(|error| panic!("{error}"))
    }

    fn text(fr: &str) -> String {
        format!(r#"{{"fr":"{fr}","en":"{fr} (en)","de":"","es":"","pt":""}}"#)
    }

    fn world() -> World {
        let astrub = text("Astrub");
        let forest = text("Forêt");
        let array = |items: Vec<String>| format!("[{}]", items.join(","));
        World {
            areas: parse(&format!(
                r#"[{{"id":0,"name":{astrub},"superAreaId":0,"worldmapId":1,"hasWorldMap":true}},
                    {{"id":1,"name":null,"superAreaId":0,"worldmapId":1,"hasWorldMap":true}},
                    {{"id":2,"name":{astrub},"superAreaId":0,"worldmapId":1,"hasWorldMap":true}}]"#
            )),
            sub_areas: parse(&array(
                [
                    (95, forest.as_str(), 0),
                    (96, "null", 0),
                    (97, forest.as_str(), 1),
                    (98, forest.as_str(), 2),
                    (99, forest.as_str(), 0),
                ]
                .iter()
                .map(|(id, name, area)| {
                    format!(
                        r#"{{"id":{id},"name":{name},"areaId":{area},"level":{id},"zaapMapId":null,"basicAccountAllowed":true,
                          "customWorldMapId":-1,"harvestables":[],"monsters":[]}}"#
                    )
                })
                .collect(),
            )),
            positions: parse(&array(
                [
                    (1, -1, -27, 95, 1),
                    (2, -1, -27, 95, 1),
                    (3, 4, 2, 95, 2),
                    (4, 0, 0, 95, 9),
                    (5, 3, 3, 96, 1),
                    (6, 3, 3, 97, 1),
                    (7, 0, 0, 99, -1),
                ]
                .iter()
                .map(|(id, x, y, sub_area, world)| {
                    format!(
                        r#"{{"id":{id},"x":{x},"y":{y},"subAreaId":{sub_area},"worldMap":{world},"name":null,"flags":[]}}"#
                    )
                })
                .collect(),
            )),
            zaaps: Vec::new(),
        }
    }

    fn worlds() -> Vec<WorldMap> {
        let name = text("Monde des Douze");
        let world = |id: i32, visible: bool| {
            format!(
                r#"{{"id":{id},"name":{name},"originX":6480,"originY":4944,"mapWidth":69.5,"mapHeight":49.7,"minScale":0.2,
                    "maxScale":1,"startScale":0.6,"width":2500,"height":1100,"visible":{visible},
                    "zooms":[{{"percent":100,"width":2500,"height":1100,"columns":3,"rows":2}}]}}"#
            )
        };
        serde_json::from_str(&format!("[{},{},{}]", world(1, true), world(2, true), world(9, false)))
            .unwrap_or_else(|error| panic!("{error}"))
    }

    // The extractor's own tests write these files (tools/extractor/tests/Items/): wheat and wood, the lumberjack
    // cutting the wood; the wheat reaped by a farmer, two more woods, a loot box, and a fisher's fish added.
    fn items() -> ItemTables {
        let read = |text: &str| -> Vec<serde_json::Value> { parse(text) };
        let mut items = read(include_str!("../../../tools/extractor/tests/fixtures/items/items.json"));
        let item = |id: i32, level: i32| {
            let mut one = items[1].clone();
            one["id"] = id.into();
            one["level"] = level.into();
            one
        };
        let added = [item(100, 20), item(200, 1), item(500, 1), item(30562, 1)];
        items.extend(added);
        items.sort_by_key(|one| one["id"].as_i64());
        let mut jobs = read(include_str!("../../../tools/extractor/tests/fixtures/items/jobs.json"));
        let mut skills = read(include_str!(
            "../../../tools/extractor/tests/fixtures/items/skills.json"
        ));
        jobs.push(serde_json::json!({"id": 28, "name": jobs[1]["name"], "iconId": 28}));
        jobs.push(serde_json::json!({"id": 36, "name": jobs[1]["name"], "iconId": -1}));
        for (id, job, item) in [(45, 28, 289), (46, 2, 100), (47, 2, 200), (48, 36, 500)] {
            let mut skill = skills[0].clone();
            skill["id"] = id.into();
            skill["parentJobId"] = job.into();
            skill["gatheredItemId"] = item.into();
            skills.push(skill);
        }
        ItemTables {
            items: parse(&serde_json::Value::from(items).to_string()),
            types: parse(include_str!(
                "../../../tools/extractor/tests/fixtures/items/item-types.json"
            )),
            jobs: parse(&serde_json::Value::from(jobs).to_string()),
            skills: parse(&serde_json::Value::from(skills).to_string()),
            // The wheat's icon; the wood names none.
            item_icons: std::collections::HashSet::from([42011]),
        }
    }

    #[test]
    fn gives_each_sub_area_its_resources_and_how_many_monsters_live_there() {
        let mut world = world();
        // A loot box (30562), which no skill gathers, among the resources; a monster listed twice.
        world.sub_areas[0].harvestables = vec![289, 30562, 303, 100, 200];
        world.sub_areas[0].monsters = vec![31, 34, 31];
        // The fish of 96, unnamed, and of 97, in an unnamed area: neither is listed, nor is what it holds.
        world.sub_areas[1].harvestables = vec![500];
        world.sub_areas[2].harvestables = vec![500];
        let folder = Path::new("data");
        let atlas = atlas((&world, &worlds(), &[], &items()), folder);
        let file = |path: &str| {
            let joined = path.split('/').fold(folder.to_path_buf(), |path, part| path.join(part));
            Some(joined.to_string_lossy().into_owned())
        };

        let jobs: Vec<(i32, &str, Option<String>)> = atlas
            .jobs
            .iter()
            .map(|job| (job.id, job.name.en.as_str(), job.icon.clone()))
            .collect();
        assert_eq!(
            jobs,
            [
                (2, "Lumberjack", file("icons/jobs/1.png")),
                (28, "Lumberjack", file("icons/jobs/28.png"))
            ]
        );
        let resources: Vec<(i32, &str, u8, i32, Option<String>)> = atlas
            .resources
            .iter()
            .map(|one| (one.id, one.name.fr.as_str(), one.level, one.job, one.icon.clone()))
            .collect();
        assert_eq!(
            resources,
            [
                (200, "Bois", 1, 2, None),
                (303, "Bois", 1, 2, None),
                (100, "Bois", 20, 2, None),
                (289, "Blé", 1, 28, file("icons/items/42011.png"))
            ]
        );
        let forest = &atlas.areas[0].sub_areas[0];
        assert_eq!(
            (forest.id, forest.resources.as_slice(), forest.monsters),
            (95, [200, 303, 100, 289].as_slice(), 2)
        );
        // A sub-area that lists nothing holds nothing.
        let other = &atlas.areas[0].sub_areas[1];
        assert_eq!((other.resources.as_slice(), other.monsters), ([].as_slice(), 0));
    }

    #[test]
    fn gives_the_worlds_shown_with_each_tile_s_file() {
        let folder = Path::new("data");
        let atlas = atlas((&world(), &worlds(), &[], &items()), folder);
        assert_eq!(atlas.worlds.iter().map(|one| one.id).collect::<Vec<_>>(), [1, 2]);
        let first = &atlas.worlds[0];
        assert_eq!(
            first.name.as_ref().map(|name| name.fr.as_str()),
            Some("Monde des Douze")
        );
        assert_eq!(
            (first.origin, first.map_size, first.size),
            ([6480, 4944], [69.5, 49.7], [2500, 1100])
        );
        assert_eq!((first.min_scale, first.max_scale, first.start_scale), (0.2, 1.0, 0.6));
        let tiles = &first.zooms[0].tiles;
        assert_eq!(tiles.len(), 6);
        assert_eq!(
            tiles[5],
            folder
                .join("tiles")
                .join("1")
                .join("100")
                .join("6.jpg")
                .to_string_lossy()
        );
    }

    #[test]
    fn groups_the_named_sub_areas_that_hold_a_map_by_named_area_with_their_maps_on_the_worlds_shown() {
        let atlas = atlas((&world(), &worlds(), &[], &items()), Path::new("data"));
        // Area 1 is unnamed and area 2's sub-area holds no map; sub-area 96 is unnamed.
        assert_eq!(atlas.areas.len(), 1);
        let astrub = &atlas.areas[0];
        assert_eq!(
            (astrub.name.fr.as_str(), astrub.name.en.as_str()),
            ("Astrub", "Astrub (en)")
        );
        let ids: Vec<(i32, u32)> = astrub.sub_areas.iter().map(|one| (one.id, one.level)).collect();
        assert_eq!(ids, [(95, 95), (99, 99)]);
        // Two floors at -1, -27 are one map; world 9 is not shown.
        assert_eq!(
            astrub.sub_areas[0].maps,
            [
                WorldMaps {
                    world: 1,
                    at: vec![[-1, -27]],
                    priority: vec![]
                },
                WorldMaps {
                    world: 2,
                    at: vec![[4, 2]],
                    priority: vec![]
                }
            ]
        );
        // A sub-area on no world shown is listed, with no maps.
        assert_eq!(astrub.sub_areas[1].maps, []);
    }

    #[test]
    fn leaves_out_the_maps_the_client_parks_at_0_0_and_says_where_a_sub_area_has_the_priority() {
        let mut world = world();
        let position = |id: i32, x: i16, y: i16, sub_area: i32, flags: &str| -> crate::data::MapPosition {
            parse(&format!(
                r#"{{"id":{id},"x":{x},"y":{y},"subAreaId":{sub_area},"worldMap":1,"name":null,"flags":[{flags}]}}"#
            ))
        };
        // 95's house parked at 0, 0; 99 truly there, with the priority, and sharing 3, 3 with 97, which has it not.
        world.positions.extend([
            position(10, 0, 0, 95, ""),
            position(11, 0, 0, 99, r#""hasPriorityOnWorldmap""#),
            position(12, 3, 3, 99, r#""outdoor","hasPriorityOnWorldmap""#),
        ]);
        let atlas = atlas((&world, &worlds(), &[], &items()), Path::new("data"));
        let astrub = &atlas.areas[0];
        assert_eq!(astrub.sub_areas[0].maps[0].at, [[-1, -27]]);
        assert_eq!(
            astrub.sub_areas[1].maps,
            [WorldMaps {
                world: 1,
                at: vec![[0, 0], [3, 3]],
                priority: vec![[0, 0], [3, 3]]
            }]
        );
    }

    // The extractor's own tests write this file (tools/extractor/tests/Hints/HintTablesTests.cs): a bank on world 1, a
    // dungeon on world 2.
    const HINTS: &str = include_str!("../../../tools/extractor/tests/fixtures/hints/hints.json");

    #[test]
    fn gives_each_world_shown_its_hints_by_kind_with_their_pictures() {
        let read: Vec<Hint> = parse(HINTS);
        let (bank, dungeon) = (&read[0], &read[1]);
        let zaap = Hint {
            id: 9000,
            category_id: 9,
            gfx: 410,
            ..bank.clone()
        };
        let hidden = Hint {
            id: 9001,
            world_map_id: 9,
            ..bank.clone()
        };
        let folder = std::env::temp_dir().join(format!("d3b-atlas-{}", std::process::id()));
        let pictures = folder.join("icons").join("hints");
        std::fs::create_dir_all(&pictures).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(pictures.join("401.png"), b"png").unwrap_or_else(|error| panic!("{error}"));
        let hints = [bank.clone(), dungeon.clone(), zaap, hidden];
        let atlas = atlas((&world(), &worlds(), &hints, &items()), &folder);
        std::fs::remove_dir_all(&folder).unwrap_or_else(|error| panic!("{error}"));

        // World 1: the zaap before the bank, whatever their ids; world 9 is not shown.
        let first: Vec<(i32, HintKind)> = atlas.worlds[0].hints.iter().map(|hint| (hint.id, hint.kind)).collect();
        assert_eq!(first, [(9000, HintKind::Zaap), (2733, HintKind::Bank)]);
        let bank = &atlas.worlds[0].hints[1];
        assert_eq!((bank.name.fr.as_str(), bank.at, bank.level), ("Banque", [4, -18], 0));
        assert_eq!(
            bank.picture,
            Some(pictures.join("401.png").to_string_lossy().into_owned())
        );
        // The zaap's picture is not in the folder, as 475's is not in the client.
        assert_eq!(atlas.worlds[0].hints[0].picture, None);
        let second = &atlas.worlds[1].hints;
        assert_eq!(
            second
                .iter()
                .map(|hint| (hint.id, hint.kind, hint.level))
                .collect::<Vec<_>>(),
            [(2800, HintKind::Dungeon, 50)]
        );
    }

    #[test]
    fn says_why_there_is_no_atlas() {
        assert_eq!(
            read(Err(&"no data".to_owned()), Some(Path::new("data"))),
            Err(AtlasError::NoData {
                reason: "no data".into()
            })
        );
        let (world, worlds) = (world(), worlds());
        assert_eq!(
            read(Ok((&world, &worlds, &[], &items())), None),
            Err(AtlasError::NoData {
                reason: "the game data's folder is unknown".into()
            })
        );
    }

    #[test]
    fn leaves_out_a_sub_area_whose_area_is_missing() {
        let mut world = world();
        world.areas.retain(|area| area.id != 0);
        // Only area 2 is left, whose sub-area holds no map.
        assert_eq!(atlas((&world, &worlds(), &[], &items()), Path::new("data")).areas, []);
    }
}
