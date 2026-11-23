//! The run's random source (`docs/bot.md`, "The values (v2.4)"): `SplitMix64`, seeded for each run by the standard
//! library's `RandomState` (keys drawn from the system once per thread, then counted on, so each run's seed differs)
//! and kept with it, so a run can be replayed from its seed; tests pass a fixed one.

use std::hash::{BuildHasher, Hasher};

/// A stream of random numbers from a seed.
#[derive(Debug, Clone)]
pub(crate) struct Random {
    seed: u64,
    state: u64,
}

impl Random {
    /// The stream of `seed`.
    pub(crate) fn new(seed: u64) -> Self {
        Self { seed, state: seed }
    }

    /// A stream of a seed drawn fresh from the system (the owner, 2026-11-17: a fresh seed per run).
    pub(crate) fn fresh() -> Self {
        Self::new(std::collections::hash_map::RandomState::new().build_hasher().finish())
    }

    /// The seed it started from.
    pub(crate) fn seed(&self) -> u64 {
        self.seed
    }

    fn next(&mut self) -> u64 {
        self.state = self.state.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.state;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }

    /// A number from 0 to `count` - 1, each as likely (rejection keeps it unbiased); 0 for no count.
    pub(crate) fn below(&mut self, count: u64) -> u64 {
        if count == 0 {
            return 0;
        }
        let zone = u64::MAX - u64::MAX % count;
        loop {
            let drawn = self.next();
            if drawn < zone {
                return drawn % count;
            }
        }
    }

    /// A whole number from `low` to `high`, both included, bounds the other way round swapped.
    pub(crate) fn between(&mut self, low: i64, high: i64) -> i64 {
        let (low, high) = (low.min(high), low.max(high));
        let span = u64::try_from(i128::from(high) - i128::from(low)).unwrap_or(u64::MAX);
        let offset = if span == u64::MAX {
            self.next()
        } else {
            self.below(span + 1)
        };
        let drawn = i128::from(low) + i128::from(offset);
        // Within `low` to `high`, both whole numbers, so it fits.
        i64::try_from(drawn).unwrap_or(high)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_seed_gives_its_own_stream_again() {
        let draws = |seed| {
            let mut random = Random::new(seed);
            (0..5).map(|_| random.between(1, 6)).collect::<Vec<_>>()
        };
        assert_eq!(draws(1), draws(1));
        assert_ne!(draws(1), draws(2));
        assert_eq!(Random::new(7).seed(), 7);
    }

    #[test]
    fn between_stays_within_its_bounds_swapped_and_reaches_each_value() {
        let mut random = Random::new(3);
        let mut seen = [false; 6];
        for _ in 0..10_000 {
            let drawn = random.between(6, 1);
            assert!((1..=6).contains(&drawn), "{drawn}");
            seen[usize::try_from(drawn - 1).unwrap_or_default()] = true;
        }
        assert_eq!(seen, [true; 6]);
        assert_eq!(random.between(5, 5), 5);
        // The whole range of whole numbers: each draw its own, not one of the bounds.
        let wide: Vec<i64> = (0..4).map(|_| random.between(i64::MIN, i64::MAX)).collect();
        assert!(
            wide.iter().all(|it| *it != i64::MIN && *it != i64::MAX) && wide[0] != wide[1],
            "{wide:?}"
        );
    }
}
