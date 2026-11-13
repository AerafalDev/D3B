//! Each character as the app shows it (`docs/game.md`, "In the app"): the event that carries a connection's view
//! whenever it changed, its map named from the game data when the data's build is the connection's.

use serde::{Deserialize, Serialize};
use specta::Type;
use tauri::Manager;
use tauri_specta::Event;

use std::sync::Arc;

use super::LoadedData;
use super::inspector::{self, Inspected, Inspector};
use super::relay::MapAt;
use crate::data::World;
use crate::game::{ActorKind, Context, Live, Monster, Seen, View};
use crate::protocol::Observer;

/// Makes, for each game connection, the character the app follows: each change of its view goes to the webview as
/// [`CharacterChanged`], its map named only when the game data's build is the one the connection named.
pub(crate) fn follow(
    handle: tauri::AppHandle,
    inspector: Arc<Inspector>,
) -> impl Fn(u64, Option<&str>) -> Box<dyn Observer> + Send + Sync + 'static {
    move |number, build| {
        let connection = u32::try_from(number).unwrap_or(u32::MAX);
        let streamed = handle.clone();
        let send = Box::new(move |frames: &_| inspector::emit(&streamed, frames));
        let handle = handle.clone();
        let build = build.map(str::to_owned);
        let named = build.clone();
        let live = Box::new(Live::new(move |view: Option<View>| {
            let data = handle.try_state::<LoadedData>();
            let data = data.as_deref().and_then(|data| data.0.as_ref().ok());
            let data = data.map(|data| (data.manifest.build.as_str(), &data.world));
            let world = world_for(data, named.as_deref());
            let view = view.map(|view| CharacterView::new(&view, world));
            let _ = CharacterChanged { connection, view }.emit(&handle);
        }));
        Box::new(Inspected::new(live, connection, build, Arc::clone(&inspector), send))
    }
}

/// A connection's character changed; `view` is `None` once the connection closed.
#[derive(Debug, Clone, Serialize, Deserialize, Type, tauri_specta::Event)]
pub struct CharacterChanged {
    pub connection: u32,
    pub view: Option<CharacterView>,
}

/// A name in the app's two languages.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
pub struct Names {
    pub fr: String,
    pub en: String,
}

/// Where the character stands, named when the data is of its connection's build.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct Place {
    pub map_id: f64,
    pub at: Option<MapAt>,
    pub sub_area: Option<Names>,
    pub area: Option<Names>,
}

/// A value and the most it can be.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize, Type)]
pub struct Gauge {
    pub value: f64,
    pub max: f64,
}

/// A character's view (`game::View`), its numbers as JavaScript reads them.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct CharacterView {
    pub name: Option<String>,
    pub level: Option<i32>,
    pub breed: Option<i32>,
    pub fighting: Option<bool>,
    pub place: Option<Place>,
    pub cell: Option<i32>,
    pub life: Option<Gauge>,
    /// Whether the life is the client's count of a regeneration still below the max.
    pub life_estimated: bool,
    pub pods: Option<Gauge>,
    pub kamas: Option<f64>,
    pub action_points: Option<f64>,
    pub movement_points: Option<f64>,
    pub energy: Option<Gauge>,
    /// The experience from the level's floor to the next's.
    pub experience: Option<Gauge>,
    pub actors: Vec<ActorSeen>,
    pub interactives: Vec<InteractiveSeen>,
    pub stated: Vec<StatedSeen>,
    pub stale: StaleParts,
}

/// Another actor on the map, and the cell it was last seen on.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Type)]
pub struct ActorSeen {
    pub id: f64,
    pub kind: ActorSeenKind,
    pub cell: Option<i32>,
}

/// What an actor is: a player by name, an NPC or monsters by their ids in the client's tables, or another kind.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ActorSeenKind {
    Player {
        name: String,
    },
    #[serde(rename_all = "camelCase")]
    Npc {
        npc_id: i32,
    },
    MonsterGroup {
        monsters: Vec<MonsterSeen>,
    },
    Other,
}

/// A monster of a group, its main one first.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
pub struct MonsterSeen {
    pub id: i32,
    pub grade: i32,
    pub level: i32,
}

/// An element the character may use, such as a resource, by its id and its kind's.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct InteractiveSeen {
    pub element_id: i32,
    pub kind_id: i32,
    /// Whether one of its skills is enabled for the character.
    pub usable: bool,
}

/// An element with a state: 0 available, 2 being harvested, 1 harvested.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[serde(rename_all = "camelCase")]
pub struct StatedSeen {
    pub element_id: i32,
    pub cell: Option<i32>,
    pub state: i32,
}

/// Which parts may be out of date: the protocol dropped frames since they were last refreshed.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Type)]
#[expect(
    clippy::struct_excessive_bools,
    reason = "one mark per part of the view, each independent of the others"
)]
pub struct StaleParts {
    pub map: bool,
    pub life: bool,
    pub pods: bool,
    pub kamas: bool,
}

/// An `i64` as JavaScript reads it: exact up to 2⁵³, more than any id, kamas or experience the game holds.
#[expect(clippy::cast_precision_loss, reason = "the game's numbers stay far below 2⁵³")]
fn number(value: i64) -> f64 {
    value as f64
}

impl CharacterView {
    /// `view` as the app shows it, its map placed and named from `world`, given only when its build is the connection's.
    pub(crate) fn new(view: &View, world: Option<&World>) -> Self {
        let (life, characteristics) = (view.life, view.characteristics);
        let gauge = |value, max| Gauge {
            value: number(value),
            max: number(max),
        };
        Self {
            name: view.who.as_ref().map(|who| who.name.clone()),
            level: view.who.as_ref().map(|who| who.level),
            breed: view.who.as_ref().map(|who| who.breed),
            fighting: view.context.map(|context| context == Context::Fight),
            place: view.map_id.map(|map_id| place(map_id, world)),
            cell: view.cell,
            life: life.map(|life| gauge(life.life, life.max_life)),
            life_estimated: life.is_some_and(|life| life.estimated),
            pods: view.pods.map(|pods| gauge(pods.weight.into(), pods.max_weight.into())),
            kamas: view.kamas.map(number),
            action_points: characteristics.map(|read| number(read.action_points)),
            movement_points: characteristics.map(|read| number(read.movement_points)),
            energy: characteristics.map(|read| gauge(read.energy, read.max_energy)),
            experience: characteristics.and_then(|read| {
                // Numbers from the wire: saturated, and no gauge when the next floor is not above this one.
                let max = read.next_level_experience.saturating_sub(read.level_experience);
                (max > 0).then(|| gauge(read.experience.saturating_sub(read.level_experience), max))
            }),
            actors: view.around.actors.iter().map(actor).collect(),
            interactives: view
                .around
                .interactives
                .iter()
                .map(|element| InteractiveSeen {
                    element_id: element.element_id,
                    kind_id: element.kind_id,
                    usable: !element.enabled_skills.is_empty(),
                })
                .collect(),
            stated: view
                .around
                .stated
                .iter()
                .map(|element| StatedSeen {
                    element_id: element.element_id,
                    cell: element.cell,
                    state: element.state,
                })
                .collect(),
            stale: StaleParts {
                map: view.stale.map,
                life: view.stale.life,
                pods: view.stale.pods,
                kamas: view.stale.kamas,
            },
        }
    }
}

fn actor(seen: &Seen) -> ActorSeen {
    let monster = |monster: &Monster| MonsterSeen {
        id: monster.id,
        grade: monster.grade,
        level: monster.level,
    };
    ActorSeen {
        id: number(seen.id),
        kind: match &seen.kind {
            ActorKind::Player { name } => ActorSeenKind::Player { name: name.clone() },
            ActorKind::Npc { npc_id } => ActorSeenKind::Npc { npc_id: *npc_id },
            ActorKind::MonsterGroup { main, others } => ActorSeenKind::MonsterGroup {
                monsters: main.iter().chain(others).map(monster).collect(),
            },
            ActorKind::Other => ActorSeenKind::Other,
        },
        cell: seen.cell,
    }
}

/// The world of the game data, given with its build, when that build is `build`, the connection's: another build may
/// number its maps otherwise.
fn world_for<'a>(data: Option<(&str, &'a World)>, build: Option<&str>) -> Option<&'a World> {
    data.filter(|(data_build, _)| build == Some(*data_build))
        .map(|(_, world)| world)
}

/// `map_id` placed and named from `world`, which the caller gives only when its build is the connection's.
fn place(map_id: i64, world: Option<&World>) -> Place {
    let position = world.and_then(|world| {
        let id = u32::try_from(map_id).ok()?;
        let index = world.positions.binary_search_by_key(&id, |position| position.id).ok()?;
        Some(&world.positions[index])
    });
    let sub_area = position.and_then(|position| {
        let sub_areas = &world?.sub_areas;
        let index = sub_areas
            .binary_search_by_key(&position.sub_area_id, |sub_area| sub_area.id)
            .ok()?;
        Some(&sub_areas[index])
    });
    let area = sub_area.and_then(|sub_area| {
        let areas = &world?.areas;
        let index = areas.binary_search_by_key(&sub_area.area_id, |area| area.id).ok()?;
        Some(&areas[index])
    });
    Place {
        map_id: number(map_id),
        at: position.map(|position| MapAt {
            x: position.x,
            y: position.y,
        }),
        sub_area: sub_area.and_then(|sub_area| sub_area.name.as_ref()).map(|name| Names {
            fr: name.fr.clone(),
            en: name.en.clone(),
        }),
        area: area.and_then(|area| area.name.as_ref()).map(|name| Names {
            fr: name.fr.clone(),
            en: name.en.clone(),
        }),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::{Characteristics, Who};

    fn json<T: serde::de::DeserializeOwned>(text: &str) -> T {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn world() -> World {
        let name = |fr: &str, en: &str| format!(r#"{{"fr": "{fr}", "en": "{en}", "de": "", "es": "", "pt": ""}}"#);
        World {
            positions: json(
                r#"[{"id": 88213271, "x": 4, "y": -18, "subAreaId": 2, "worldMap": 1, "name": null, "flags": []}]"#,
            ),
            sub_areas: json(&format!(
                r#"[{{"id": 2, "name": {}, "areaId": 0, "level": 1, "zaapMapId": null, "basicAccountAllowed": true,
                    "customWorldMapId": -1, "harvestables": [], "monsters": []}}]"#,
                name("Forêt", "Forest")
            )),
            areas: json(&format!(
                r#"[{{"id": 0, "name": {}, "superAreaId": 0, "worldmapId": 1, "hasWorldMap": true}}]"#,
                name("Amakna", "Amakna")
            )),
            zaaps: Vec::new(),
        }
    }

    #[test]
    fn places_and_names_the_map_from_the_data_it_is_given() {
        let view = View {
            map_id: Some(88_213_271),
            ..View::default()
        };

        let place = CharacterView::new(&view, Some(&world())).place;
        let place = place.unwrap_or_else(|| panic!("no place"));
        assert_eq!(place.at, Some(MapAt { x: 4, y: -18 }));
        assert_eq!(place.sub_area.map(|name| name.en), Some("Forest".into()));
        assert_eq!(place.area.map(|name| name.fr), Some("Amakna".into()));

        // No data of the connection's build, or a map it lacks: the map's id alone.
        let unnamed = CharacterView::new(&view, None).place;
        assert_eq!(
            unnamed.map(|place| (place.map_id, place.at)),
            Some((88_213_271.0, None))
        );
        let lacking = View {
            map_id: Some(-1),
            ..View::default()
        };
        let lacking = CharacterView::new(&lacking, Some(&world())).place;
        assert_eq!(lacking.and_then(|place| place.sub_area), None);
    }

    #[test]
    fn shows_who_the_experience_and_a_group_its_main_monster_first() {
        let monster = |id, level| Monster { id, grade: 3, level };
        let mut view = View {
            who: Some(Who {
                id: 1,
                name: "Lyra".into(),
                level: 87,
                breed: 10,
            }),
            context: Some(Context::Fight),
            characteristics: Some(Characteristics {
                level_experience: 800,
                experience: 1_000,
                next_level_experience: 1_200,
                ..Characteristics::default()
            }),
            ..View::default()
        };
        view.around.actors.push(Seen {
            id: -20,
            kind: ActorKind::MonsterGroup {
                main: Some(monster(489, 52)),
                others: vec![monster(101, 34)],
            },
            cell: Some(190),
        });

        let shown = CharacterView::new(&view, None);

        assert_eq!(
            (shown.name.as_deref(), shown.level, shown.fighting),
            (Some("Lyra"), Some(87), Some(true))
        );
        assert_eq!(
            shown.experience,
            Some(Gauge {
                value: 200.0,
                max: 400.0
            })
        );
        let ActorSeenKind::MonsterGroup { monsters } = &shown.actors[0].kind else {
            panic!("not a group: {:?}", shown.actors);
        };
        assert_eq!(
            monsters.iter().map(|monster| monster.id).collect::<Vec<_>>(),
            [489, 101]
        );
    }

    #[test]
    fn the_data_names_a_connection_of_its_own_build_only() {
        let world = world();
        let data = Some(("3.7.13.17", &world));

        assert!(world_for(data, Some("3.7.13.17")).is_some());
        assert!(world_for(data, Some("3.7.14.1")).is_none());
        assert!(world_for(data, None).is_none());
        assert!(world_for(None, Some("3.7.13.17")).is_none());
    }

    #[test]
    fn a_map_or_its_tables_the_data_lacks_are_not_named() {
        let mut world = world();
        world.areas.clear();
        let shown = |map_id| {
            let view = View {
                map_id: Some(map_id),
                ..View::default()
            };
            CharacterView::new(&view, Some(&world))
                .place
                .unwrap_or_else(|| panic!("no place"))
        };

        let absent = shown(5);
        assert_eq!((absent.at, absent.sub_area), (None, None));
        let without_area = shown(88_213_271);
        assert!(without_area.sub_area.is_some());
        assert_eq!(without_area.area, None);
    }

    #[test]
    fn experience_from_the_wire_saturates_and_a_floor_not_below_the_next_shows_none() {
        let shown = |level_experience, experience, next_level_experience| {
            let view = View {
                characteristics: Some(Characteristics {
                    level_experience,
                    experience,
                    next_level_experience,
                    ..Characteristics::default()
                }),
                ..View::default()
            };
            CharacterView::new(&view, None).experience
        };

        assert_eq!(shown(1_000, 1_000, 1_000), None);
        assert_eq!(shown(1_200, 1_000, 800), None);
        let huge = shown(i64::MIN, i64::MAX, 0);
        assert_eq!(huge.map(|gauge| gauge.value), Some(number(i64::MAX)));
    }

    #[test]
    fn a_group_without_its_main_monster_shows_the_others() {
        let mut view = View::default();
        view.around.actors.push(Seen {
            id: -20,
            kind: ActorKind::MonsterGroup {
                main: None,
                others: vec![Monster {
                    id: 101,
                    grade: 1,
                    level: 34,
                }],
            },
            cell: None,
        });

        let shown = CharacterView::new(&view, None);

        assert_eq!(
            shown.actors[0].kind,
            ActorSeenKind::MonsterGroup {
                monsters: vec![MonsterSeen {
                    id: 101,
                    grade: 1,
                    level: 34
                }]
            }
        );
    }
}
