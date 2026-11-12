use super::*;
use crate::protocol::game::{
    self, ActorDisposition, ActorInformation, InteractiveSkill, MonsterGroupActor, NamedActor, NpcActor, RoleplayActor,
};

const ME: i64 = 1_234_567;

fn actor(id: i64, cell: i32, kind: Option<Roleplay>) -> Actor {
    Actor {
        id,
        disposition: Some(ActorDisposition {
            cell_id: Some(cell),
            direction: 1,
        }),
        information: Some(ActorInformation {
            roleplay: Some(RoleplayActor { kind }),
        }),
    }
}

fn player(id: i64, cell: i32) -> Actor {
    actor(id, cell, Some(Roleplay::Named(NamedActor { name: format!("P{id}") })))
}

fn monster(monster_id: i32, level: i32) -> game::Monster {
    game::Monster {
        grade: 3,
        level,
        monster_id,
    }
}

fn interactive(element_id: i32, on_current_map: bool, enabled: &[i32]) -> InteractiveElement {
    let skills = |ids: &[i32]| {
        ids.iter()
            .map(|&skill_id| InteractiveSkill {
                instance_id: 1,
                skill_id,
            })
            .collect()
    };
    InteractiveElement {
        on_current_map,
        disabled_skills: skills(&[46]),
        interactive_id: 1,
        enabled_skills: skills(enabled),
        element_id,
    }
}

fn stated(element_id: i32, on_current_map: bool, state: i32) -> StatedElement {
    StatedElement {
        on_current_map,
        element_id,
        cell_id: 287,
        state,
    }
}

fn ids(around: &Around) -> Vec<(i64, Option<i32>)> {
    around.actors.iter().map(|actor| (actor.id, actor.cell)).collect()
}

#[test]
fn tells_each_actor_by_its_kind() {
    let group = MonsterGroupActor {
        monsters: Some(game::MonsterGroup {
            others: vec![monster(101, 34)],
            main: Some(monster(489, 52)),
        }),
    };
    let mut around = Around::default();

    around.shown(
        &[
            player(7, 150),
            actor(-1, 260, Some(Roleplay::Npc(NpcActor { npc_id: 2876 }))),
            actor(-2, 190, Some(Roleplay::MonsterGroup(group))),
            actor(-3, 10, None),
            Actor {
                information: None,
                ..actor(-4, 11, None)
            },
        ],
        Some(ME),
    );

    let kinds: Vec<_> = around.actors.iter().map(|actor| actor.kind.clone()).collect();
    let monster = |id, level| Monster { id, grade: 3, level };
    assert_eq!(
        kinds,
        [
            ActorKind::Player { name: "P7".into() },
            ActorKind::Npc { npc_id: 2876 },
            ActorKind::MonsterGroup {
                main: Some(monster(489, 52)),
                others: vec![monster(101, 34)]
            },
            ActorKind::Other,
            ActorKind::Other,
        ]
    );
}

#[test]
fn the_map_holds_neither_the_character_nor_the_neighbours_elements() {
    let information = MapInformationEvent {
        stated_elements: vec![stated(1, true, 0), stated(2, false, 0)],
        actors: vec![player(ME, 288), player(7, 150)],
        interactive_elements: vec![interactive(1, true, &[45]), interactive(3, false, &[45])],
        map_id: 1,
        sub_area_id: 2,
    };

    let around = Around::of_map(&information, Some(ME));

    assert_eq!(ids(&around), [(7, Some(150))]);
    assert_eq!(
        around.interactives,
        [Interactive {
            element_id: 1,
            kind_id: 1,
            enabled_skills: vec![45],
            disabled_skills: vec![46],
        }]
    );
    assert_eq!(
        around.stated,
        [Stated {
            element_id: 1,
            cell: Some(287),
            state: 0
        }]
    );
}

#[test]
fn actors_come_move_and_leave_by_their_id() {
    let mut around = Around::default();
    around.shown(&[player(7, 150), player(8, 160)], Some(ME));

    around.shown(&[player(7, 170)], Some(ME));
    around.walked(8, &[160, 174, 188]);
    assert_eq!(ids(&around), [(7, Some(170)), (8, Some(188))]);

    around.removed(7);
    // An id never seen: removed or walking, it changes nothing.
    around.removed(99);
    around.walked(99, &[1, 2]);
    // A walk to a cell off the map, or an empty one, leaves the actor where it stands.
    around.walked(8, &[188, 600]);
    around.walked(8, &[]);
    assert_eq!(ids(&around), [(8, Some(188))]);
}

#[test]
fn an_element_update_replaces_its_own_and_adds_one_of_the_current_map_only() {
    let mut around = Around::default();
    around.stated(&stated(1, true, 0));
    around.interactive(&interactive(1, true, &[45]));

    // Updates of a known element say it is not on the current map (verified, every stated one): they still apply.
    around.stated(&stated(1, false, 2));
    around.stated(&stated(5, false, 1));
    around.interactive(&interactive(1, false, &[]));
    around.interactive(&interactive(5, false, &[45]));

    assert_eq!(
        around
            .stated
            .iter()
            .map(|element| (element.element_id, element.state))
            .collect::<Vec<_>>(),
        [(1, 2)]
    );
    assert_eq!(around.interactives.len(), 1);
    assert_eq!(around.interactives[0].enabled_skills, Vec::<i32>::new());
}

#[test]
fn a_cell_off_the_map_is_none() {
    let mut around = Around::default();
    around.shown(&[player(7, 560), player(8, -1)], Some(ME));
    around.stated(&StatedElement {
        cell_id: 900,
        ..stated(1, true, 0)
    });

    assert_eq!(ids(&around), [(7, None), (8, None)]);
    assert_eq!(around.stated[0].cell, None);
}
