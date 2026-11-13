//! A character's characteristics and its life between them (`docs/game.md`, "The state of a character"): what the
//! server sends, and the regeneration the client counts on its own between two messages.

use std::time::{Duration, Instant};

use crate::protocol::game::{CharacterStats, characteristic::Kind};

/// The characteristics the live view shows, read from `CharacterStatsEvent` (3.7.13.17, `artifacts/studies/live-view.md`).
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub(crate) struct Characteristics {
    /// Characteristic 0 plus 11, the vitality (verified on all 634 `LifeRegenEndEvent`).
    pub max_life: i64,
    /// The max plus characteristic 97, the life lost, never above 0 (verified, as above).
    pub life: i64,
    /// Characteristic 1.
    pub action_points: i64,
    /// Characteristic 23.
    pub movement_points: i64,
    /// Characteristics 29 and 47.
    pub energy: i64,
    pub max_energy: i64,
    /// The stats' fields 8, 11 and 2, in that order on every frame: the level's floor, the experience and the next
    /// level's floor (the order verified, the meaning inferred).
    pub level_experience: i64,
    pub experience: i64,
    pub next_level_experience: i64,
}

impl Characteristics {
    /// The characteristics of `stats`. Every sum saturates: the numbers come from the wire, and a frame may hold any.
    pub(crate) fn read(stats: &CharacterStats) -> Self {
        let value = |id: i32| {
            stats
                .characteristics
                .iter()
                .filter(|characteristic| characteristic.id == id)
                .map(|characteristic| characteristic.kind.as_ref().map_or(0, value))
                .fold(0_i64, i64::saturating_add)
        };
        let max_life = value(0).saturating_add(value(11));
        Self {
            max_life,
            life: max_life.saturating_add(value(97)),
            action_points: value(1),
            movement_points: value(23),
            energy: value(29),
            max_energy: value(47),
            level_experience: stats.level_experience,
            experience: stats.experience,
            next_level_experience: stats.next_level_experience,
        }
    }
}

/// A characteristic's value: the sum of its numbers (verified for a detailed one, on the life; inferred for the
/// others, the C# version), a usable one's spent points (7) left out.
fn value(kind: &Kind) -> i64 {
    let numbers: &[i32] = match kind {
        Kind::Usable(usable) => &[
            usable.number_1,
            usable.number_2,
            usable.number_3,
            usable.number_4,
            usable.number_5,
            usable.number_6,
        ],
        Kind::Value(value) => return i64::from(value.number_1).saturating_add(value.total),
        Kind::Detailed(detailed) => &[
            detailed.number_1,
            detailed.number_2,
            detailed.number_3,
            detailed.number_4,
            detailed.number_5,
            detailed.number_7,
        ],
    };
    numbers.iter().copied().map(i64::from).fold(0, i64::saturating_add)
}

/// The life now, and whether it is the client's count of a regeneration still below the max rather than a value the
/// server sent (a count at the max is the life itself).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct LifeNow {
    pub life: i64,
    pub max_life: i64,
    pub estimated: bool,
}

/// One point back every `every`, counted from `since` (3.7.13.17, verified: within a point of `LifeRegenEndEvent`'s
/// count on all 634 regenerations ended, `artifacts/studies/live-view.md`).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
struct Regeneration {
    since: Instant,
    every: Duration,
}

/// The life the server last sent, and the regeneration running since, if one is.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub(crate) struct Life {
    sent: Option<(i64, i64)>,
    regeneration: Option<Regeneration>,
}

impl Life {
    /// The life of new characteristics: a running regeneration keeps its start, the server sending during one the life
    /// as it was then (verified, 516 of 634 regenerations).
    pub(crate) fn sent(&mut self, life: i64, max_life: i64) {
        self.sent = Some((life, max_life));
    }

    /// `LifeRegenStartEvent`: one point every `interval` tenths of a second from `at` (the unit inferred, the C#
    /// version; the count it gives verified, as above); an interval of 0 or less regenerates nothing.
    pub(crate) fn start(&mut self, interval: i32, at: Instant) {
        self.regeneration = u64::try_from(interval)
            .ok()
            .filter(|&interval| interval > 0)
            .map(|interval| Regeneration {
                since: at,
                every: Duration::from_millis(interval * 100),
            });
    }

    /// `LifeRegenEndEvent`: the life the server counted, regenerated points included, and no regeneration (verified,
    /// 634 of 634).
    pub(crate) fn end(&mut self, life: i64, max_life: i64) {
        self.sent = Some((life, max_life));
        self.regeneration = None;
    }

    /// A fight starts at `now` (verified: each regeneration ends as a fight starts, 634 of 634): the points counted so
    /// far are kept, and no more are counted.
    pub(crate) fn stop(&mut self, now: Instant) {
        if let Some(life) = self.at(now) {
            self.sent = Some((life.life, life.max_life));
        }
        self.regeneration = None;
    }

    /// The life at `now`: the life sent plus the points regenerated since the start, rounded down, never above the max.
    /// A count that reached the max is no estimate: the server may end the regeneration much later (a fight, a sit),
    /// and a full life must not keep saying it regenerates (the owner, 2026-11-13).
    pub(crate) fn at(&self, now: Instant) -> Option<LifeNow> {
        let (life, max_life) = self.sent?;
        let points = self.regeneration.map_or(0, |regeneration| {
            let elapsed = now.saturating_duration_since(regeneration.since);
            i64::try_from(elapsed.as_millis() / regeneration.every.as_millis()).unwrap_or(i64::MAX)
        });
        let life = life.saturating_add(points).min(max_life);
        Some(LifeNow {
            life,
            max_life,
            estimated: self.regeneration.is_some() && life < max_life,
        })
    }
}

#[cfg(test)]
mod tests;
