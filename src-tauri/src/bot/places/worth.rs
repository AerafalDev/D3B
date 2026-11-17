//! What a resource or a monster is worth to a character: whole at its level, halved every 20 levels below, nothing
//! above (the owner, 2026-11-16; the C# version's D166 and its `HuntingGround.WorthOf`, measured in game).

/// The levels below the character's, or its profession's, that halve a resource's or a monster's worth.
pub(crate) const HALVING_LEVELS: f64 = 20.0;

/// What a resource of `resource` level is worth to a profession of `job` level: 1 at its level, halved every
/// [`HALVING_LEVELS`] below, smoothly, and 0 above it, since the profession cannot gather it yet.
pub(crate) fn resource(job: u16, resource: u16) -> f64 {
    if resource > job {
        return 0.0;
    }
    halved(job - resource)
}

/// What a monster whose grades are of `levels` is worth to a character of `level`: 1 when one of its grades reaches
/// the level, 0 when even its lowest is above it, else halved every [`HALVING_LEVELS`] below from its highest grade
/// (the owner, 2026-11-17, as the C# version measured; its grades' levels do not always rise, `docs/data.md`).
pub(crate) fn monster(level: u16, levels: &[u16]) -> f64 {
    let (Some(&lowest), Some(&highest)) = (levels.iter().min(), levels.iter().max()) else {
        return 0.0;
    };
    if lowest > level {
        return 0.0;
    }
    if highest >= level {
        return 1.0;
    }
    halved(level - highest)
}

fn halved(below: u16) -> f64 {
    0.5_f64.powf(f64::from(below) / HALVING_LEVELS)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn near(found: f64, expected: f64) -> bool {
        (found - expected).abs() < 1e-9
    }

    #[test]
    fn a_resource_is_whole_at_its_profession_s_level_halved_every_20_levels_below_and_nothing_above() {
        assert!(near(resource(20, 20), 1.0));
        assert!(near(resource(40, 20), 0.5));
        assert!(near(resource(60, 20), 0.25));
        assert!(near(resource(30, 20), 0.5_f64.sqrt()));
        assert!(near(resource(19, 20), 0.0));
    }

    #[test]
    fn a_monster_is_whole_when_a_grade_reaches_the_level_and_nothing_when_its_lowest_is_above() {
        assert!(near(monster(18, &[16, 17, 18, 19, 20]), 1.0));
        assert!(near(monster(25, &[16, 17, 18, 19, 20]), 0.5_f64.powf(0.25)));
        assert!(near(monster(40, &[16, 20]), 0.5));
        assert!(near(monster(15, &[16, 17, 18, 19, 20]), 0.0));
        // Wabbit Gm's grades fall from 80 to 75 (182 on 3.7.13.17): its lowest is 75, its highest 200.
        assert!(near(monster(76, &[80, 80, 80, 80, 80, 75, 130, 200]), 1.0));
        assert!(near(monster(74, &[80, 75, 130]), 0.0));
    }

    #[test]
    fn a_monster_without_a_grade_is_worth_nothing() {
        assert!(near(monster(50, &[]), 0.0));
    }
}
