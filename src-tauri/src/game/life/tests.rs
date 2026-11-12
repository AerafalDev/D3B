use std::time::{Duration, Instant};

use super::*;
use crate::protocol::game::{
    Characteristic, CharacteristicValue, DetailedCharacteristic, UsableCharacteristic, characteristic::Kind,
};

fn detailed(id: i32, number_5: i32, number_7: i32) -> Characteristic {
    Characteristic {
        id,
        kind: Some(Kind::Detailed(DetailedCharacteristic {
            number_5,
            number_7,
            ..Default::default()
        })),
    }
}

fn stats(characteristics: Vec<Characteristic>) -> CharacterStats {
    CharacterStats {
        characteristics,
        next_level_experience: 1_200,
        kamas: 0,
        level_experience: 800,
        experience: 1_000,
    }
}

fn at(life: &Life, now: Instant) -> (i64, i64, bool) {
    let life = life.at(now).unwrap_or_else(|| panic!("no life"));
    (life.life, life.max_life, life.estimated)
}

#[test]
fn reads_the_life_and_its_max_as_the_sum_of_three_characteristics() {
    let read = Characteristics::read(&stats(vec![
        detailed(0, 0, 55),
        detailed(11, 400, 25),
        detailed(97, 0, -30),
    ]));

    assert_eq!((read.life, read.max_life), (450, 480));
    assert_eq!(
        (read.level_experience, read.experience, read.next_level_experience),
        (800, 1_000, 1_200)
    );
}

#[test]
fn a_life_never_lost_is_the_max() {
    // Characteristic 97 sent empty, or not at all.
    let read = Characteristics::read(&stats(vec![detailed(0, 0, 55), detailed(11, 400, 25)]));

    assert_eq!((read.life, read.max_life), (480, 480));
}

#[test]
fn reads_the_points_and_the_energy_of_each_kind() {
    let usable = |id, number_4, spent| Characteristic {
        id,
        kind: Some(Kind::Usable(UsableCharacteristic {
            number_4,
            number_6: 1,
            spent,
            ..Default::default()
        })),
    };
    let value = |id, total| Characteristic {
        id,
        kind: Some(Kind::Value(CharacteristicValue { number_1: 0, total })),
    };

    let read = Characteristics::read(&stats(vec![
        usable(1, 6, 3),
        usable(23, 3, 2),
        value(29, 9_870),
        value(47, 10_000),
        Characteristic { id: 5, kind: None },
    ]));

    // The spent points (7) are not part of the value.
    assert_eq!((read.action_points, read.movement_points), (7, 4));
    assert_eq!((read.energy, read.max_energy), (9_870, 10_000));
}

#[test]
fn regenerates_a_point_each_interval_rounded_down_up_to_the_max() {
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(400, 480);
    life.start(5, start);

    assert_eq!(at(&life, start), (400, 480, true));
    assert_eq!(at(&life, start + Duration::from_millis(499)), (400, 480, true));
    assert_eq!(at(&life, start + Duration::from_millis(1_250)), (402, 480, true));
    assert_eq!(at(&life, start + Duration::from_secs(3_600)), (480, 480, true));
    assert!(life.regenerating(start));
    assert!(!life.regenerating(start + Duration::from_secs(3_600)));
}

#[test]
fn characteristics_during_a_regeneration_keep_its_start() {
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(400, 480);
    life.start(5, start);

    life.sent(410, 500);

    assert_eq!(at(&life, start + Duration::from_secs(2)), (414, 500, true));
}

#[test]
fn the_end_of_a_regeneration_sets_the_life_the_server_counted() {
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(400, 480);
    life.start(5, start);

    life.end(450, 480);

    assert_eq!(at(&life, start + Duration::from_secs(60)), (450, 480, false));
}

#[test]
fn a_fight_stops_the_count() {
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(400, 480);
    life.start(5, start);

    life.stop();

    assert_eq!(at(&life, start + Duration::from_secs(60)), (400, 480, false));
    assert!(!life.regenerating(start));
}

#[test]
fn an_interval_of_zero_or_less_regenerates_nothing() {
    let start = Instant::now();
    for interval in [0, -5] {
        let mut life = Life::default();
        life.sent(400, 480);
        life.start(interval, start);

        assert_eq!(
            at(&life, start + Duration::from_secs(60)),
            (400, 480, false),
            "{interval}"
        );
    }
}

#[test]
fn no_life_is_known_before_the_server_sends_one() {
    let start = Instant::now();
    let mut life = Life::default();
    life.start(5, start);

    assert_eq!(life.at(start + Duration::from_secs(1)), None);
    assert!(!life.regenerating(start));
}
