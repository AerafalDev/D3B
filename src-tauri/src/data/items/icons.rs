use std::collections::HashSet;

use super::super::DataError;
use super::super::manifest::Manifest;
use super::{Item, ItemTables, Job};

/// The `iconId` of a job without an icon (job 1, Base, on 3.7.13.17).
pub const NO_ICON: i32 = -1;

/// Whose icon: an item's or a job's, each in its own folder of the data, as 32 px PNG files (verified on 3.7.13.17,
/// `artifacts/studies/items.md`).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Icons {
    Items,
    Jobs,
}

impl Icons {
    /// The folder of the icons of this kind, as the manifest's names and paths start.
    fn folder(self) -> &'static str {
        match self {
            Self::Items => "icons/items/",
            Self::Jobs => "icons/jobs/",
        }
    }

    /// The name, in the manifest, of the icon `icon`, such as `icons/items/303`.
    pub fn name(self, icon: i32) -> String {
        format!("{}{icon}", self.folder())
    }

    /// The path, relative to the data folder, of the icon `icon`, which the webview loads through the asset protocol.
    pub fn file(self, icon: i32) -> String {
        format!("{}.png", self.name(icon))
    }
}

/// The icons of `kind` that `manifest` lists at their [`Icons::file`].
pub fn listed(manifest: &Manifest, kind: Icons) -> HashSet<i32> {
    manifest
        .tables
        .iter()
        .filter_map(|table| {
            let icon = table.name.strip_prefix(kind.folder())?.parse().ok()?;
            (kind.name(icon) == table.name && kind.file(icon) == table.path).then_some(icon)
        })
        .collect()
}

/// Refuses the jobs when one names an icon `manifest` lacks. An item may lack its own: four of 3.7.13.17 name an icon
/// the client lacks, and the extractor writes them as they are.
pub fn check(jobs: &[Job], manifest: &Manifest) -> Result<(), DataError> {
    let icons = listed(manifest, Icons::Jobs);
    match jobs
        .iter()
        .find(|job| job.icon_id != NO_ICON && !icons.contains(&job.icon_id))
    {
        Some(job) => Err(DataError::MissingTable(Icons::Jobs.name(job.icon_id))),
        None => Ok(()),
    }
}

impl ItemTables {
    /// The file of `item`'s icon, relative to the data folder, or `None` when the folder lacks it.
    pub fn item_icon(&self, item: &Item) -> Option<String> {
        self.item_icons
            .contains(&item.icon_id)
            .then(|| Icons::Items.file(item.icon_id))
    }

    /// The file of `job`'s icon, relative to the data folder, or `None` for a job without one: the folder holds every
    /// other job's.
    pub fn job_icon(job: &Job) -> Option<String> {
        (job.icon_id != NO_ICON).then(|| Icons::Jobs.file(job.icon_id))
    }
}

#[cfg(test)]
mod tests {
    use super::super::tests::fixtures;
    use super::*;

    /// A manifest listing `files`, paths of the data folder each named by its path without `.png`.
    fn manifest(files: &[&str]) -> Manifest {
        let tables: Vec<String> = files
            .iter()
            .map(|file| {
                let name = file.trim_end_matches(".png");
                format!(r#"{{"name":"{name}","path":"{file}","sha256":"00"}}"#)
            })
            .collect();
        let text = format!(
            r#"{{"format":{},"build":"3.7.13.17","extractor":"0","tables":[{}]}}"#,
            super::super::super::FORMAT,
            tables.join(",")
        );
        Manifest::parse(&text).unwrap_or_else(|error| panic!("{error}"))
    }

    #[test]
    fn names_an_icon_by_its_kind_and_number() {
        assert_eq!(Icons::Items.name(303), "icons/items/303");
        assert_eq!(Icons::Items.file(0), "icons/items/0.png");
        assert_eq!(Icons::Jobs.file(42), "icons/jobs/42.png");
    }

    #[test]
    fn lists_the_icons_of_one_kind_at_their_paths() {
        let manifest = manifest(&[
            "icons/items/0.png",
            "icons/items/303.png",
            "icons/items/007.png",
            "icons/items/7.jpg",
            "icons/items/x.png",
            "icons/jobs/1.png",
            "items.png",
        ]);
        assert_eq!(listed(&manifest, Icons::Items), HashSet::from([0, 303]));
        assert_eq!(listed(&manifest, Icons::Jobs), HashSet::from([1]));
    }

    #[test]
    fn reads_jobs_whose_icons_the_folder_holds() {
        // The fixtures' Base has no icon; the lumberjack's is 1.
        let jobs = fixtures().jobs;
        assert!(check(&jobs, &manifest(&["icons/jobs/1.png"])).is_ok());
        assert_eq!(ItemTables::job_icon(&jobs[0]), None);
        assert_eq!(ItemTables::job_icon(&jobs[1]).as_deref(), Some("icons/jobs/1.png"));
    }

    #[test]
    fn refuses_a_job_whose_icon_the_folder_lacks() {
        let jobs = fixtures().jobs;
        assert!(matches!(
            check(&jobs, &manifest(&["icons/items/1.png", "icons/jobs/1.jpg"])),
            Err(DataError::MissingTable(name)) if name == "icons/jobs/1"
        ));
    }

    #[test]
    fn an_item_whose_icon_the_folder_lacks_has_none() {
        let mut tables = fixtures();
        tables.item_icons = listed(&manifest(&["icons/items/42011.png"]), Icons::Items);
        assert_eq!(
            tables.item_icon(&tables.items[0]).as_deref(),
            Some("icons/items/42011.png")
        );
        assert_eq!(tables.item_icon(&tables.items[1]), None);
    }
}
