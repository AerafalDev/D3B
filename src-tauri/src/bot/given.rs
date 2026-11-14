//! D3B's own blueprints (`docs/bot.md`, "The concepts"): shipped inside the app, locked, duplicated to be changed. Ours,
//! written by hand in `src-tauri/blueprints/`; their names, descriptions and settings' texts are the interface's
//! translations under `given.<id>`, as the nodes' are, the files' English being what a copy starts from.

use super::blueprint::Blueprint;
use super::json::{self, ReadError};

/// The given blueprints, by file: harvest what grows (the owner, 2026-11-13), harvest a profession's resources chosen
/// per character (the owner, 2026-11-14), hunt.
const GIVEN: &[(&str, &str)] = &[
    (
        "automatic-harvest",
        include_str!("../../blueprints/automatic-harvest.json"),
    ),
    ("job-harvest", include_str!("../../blueprints/job-harvest.json")),
    ("hunt", include_str!("../../blueprints/hunt.json")),
];

/// D3B's blueprints, in the Library's order. Their tests hold them readable and without problems, so a failure here is
/// a bug of this version: it is returned rather than hidden.
pub(crate) fn given() -> Result<Vec<Blueprint>, ReadError> {
    GIVEN.iter().map(|(_, text)| json::read(text)).collect()
}

#[cfg(test)]
mod tests {
    use std::collections::HashSet;

    use super::*;
    use crate::bot::problems::problems;

    #[test]
    fn every_given_blueprint_reads_and_has_no_problem() {
        for (name, text) in GIVEN {
            let blueprint = json::read(text).unwrap_or_else(|error| panic!("{name}: {error}"));
            assert_eq!(problems(&blueprint), [], "{name}");
            assert_eq!(blueprint.id.0, *name, "a file named by its id");
            assert!(blueprint.based_on.is_none(), "{name} is no copy");
        }
    }

    #[test]
    fn the_given_blueprints_are_three_with_distinct_ids() {
        let blueprints = given().unwrap_or_else(|error| panic!("{error}"));
        let ids: HashSet<_> = blueprints.iter().map(|blueprint| blueprint.id.0.as_str()).collect();
        assert_eq!(ids, HashSet::from(["automatic-harvest", "job-harvest", "hunt"]));
    }

    #[test]
    fn every_setting_of_a_given_blueprint_reads_in_each_language() {
        let locales = [
            include_str!("../../../src/i18n/locales/en.json"),
            include_str!("../../../src/i18n/locales/fr.json"),
        ];
        for text in locales {
            let locale: serde_json::Value = serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"));
            for blueprint in given().unwrap_or_else(|error| panic!("{error}")) {
                let texts = &locale["given"][&blueprint.id.0];
                let filled = |value: &serde_json::Value| value.as_str().is_some_and(|text| !text.is_empty());
                assert!(
                    filled(&texts["name"]) && filled(&texts["description"]),
                    "{}",
                    blueprint.id.0
                );
                for variable in &blueprint.variables {
                    let texts = &texts["variables"][&variable.id.0];
                    assert!(
                        filled(&texts["name"]) && filled(&texts["help"]),
                        "{}: {}",
                        blueprint.id.0,
                        variable.id.0
                    );
                }
            }
        }
    }
}
