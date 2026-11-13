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
    // Characteristic 97 sent with no kind, or not at all.
    let empty = Characteristic { id: 97, kind: None };
    for characteristics in [
        vec![detailed(0, 0, 55), detailed(11, 400, 25), empty],
        vec![detailed(0, 0, 55), detailed(11, 400, 25)],
    ] {
        let read = Characteristics::read(&stats(characteristics));

        assert_eq!((read.life, read.max_life), (480, 480));
    }
}

#[test]
fn every_number_of_a_detailed_characteristic_counts() {
    let numbers = Characteristic {
        id: 11,
        kind: Some(Kind::Detailed(DetailedCharacteristic {
            number_1: 1,
            number_2: 2,
            number_3: 4,
            number_4: 8,
            number_5: 16,
            number_7: 32,
        })),
    };

    assert_eq!(Characteristics::read(&stats(vec![numbers])).max_life, 63);
}

#[test]
fn sums_from_the_wire_saturate_rather_than_overflow() {
    let huge = |id| Characteristic {
        id,
        kind: Some(Kind::Value(CharacteristicValue {
            number_1: 1,
            total: i64::MAX,
        })),
    };
    let lost = Characteristic {
        id: 97,
        kind: Some(Kind::Value(CharacteristicValue {
            number_1: -1,
            total: i64::MIN,
        })),
    };

    let read = Characteristics::read(&stats(vec![huge(0), huge(11), huge(29), huge(29), lost]));

    assert_eq!((read.max_life, read.energy), (i64::MAX, i64::MAX));
    assert_eq!(read.life, i64::MAX.saturating_add(i64::MIN));
}

#[test]
fn reads_the_points_and_the_energy_of_each_kind() {
    let usable = |id, number_4, spent| Characteristic {
        id,
        kind: Some(Kind::Usable(UsableCharacteristic {
            number_1: 100,
            number_2: 200,
            number_3: 400,
            number_4,
            number_5: 800,
            number_6: -1_500,
            spent,
        })),
    };
    let value = |id, total| Characteristic {
        id,
        kind: Some(Kind::Value(CharacteristicValue { number_1: 5, total })),
    };

    let read = Characteristics::read(&stats(vec![
        usable(1, 6, 3),
        usable(23, 3, 2),
        value(29, 9_865),
        value(47, 9_995),
    ]));

    // The spent points (7) are not part of the value.
    assert_eq!((read.action_points, read.movement_points), (6, 3));
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
    assert_eq!(at(&life, start + Duration::from_millis(39_999)), (479, 480, true));
    assert_eq!(at(&life, start + Duration::from_secs(3_600)), (480, 480, false));
}

#[test]
fn a_full_life_is_not_regenerating_while_the_server_has_not_ended_the_regeneration() {
    // The server ends a regeneration only later (a fight, a sit), so a full life must not keep saying it regenerates
    // (the owner, 2026-11-13): at its max the count is the life itself.
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(478, 480);
    life.start(5, start);

    assert_eq!(at(&life, start + Duration::from_millis(500)), (479, 480, true));
    assert_eq!(at(&life, start + Duration::from_secs(1)), (480, 480, false));
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

    life.stop(start + Duration::from_secs(10));

    // The 20 points counted up to the fight are kept, and no more are counted.
    assert_eq!(at(&life, start + Duration::from_secs(60)), (420, 480, false));
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
}

#[test]
fn a_life_sent_above_its_max_shows_the_max_either_way() {
    let start = Instant::now();
    let mut life = Life::default();
    life.sent(500, 480);
    assert_eq!(at(&life, start), (480, 480, false));

    life.start(5, start);
    assert_eq!(at(&life, start), (480, 480, false));
}
