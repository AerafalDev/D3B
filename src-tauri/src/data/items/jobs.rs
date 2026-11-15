use std::collections::{BTreeSet, HashMap, HashSet};

use serde::Deserialize;

use super::super::DataError;
use super::super::text::Text;
use super::{Item, ItemTables};

/// A job, such as the lumberjack. Those whose skills gather what a sub-area's `harvestables` lists are the harvesting
/// professions: 2, 24, 26, 28 and 36 on 3.7.13.17 (verified, `artifacts/studies/items.md`).
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Job {
    pub id: i32,
    pub name: Text,
    /// Its icon's number, -1 for none: job 1, Base (verified on 3.7.13.17, `artifacts/studies/items.md`).
    pub icon_id: i32,
}

/// A skill of a job, such as cutting an ash tree.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Skill {
    pub id: i32,
    pub name: Text,
    pub parent_job_id: i32,
    /// The item it gathers, the client's `gatheredRessourceItem`, or `None` when it gathers nothing (-1 or 0 there).
    pub gathered_item_id: Option<i32>,
    /// The job's level it needs; a resource's item has the same level (verified on 3.7.13.17,
    /// `artifacts/studies/items.md`).
    pub level_min: i32,
    /// The distance it is used from, in cells: 1, or 10 for fishing (the C# version measured the server allowing 10 on
    /// 3.7.12.12; inferred for 3.7.13.17, `artifacts/studies/items.md`).
    pub range: i32,
    /// Whether the client shows it: five level-1 harvest skills repeat another with `false`, such as 299 beside 6
    /// (verified on 3.7.13.17, `artifacts/studies/items.md`); which one the server offers is not in the data.
    pub client_display: bool,
}

/// A resource: an item a job's skill gathers.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Resource<'a> {
    pub item: &'a Item,
    pub job: &'a Job,
}

impl ItemTables {
    /// The resources among a sub-area's `harvestables`, once each, in order of job, then level, then id. The list also
    /// holds items no skill gathers, which are not resources: Forêt d'Astrub (97) lists 45 items, 30 of them loot boxes,
    /// and 15 resources of the lumberjack and the alchemist (verified on 3.7.13.17's data).
    pub fn resources(&self, harvestables: &[i32]) -> Vec<Resource<'_>> {
        // The job of the first skill, by id, that gathers each item.
        let mut gatherers: HashMap<i32, i32> = HashMap::new();
        for skill in &self.skills {
            if let Some(item) = skill.gathered_item_id {
                gatherers.entry(item).or_insert(skill.parent_job_id);
            }
        }
        let item = |id: i32| {
            self.items
                .binary_search_by_key(&id, |item| item.id)
                .ok()
                .map(|at| &self.items[at])
        };
        let job = |id: i32| {
            self.jobs
                .binary_search_by_key(&id, |job| job.id)
                .ok()
                .map(|at| &self.jobs[at])
        };
        let mut found: Vec<Resource<'_>> = harvestables
            .iter()
            .collect::<BTreeSet<_>>()
            .into_iter()
            .filter_map(|&id| {
                Some(Resource {
                    item: item(id)?,
                    job: job(*gatherers.get(&id)?)?,
                })
            })
            .collect();
        found.sort_by_key(|resource| (resource.job.id, resource.item.level, resource.item.id));
        found
    }
}

/// Refuses the skills when one is of a job that `jobs` lacks, or gathers an item that `items` lacks.
pub fn check(skills: &[Skill], jobs: &HashSet<i32>, items: &HashSet<i32>) -> Result<(), DataError> {
    let missing = |skill: &Skill, field, target| DataError::Reference {
        record: format!("skill {}", skill.id),
        field,
        target,
    };
    for skill in skills {
        if !jobs.contains(&skill.parent_job_id) {
            return Err(missing(skill, "parentJobId", skill.parent_job_id));
        }
        if let Some(item) = skill.gathered_item_id.filter(|item| !items.contains(item)) {
            return Err(missing(skill, "gatheredItemId", item));
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::super::tests::fixtures;
    use super::*;

    #[test]
    fn reads_the_jobs_and_skills_the_extractor_writes() {
        let tables = fixtures().checked().unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            (tables.jobs[1].name.en.as_str(), tables.jobs[1].icon_id),
            ("Lumberjack", 1)
        );
        assert_eq!(tables.skills[0].gathered_item_id, Some(303));
        assert!(tables.skills[0].client_display);
        assert_eq!(tables.skills[1].name.en, "Gather");
        assert_eq!(tables.skills[1].gathered_item_id, None);
        assert_eq!(tables.skills[1].range, 10);
    }

    #[test]
    fn gives_the_resources_a_job_gathers_among_harvestables_by_job_then_level() {
        let mut tables = fixtures();
        let farmer = Job {
            id: 28,
            ..tables.jobs[1].clone()
        };
        tables.jobs.push(farmer);
        tables.skills.push(Skill {
            id: 45,
            parent_job_id: 28,
            gathered_item_id: Some(289),
            ..tables.skills[0].clone()
        });
        let found = |tables: &ItemTables, harvestables: &[i32]| -> Vec<(i32, i32)> {
            tables
                .resources(harvestables)
                .iter()
                .map(|resource| (resource.item.id, resource.job.id))
                .collect()
        };
        // The lumberjack's wood before the farmer's wheat, though listed after it; once each.
        assert_eq!(found(&tables, &[289, 303, 303]), [(303, 2), (289, 28)]);
        // An item no skill gathers, as the loot boxes, and one the folder lacks are not resources.
        tables.skills.pop();
        assert_eq!(found(&tables, &[289, 303, 999]), [(303, 2)]);
        assert_eq!(found(&tables, &[]), []);
    }

    #[test]
    fn refuses_a_skill_of_a_job_the_folder_lacks() {
        let mut tables = fixtures();
        tables.jobs.remove(1);
        assert!(matches!(
            tables.checked(),
            Err(DataError::Reference { record, field: "parentJobId", target: 2 }) if record == "skill 6"
        ));
    }

    #[test]
    fn refuses_a_skill_gathering_an_item_the_folder_lacks() {
        let mut tables = fixtures();
        tables.skills[0].gathered_item_id = Some(3656);
        assert!(matches!(
            tables.checked(),
            Err(DataError::Reference { record, field: "gatheredItemId", target: 3656 }) if record == "skill 6"
        ));
    }
}
