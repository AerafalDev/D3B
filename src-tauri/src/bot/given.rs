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
    use crate::bot::blueprint::{Does, End, Graph, Node};
    use crate::bot::problems::problems;

    fn all() -> Vec<Blueprint> {
        given().unwrap_or_else(|error| panic!("{error}"))
    }

    fn kind_of(node: &Node) -> &str {
        match &node.does {
            Does::Kind(kind) => &kind.0,
            _ => "",
        }
    }

    /// The node the flow goes to from `node`'s output `pin`.
    fn next<'a>(graph: &'a Graph, node: &Node, pin: &str) -> &'a Node {
        let wire = graph
            .wires
            .iter()
            .find(|wire| wire.from.0 == node.id && wire.from.1.0 == pin);
        let to = wire.unwrap_or_else(|| panic!("{} leads nowhere by {pin}", node.id.0));
        graph
            .nodes
            .iter()
            .find(|node| node.id == to.to.0)
            .unwrap_or_else(|| panic!("no {}", to.to.0.0))
    }

    fn the<'a>(graph: &'a Graph, kind: &str) -> &'a Node {
        graph
            .nodes
            .iter()
            .find(|node| kind_of(node) == kind)
            .unwrap_or_else(|| panic!("no {kind}"))
    }

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
    #[test]
    fn each_goes_to_its_zone_works_until_its_pods_banks_deposits_and_starts_again() {
        for blueprint in all() {
            let graph = &blueprint.graphs[0];
            let go = next(graph, the(graph, "on_start"), "then");
            let work = next(graph, go, "done");
            let to_bank = next(graph, work, "done");
            let deposit = next(graph, to_bank, "done");
            let kinds = [go, work, to_bank, deposit].map(kind_of);
            let work_kind = if blueprint.id.0 == "hunt" {
                "hunt_until"
            } else {
                "harvest_until"
            };
            assert_eq!(
                kinds,
                ["go_to_zone", work_kind, "go_to_bank", "deposit_all_but"],
                "{}",
                blueprint.id.0
            );
            assert_eq!(
                next(graph, deposit, "done").id,
                go.id,
                "{} starts again",
                blueprint.id.0
            );
        }
    }

    #[test]
    fn each_setting_reaches_an_input_and_a_profession_chooses_what_is_harvested() {
        for blueprint in all() {
            let graph = &blueprint.graphs[0];
            for variable in blueprint.variables.iter().filter(|variable| variable.per_character) {
                let get = graph
                    .nodes
                    .iter()
                    .find(|node| node.does == Does::Get(variable.id.clone()));
                let get = get.unwrap_or_else(|| panic!("{}: {} is read nowhere", blueprint.id.0, variable.id.0));
                assert!(
                    graph.wires.iter().any(|wire| wire.from.0 == get.id),
                    "{}: {}",
                    blueprint.id.0,
                    variable.id.0
                );
            }
        }
        let blueprints = all();
        let graph = &blueprints
            .iter()
            .find(|blueprint| blueprint.id.0 == "job-harvest")
            .map(|it| &it.graphs[0]);
        let graph = graph.unwrap_or_else(|| panic!("no job-harvest"));
        let (resources, harvest) = (the(graph, "job_resources"), the(graph, "harvest_until"));
        let fed = graph
            .wires
            .iter()
            .any(|wire| wire.from.0 == resources.id && wire.to == End(harvest.id.clone(), what()));
        assert!(fed, "the profession's resources are what is harvested");
    }

    fn what() -> crate::bot::Id {
        crate::bot::Id("what".to_owned())
    }

    #[test]
    fn each_waits_for_life_at_90_when_it_falls_below_40() {
        for blueprint in all() {
            let graph = &blueprint.graphs[0];
            let life = the(graph, "life_below");
            let input = |node: &Node, pin: &str| node.inputs.get(&crate::bot::Id(pin.to_owned())).cloned();
            assert_eq!(
                (input(life, "threshold"), input(life, "rearm")),
                (Some(40.into()), Some(60.into()))
            );
            let wait = next(graph, life, "then");
            assert_eq!(
                (kind_of(wait), input(wait, "life")),
                ("wait_for_life", Some(90.into())),
                "{}",
                blueprint.id.0
            );
        }
    }

    #[test]
    fn every_file_of_the_folder_is_given_and_named_by_its_id() {
        let folder = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("blueprints");
        let files: HashSet<String> = std::fs::read_dir(folder)
            .into_iter()
            .flatten()
            .flatten()
            .filter_map(|entry| entry.file_name().to_str().map(str::to_owned))
            .collect();
        let named: HashSet<String> = all()
            .iter()
            .map(|blueprint| format!("{}.json", blueprint.id.0))
            .collect();
        assert_eq!(files, named);
    }

    #[test]
    fn the_files_english_is_the_english_translation() {
        let locale: serde_json::Value = serde_json::from_str(include_str!("../../../src/i18n/locales/en.json"))
            .unwrap_or_else(|error| panic!("{error}"));
        for blueprint in all() {
            let texts = &locale["given"][&blueprint.id.0];
            assert_eq!(
                (texts["name"].as_str(), texts["description"].as_str()),
                (Some(blueprint.name.as_str()), Some(blueprint.description.as_str()))
            );
            for variable in &blueprint.variables {
                let texts = &texts["variables"][&variable.id.0];
                assert_eq!(
                    (texts["name"].as_str(), texts["help"].as_str()),
                    (Some(variable.name.as_str()), Some(variable.help.as_str()))
                );
            }
        }
    }
}
