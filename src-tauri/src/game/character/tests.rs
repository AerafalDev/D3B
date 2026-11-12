use std::time::{Duration, Instant};

use super::*;
use crate::protocol::game::{
    self, Actor, ActorDisposition, Character as Chosen, CharacterBasics, CharacterLook, CharacterStats, Characteristic,
    DetailedCharacteristic, character_selection_event::Success, characteristic::Kind,
};

const ME: i64 = 1_234_567;

fn chosen(id: i64, name: &str, level: i32) -> Message {
    Message::CharacterSelectionEvent(game::CharacterSelectionEvent {
        result: Some(Selection::Success(Success {
            character: Some(Chosen {
                id,
                basics: Some(CharacterBasics {
                    level: Some(level),
                    name: name.into(),
                    look: Some(CharacterLook { breed_id: 10 }),
                }),
            }),
        })),
    })
}

/// Characteristics of `max_life` and `lost` life, with `kamas`.
fn stats(max_life: i32, lost: i32, kamas: i64) -> Message {
    let detailed = |id, number_7| Characteristic {
        id,
        kind: Some(Kind::Detailed(DetailedCharacteristic {
            number_7,
            ..Default::default()
        })),
    };
    Message::CharacterStatsEvent(game::CharacterStatsEvent {
        stats: Some(CharacterStats {
            characteristics: vec![detailed(0, 55), detailed(11, max_life - 55), detailed(97, -lost)],
            kamas,
            ..Default::default()
        }),
    })
}

fn map_information(map_id: i64, actors: &[(i64, i32)]) -> Message {
    Message::MapInformationEvent(game::MapInformationEvent {
        actors: actors
            .iter()
            .map(|&(id, cell)| Actor {
                id,
                disposition: Some(ActorDisposition {
                    cell_id: Some(cell),
                    direction: 1,
                }),
                information: None,
            })
            .collect(),
        map_id,
        ..Default::default()
    })
}

fn context(fight: bool) -> Message {
    Message::ContextCreationEvent(game::ContextCreationEvent {
        context: i32::from(fight),
    })
}

fn current(map_id: i64) -> Message {
    Message::MapCurrentEvent(game::MapCurrentEvent { map_id })
}

fn walk(actor_id: i64, cells: &[i32]) -> Message {
    Message::MapMovementEvent(game::MapMovementEvent {
        cells: cells.to_vec(),
        actor_id,
    })
}

fn regeneration() -> Message {
    Message::LifeRegenStartEvent(game::LifeRegenStartEvent { interval: 5 })
}

fn regenerated(life: i32) -> Message {
    Message::LifeRegenEndEvent(game::LifeRegenEndEvent {
        max_life: 480,
        life,
        regenerated: None,
    })
}

fn pods(weight: i32) -> Message {
    Message::InventoryWeightEvent(game::InventoryWeightEvent {
        weight,
        max_weight: 2_350,
    })
}

fn kamas(kamas: i64) -> Message {
    Message::KamasUpdateEvent(game::KamasUpdateEvent { kamas })
}

/// A character fed `messages` at `at`, with nothing dropped.
fn fed(messages: &[Message], at: Instant) -> Character {
    let mut character = Character::default();
    for message in messages {
        character.apply(message, at, 0);
    }
    character
}

fn life(view: &View) -> Option<(i64, i64, bool)> {
    view.life.map(|life| (life.life, life.max_life, life.estimated))
}

#[test]
fn knows_who_after_its_selection_and_keeps_the_characteristics_sent_before() {
    let now = Instant::now();
    let character = fed(&[stats(480, 30, 145_320), chosen(ME, "Lyra", 87)], now);

    let view = character.view(now, 0);
    let who = view
        .who
        .as_ref()
        .map(|who| (who.id, who.name.as_str(), who.level, who.breed));
    assert_eq!(who, Some((ME, "Lyra", 87, 10)));
    assert_eq!(life(&view), Some((450, 480, false)));
    assert_eq!(view.kamas, Some(145_320));
}

#[test]
fn a_second_selection_starts_over_but_for_what_came_since_the_first() {
    let now = Instant::now();
    let mut character = fed(&[stats(480, 30, 1), chosen(ME, "Lyra", 87), pods(812)], now);

    character.apply(&chosen(42, "Kordo", 142), now, 0);
    let view = character.view(now, 0);
    assert_eq!(view.who.map(|who| who.id), Some(42));
    assert_eq!((view.pods, view.life, view.kamas), (None, None, None));

    character.apply(&stats(900, 0, 2), now, 0);
    character.apply(&chosen(43, "Mirabeau", 12), now, 0);
    let view = character.view(now, 0);
    assert_eq!(life(&view), Some((900, 900, false)));
    assert_eq!(view.characteristics.map(|read| read.max_life), Some(900));
}

#[test]
fn what_a_selection_keeps_keeps_its_freshness() {
    let now = Instant::now();
    let mut character = Character::default();
    character.apply(&stats(480, 0, 100), now, 2);
    character.apply(&chosen(ME, "Lyra", 87), now, 2);

    let stale = character.view(now, 2).stale;
    assert_eq!(
        [stale.map, stale.life, stale.pods, stale.kamas],
        [true, false, true, false]
    );
}

#[test]
fn a_failed_selection_changes_nothing() {
    let now = Instant::now();
    let mut character = fed(&[chosen(ME, "Lyra", 87)], now);

    let failure = Selection::Failure(game::character_selection_event::Failure {});
    let failed = Message::CharacterSelectionEvent(game::CharacterSelectionEvent { result: Some(failure) });
    character.apply(&failed, now, 0);

    assert_eq!(character.view(now, 0).who.map(|who| who.id), Some(ME));
}

#[test]
fn a_level_gained_changes_the_level() {
    let now = Instant::now();
    let level = Message::CharacterLevelUpEvent(game::CharacterLevelUpEvent { level: 88 });

    assert_eq!(
        fed(&[chosen(ME, "Lyra", 87), level.clone()], now)
            .view(now, 0)
            .who
            .map(|who| who.level),
        Some(88)
    );
    // Before a selection there is no one to level up.
    assert_eq!(fed(&[level], now).view(now, 0).who, None);
}

#[test]
fn stands_on_its_actor_cell_then_on_the_last_of_each_walk() {
    let now = Instant::now();
    let mut character = fed(
        &[
            chosen(ME, "Lyra", 87),
            map_information(88_213_271, &[(-20, 150), (ME, 288)]),
        ],
        now,
    );
    assert_eq!(character.view(now, 0).cell, Some(288));

    character.apply(&walk(ME, &[288, 302, 316]), now, 0);
    assert_eq!(character.view(now, 0).cell, Some(316));

    // Another actor's walk, or an empty one, leaves the character where it stands.
    character.apply(&walk(-20, &[150, 164]), now, 0);
    character.apply(&walk(ME, &[]), now, 0);
    assert_eq!(character.view(now, 0).cell, Some(316));
}

#[test]
fn the_maps_information_wins_over_a_current_map_it_contradicts() {
    let now = Instant::now();
    let character = fed(
        &[chosen(ME, "Lyra", 87), current(1), map_information(2, &[(ME, 300)])],
        now,
    );

    let view = character.view(now, 0);
    assert_eq!((view.map_id, view.cell), (Some(2), Some(300)));
}

#[test]
fn a_new_map_forgets_the_cell_until_its_information() {
    let now = Instant::now();
    let mut character = fed(&[chosen(ME, "Lyra", 87), map_information(1, &[(ME, 300)])], now);

    character.apply(&current(1), now, 0);
    assert_eq!(character.view(now, 0).cell, Some(300));

    character.apply(&current(2), now, 0);
    let view = character.view(now, 0);
    assert_eq!((view.map_id, view.cell), (Some(2), None));
}

#[test]
fn a_fight_stops_the_regeneration_and_forgets_the_cell() {
    let start = Instant::now();
    let mut character = fed(
        &[
            chosen(ME, "Lyra", 87),
            stats(480, 30, 1),
            context(false),
            regeneration(),
            map_information(1, &[(ME, 300)]),
        ],
        start,
    );
    assert!(character.changing(start));

    character.apply(&context(true), start, 0);
    let view = character.view(start + Duration::from_secs(10), 0);
    assert_eq!((view.context, view.cell), (Some(Context::Fight), None));
    assert_eq!(life(&view), Some((450, 480, false)));
    assert!(!character.changing(start));

    // The characteristics a fight ends with still set the life.
    character.apply(&stats(480, 100, 1), start, 0);
    assert_eq!(life(&character.view(start, 0)), Some((380, 480, false)));
}

#[test]
fn the_regeneration_counts_until_its_end() {
    let start = Instant::now();
    let mut character = fed(&[stats(480, 30, 1), regeneration()], start);
    assert_eq!(
        life(&character.view(start + Duration::from_secs(3), 0)),
        Some((456, 480, true))
    );

    character.apply(&regenerated(470), start, 0);
    assert_eq!(
        life(&character.view(start + Duration::from_secs(9), 0)),
        Some((470, 480, false))
    );
}

#[test]
fn reads_the_pods_and_the_kamas_as_they_change() {
    let now = Instant::now();
    let character = fed(&[stats(480, 0, 100), kamas(90), pods(812)], now);

    let view = character.view(now, 0);
    assert_eq!(view.kamas, Some(90));
    assert_eq!(
        view.pods,
        Some(Pods {
            weight: 812,
            max_weight: 2_350
        })
    );
}

#[test]
fn a_part_is_stale_while_frames_were_dropped_since_its_refresh() {
    let now = Instant::now();
    let mut character = Character::default();
    character.apply(&stats(480, 0, 100), now, 0);
    character.apply(&current(1), now, 0);

    assert_eq!(character.view(now, 0).stale, Stale::default());
    let stale = character.view(now, 3).stale;
    assert!(stale.map && stale.life && stale.pods && stale.kamas);
}

#[test]
fn each_part_is_fresh_again_once_its_own_message_comes() {
    let now = Instant::now();
    let fresh = |message: Message| {
        let mut character = Character::default();
        character.apply(&message, now, 3);
        character.view(now, 3).stale
    };
    // Which parts each message refreshes: map, life, pods, kamas.
    for (message, fresh_parts) in [
        (stats(480, 0, 1), [false, true, false, true]),
        (regeneration(), [false, true, false, false]),
        (regenerated(470), [false, true, false, false]),
        (pods(1), [false, false, true, false]),
        (kamas(1), [false, false, false, true]),
        (map_information(1, &[]), [true, false, false, false]),
        (current(1), [true, false, false, false]),
    ] {
        let stale = fresh(message);
        assert_eq!([!stale.map, !stale.life, !stale.pods, !stale.kamas], fresh_parts);
    }
}

#[test]
fn walks_in_a_fight_are_not_followed() {
    let now = Instant::now();
    let character = fed(&[chosen(ME, "Lyra", 87), context(true), walk(ME, &[100, 114])], now);

    assert_eq!(character.view(now, 0).cell, None);
}

#[test]
fn a_cell_off_the_map_is_not_taken() {
    let now = Instant::now();
    let mut character = fed(&[chosen(ME, "Lyra", 87), map_information(1, &[(ME, 600)])], now);
    assert_eq!(character.view(now, 0).cell, None);

    character.apply(&walk(ME, &[300, 559]), now, 0);
    assert_eq!(character.view(now, 0).cell, Some(559));
    for off in [560, -1] {
        character.apply(&walk(ME, &[559, off]), now, 0);
        assert_eq!(character.view(now, 0).cell, Some(559), "{off}");
    }
}

#[test]
fn a_context_is_roleplay_a_fight_or_unknown() {
    let now = Instant::now();
    let unknown = Message::ContextCreationEvent(game::ContextCreationEvent { context: 7 });

    assert_eq!(
        fed(&[context(false)], now).view(now, 0).context,
        Some(Context::Roleplay)
    );
    assert_eq!(fed(&[context(true)], now).view(now, 0).context, Some(Context::Fight));
    assert_eq!(fed(&[context(true), unknown], now).view(now, 0).context, None);
}
