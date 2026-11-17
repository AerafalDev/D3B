//! Undo and redo, kept by Rust for the session so that the files and the app never disagree, written under the lock
//! every save takes: of the editor's changes to a blueprint of mine (step 3's PR 13, the owner's choice of 2026-11-16),
//! the blueprint as it was before each change, across its graphs; and of the changes to my zones on the World (the
//! owner's choices of 2026-11-17), one history for them all: painted, erased, renamed, created and deleted.

use std::collections::{HashMap, VecDeque};
use std::io;
use std::path::Path;

use super::store::{StorageError, Store};
use super::zone::Zone;
use crate::bot::{Blueprint, Id};

/// How many changes are undone at most; the oldest goes past them.
const STEPS: usize = 100;

/// The states before the changes undone, the latest last, and after those undone, the latest undone last.
#[derive(Debug)]
pub(crate) struct History<T> {
    past: VecDeque<T>,
    future: Vec<T>,
}

impl<T> Default for History<T> {
    fn default() -> Self {
        Self {
            past: VecDeque::new(),
            future: Vec::new(),
        }
    }
}

/// A zone of mine as it was, `None` while it was not there: before it was created, or after it was deleted.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct ZoneState {
    pub(crate) id: Id,
    pub(crate) zone: Option<Zone>,
}

/// The undo histories the lock holds: each blueprint of mine's, and my zones' together.
#[derive(Debug, Default)]
pub(crate) struct Histories {
    pub(super) blueprints: HashMap<Id, History<Blueprint>>,
    pub(super) zones: History<ZoneState>,
}

impl<T> History<T> {
    /// A change made from `before`: the changes undone are no longer redone.
    pub(super) fn record(&mut self, before: T) {
        if self.past.len() == STEPS {
            self.past.pop_front();
        }
        self.past.push_back(before);
        self.future.clear();
    }

    /// The state undo, or redo, brings back.
    fn next(&self, back: bool) -> Option<&T> {
        if back { self.past.back() } else { self.future.last() }
    }

    /// Undo, or redo, was written over `now`, which the other one then brings back.
    fn went(&mut self, back: bool, now: T) {
        if back {
            self.past.pop_back();
            self.future.push(now);
        } else {
            self.future.pop();
            self.past.push_back(now);
        }
    }
}

impl Store {
    /// Undo: my blueprint `id` as it was before its last change not undone, written; as it is when there is none.
    pub(crate) fn undo_blueprint(&self, id: &str) -> Result<Blueprint, StorageError> {
        self.step(id, true)
    }

    /// Redo: my blueprint `id` as it was before its last undo since its last change, written; as it is when there is
    /// none.
    pub(crate) fn redo_blueprint(&self, id: &str) -> Result<Blueprint, StorageError> {
        self.step(id, false)
    }

    fn step(&self, id: &str, back: bool) -> Result<Blueprint, StorageError> {
        let id = Self::mine(id)?;
        let mut histories = self.lock();
        let now = self.read_mine(&id)?;
        let history = histories.blueprints.entry(id).or_default();
        let Some(then) = history.next(back).cloned() else {
            return Ok(now);
        };
        self.save_while_locked(&then)?;
        history.went(back, now);
        Ok(then)
    }

    /// Undo of my zones' last change not undone: the zone it changed as it was before, written, or sent to `discard`
    /// (the system's trash in the app) when it was not there; `None` when there is nothing to undo.
    pub(crate) fn undo_zone(
        &self,
        discard: impl FnOnce(&Path) -> io::Result<()>,
    ) -> Result<Option<ZoneState>, StorageError> {
        self.step_zone(true, discard)
    }

    /// Redo of my zones' last undo since their last change: the zone as that undo found it, written, or sent to
    /// `discard`; `None` when there is nothing to redo.
    pub(crate) fn redo_zone(
        &self,
        discard: impl FnOnce(&Path) -> io::Result<()>,
    ) -> Result<Option<ZoneState>, StorageError> {
        self.step_zone(false, discard)
    }

    fn step_zone(
        &self,
        back: bool,
        discard: impl FnOnce(&Path) -> io::Result<()>,
    ) -> Result<Option<ZoneState>, StorageError> {
        let mut histories = self.lock();
        let Some(then) = histories.zones.next(back).cloned() else {
            return Ok(None);
        };
        // What the file holds now, which the other one brings back: the player's hand may have changed it.
        let now = ZoneState {
            zone: self.zone_while_locked(&then.id.0)?,
            id: then.id.clone(),
        };
        match &then.zone {
            Some(zone) => self.write_zone_while_locked(zone)?,
            None if now.zone.is_some() => {
                let path = self.zone_path(&then.id.0)?;
                discard(&path).map_err(|source| StorageError::Write { path, source })?;
            }
            None => {}
        }
        histories.zones.went(back, now);
        Ok(Some(then))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot;
    use crate::storage::tests::Folder;

    fn mine(name: &str) -> (Folder, Store) {
        let folder = Folder::new(name);
        let store = Store::new(folder.path().to_path_buf());
        let text = bot::HARVEST.replacen("automatic-harvest", "my-harvest", 1);
        let blueprint = bot::read(&text).unwrap_or_else(|error| panic!("{error}"));
        store
            .save_blueprint(&blueprint)
            .unwrap_or_else(|error| panic!("{error}"));
        (folder, store)
    }

    fn rename(store: &Store, name: &str) {
        let renamed = store.change_blueprint("my-harvest", |blueprint| {
            blueprint.name = name.into();
            Ok::<_, StorageError>(())
        });
        assert!(renamed.is_ok(), "{renamed:?}");
    }

    /// The name of my blueprint as its file holds it.
    fn written(store: &Store) -> String {
        store.blueprints().read[0].name.clone()
    }

    fn name(stepped: Result<Blueprint, StorageError>) -> String {
        stepped.map_or_else(|error| panic!("{error}"), |it| it.name)
    }

    #[test]
    fn undo_writes_the_blueprint_before_each_change_and_redo_the_one_after() {
        let (_folder, store) = mine("history-steps");
        rename(&store, "A");
        rename(&store, "B");
        assert_eq!(name(store.undo_blueprint("my-harvest")), "A");
        assert_eq!(written(&store), "A");
        assert_eq!(name(store.undo_blueprint("my-harvest")), "Automatic harvest");
        assert_eq!(
            name(store.undo_blueprint("my-harvest")),
            "Automatic harvest",
            "nothing more to undo"
        );
        assert_eq!(name(store.redo_blueprint("my-harvest")), "A");
        assert_eq!(written(&store), "A");
        rename(&store, "C");
        assert_eq!(
            name(store.redo_blueprint("my-harvest")),
            "C",
            "a change ends what redo brings back"
        );
        assert_eq!(name(store.undo_blueprint("my-harvest")), "A");
    }

    #[test]
    fn a_change_that_changes_nothing_is_no_step() {
        let (_folder, store) = mine("history-same");
        rename(&store, "A");
        rename(&store, "A");
        assert_eq!(name(store.undo_blueprint("my-harvest")), "Automatic harvest");
    }

    #[test]
    fn a_hundred_changes_are_undone_and_no_more() {
        let mut history = History::default();
        let blueprint = bot::read(bot::HARVEST).unwrap_or_else(|error| panic!("{error}"));
        for number in 0..=STEPS {
            let mut before = blueprint.clone();
            before.name = number.to_string();
            history.record(before);
        }
        let mut names = Vec::new();
        while let Some(then) = history.next(true).cloned() {
            names.push(then.name);
            history.went(true, blueprint.clone());
        }
        assert_eq!(names.len(), STEPS);
        assert_eq!(names.last().map(String::as_str), Some("1"), "the oldest went past them");
    }

    #[test]
    fn undo_of_one_of_d3bs_or_of_one_gone_is_refused() {
        let (folder, store) = mine("history-refused");
        rename(&store, "A");
        let given = store.undo_blueprint("automatic-harvest");
        assert!(matches!(given, Err(StorageError::GivenId { .. })), "{given:?}");
        std::fs::remove_file(folder.path().join("blueprints/my-harvest.json"))
            .unwrap_or_else(|error| panic!("{error}"));
        let gone = store.undo_blueprint("my-harvest");
        assert!(matches!(gone, Err(StorageError::NoSuchBlueprint { .. })), "{gone:?}");
        assert!(store.blueprints().read.is_empty(), "nothing written");
    }

    #[test]
    fn a_change_that_changes_nothing_writes_nothing() {
        let (folder, store) = mine("history-unwritten");
        let path = folder.path().join("blueprints/my-harvest.json");
        // The fixture as the player's hand wrote it, which a save would write in D3B's own layout.
        let by_hand = bot::HARVEST.replacen("automatic-harvest", "my-harvest", 1);
        std::fs::write(&path, &by_hand).unwrap_or_else(|error| panic!("{error}"));
        let same = store.change_blueprint("my-harvest", |_| Ok::<_, StorageError>(()));
        assert!(same.is_ok(), "{same:?}");
        assert_eq!(std::fs::read_to_string(&path).ok(), Some(by_hand));
    }

    #[test]
    fn undo_over_a_file_changed_by_hand_keeps_the_hand_for_redo() {
        let (folder, store) = mine("history-hand");
        rename(&store, "A");
        let mut by_hand = store.blueprints().read.remove(0);
        by_hand.name = "Hand".into();
        let path = folder.path().join("blueprints/my-harvest.json");
        std::fs::write(&path, bot::write(&by_hand)).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(name(store.undo_blueprint("my-harvest")), "Automatic harvest");
        assert_eq!(name(store.redo_blueprint("my-harvest")), "Hand");
    }

    #[test]
    fn an_undo_not_written_keeps_its_step() {
        let (folder, store) = mine("history-unsaved");
        rename(&store, "A");
        let path = folder.path().join("blueprints/my-harvest.json");
        let written = std::fs::read_to_string(&path).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(&path, "not json").unwrap_or_else(|error| panic!("{error}"));
        let kept = store.undo_blueprint("my-harvest");
        assert!(matches!(kept, Err(StorageError::Kept { .. })), "{kept:?}");
        assert_eq!(
            std::fs::read_to_string(&path).ok().as_deref(),
            Some("not json"),
            "never written over"
        );
        std::fs::write(&path, written).unwrap_or_else(|error| panic!("{error}"));
        // A step whose state cannot be written: its id is malformed, so no file is named by it.
        let mut unwritable = store.blueprints().read.remove(0);
        unwritable.id = crate::bot::Id("Not An Id".into());
        store
            .lock()
            .blueprints
            .entry(crate::bot::Id("my-harvest".into()))
            .or_default()
            .record(unwritable);
        let refused = store.undo_blueprint("my-harvest");
        assert!(matches!(refused, Err(StorageError::MalformedId { .. })), "{refused:?}");
        assert_eq!(name(store.redo_blueprint("my-harvest")), "A", "nothing went to redo");
        assert!(store.undo_blueprint("my-harvest").is_err(), "the step is still there");
    }

    /// A zone's file sent away, as the trash does, its path kept.
    fn trash(gone: &mut Vec<std::path::PathBuf>) -> impl FnOnce(&Path) -> io::Result<()> + '_ {
        |path| {
            gone.push(path.to_path_buf());
            std::fs::remove_file(path)
        }
    }

    fn paint(store: &Store, id: &str, maps: &[[i16; 2]]) {
        let painted = store.change_zone(id, |zone| {
            zone.maps.extend(maps);
            Ok::<_, StorageError>(())
        });
        assert!(painted.is_ok(), "{painted:?}");
    }

    fn rename_zone(store: &Store, id: &str, name: &str) {
        let renamed = store.change_zone(id, |zone| {
            name.clone_into(&mut zone.name);
            Ok::<_, StorageError>(())
        });
        assert!(renamed.is_ok(), "{renamed:?}");
    }

    /// The zones as their files hold them: each id, name and number of maps.
    fn zones(store: &Store) -> Vec<(String, String, usize)> {
        let read = store.zones().read.into_iter();
        read.map(|zone| (zone.id.0, zone.name, zone.maps.len())).collect()
    }

    fn zone(id: &str, name: &str, maps: usize) -> (String, String, usize) {
        (id.into(), name.into(), maps)
    }

    /// The id a step changed and whether the zone is there after it, or `None` when there was nothing to step.
    fn stepped(step: Result<Option<ZoneState>, StorageError>) -> Option<(String, bool)> {
        let step = step.unwrap_or_else(|error| panic!("{error}"));
        step.map(|state| (state.id.0, state.zone.is_some()))
    }

    #[test]
    fn my_zones_changes_are_undone_and_redone_one_history_for_them_all() {
        let folder = Folder::new("history-zones");
        let store = Store::new(folder.path().to_path_buf());
        let mut gone = Vec::new();
        let created = |name: &str| store.create_zone(name, 1).unwrap_or_else(|error| panic!("{error}"));
        created("Bois");
        paint(&store, "bois", &[[1, 1], [1, 2]]);
        created("Blé");
        rename_zone(&store, "ble", "Champ");
        let deleted = store.delete_zone("bois", trash(&mut gone));
        assert!(deleted.is_ok(), "{deleted:?}");
        assert_eq!(zones(&store), [zone("ble", "Champ", 0)]);

        // Undone from the last, whatever zone each changed: the deletion written back from what it held.
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(zones(&store), [zone("ble", "Champ", 0), zone("bois", "Bois", 2)]);
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("ble".into(), true)));
        assert_eq!(zones(&store)[0], zone("ble", "Blé", 0));
        // A creation undone sends the file to the trash.
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("ble".into(), false)));
        assert_eq!(zones(&store), [zone("bois", "Bois", 2)]);
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), false)));
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), None, "nothing more to undo");
        assert_eq!(zones(&store), []);
        let zones_folder = folder.path().join("zones");
        assert_eq!(
            gone,
            [
                zones_folder.join("bois.json"),
                zones_folder.join("ble.json"),
                zones_folder.join("bois.json")
            ]
        );

        // Redone in order, the creation written again.
        assert_eq!(stepped(store.redo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(stepped(store.redo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(zones(&store), [zone("bois", "Bois", 2)]);
        // A change ends what redo brings back.
        rename_zone(&store, "bois", "Forêt");
        assert_eq!(stepped(store.redo_zone(trash(&mut gone))), None);
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(zones(&store), [zone("bois", "Bois", 2)]);
    }

    #[test]
    fn a_creation_undone_once_its_file_is_gone_by_hand_discards_nothing_nor_does_its_redo() {
        let folder = Folder::new("history-zones-gone");
        let store = Store::new(folder.path().to_path_buf());
        store.create_zone("Bois", 1).unwrap_or_else(|error| panic!("{error}"));
        let path = folder.path().join("zones/bois.json");
        std::fs::remove_file(&path).unwrap_or_else(|error| panic!("{error}"));
        let undone = store.undo_zone(|_| panic!("nothing is left to discard"));
        assert_eq!(stepped(undone), Some(("bois".into(), false)));
        // Redo brings back what undo found: no file, so nothing is written nor discarded.
        let redone = store.redo_zone(|_| panic!("nothing is left to discard"));
        assert_eq!(stepped(redone), Some(("bois".into(), false)));
        assert_eq!(zones(&store), []);
        assert!(!path.exists());
    }

    #[test]
    fn a_zone_change_that_changes_nothing_is_no_step_and_writes_nothing() {
        let folder = Folder::new("history-zones-same");
        let store = Store::new(folder.path().to_path_buf());
        store.create_zone("Bois", 1).unwrap_or_else(|error| panic!("{error}"));
        paint(&store, "bois", &[[1, 1]]);
        let path = folder.path().join("zones/bois.json");
        let written = std::fs::read_to_string(&path).unwrap_or_default();
        // The same map again, and the same name, by a hand that wrote it in its own layout.
        std::fs::write(&path, written.replace('\n', " ")).unwrap_or_else(|error| panic!("{error}"));
        paint(&store, "bois", &[[1, 1]]);
        rename_zone(&store, "bois", "Bois");
        assert_eq!(
            std::fs::read_to_string(&path).ok(),
            Some(written.replace('\n', " ")),
            "never written"
        );
        let mut gone = Vec::new();
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        assert_eq!(
            zones(&store),
            [zone("bois", "Bois", 0)],
            "the paint undone, not the same map again"
        );
    }

    #[test]
    fn a_zone_undo_over_a_file_kept_or_not_trashed_is_refused_and_keeps_its_step() {
        let folder = Folder::new("history-zones-kept");
        let store = Store::new(folder.path().to_path_buf());
        store.create_zone("Bois", 1).unwrap_or_else(|error| panic!("{error}"));
        paint(&store, "bois", &[[1, 1]]);
        let path = folder.path().join("zones/bois.json");
        let written = std::fs::read_to_string(&path).unwrap_or_default();
        std::fs::write(&path, "not json").unwrap_or_else(|error| panic!("{error}"));
        let kept = store.undo_zone(|_| panic!("nothing is discarded"));
        assert!(matches!(kept, Err(StorageError::Kept { .. })), "{kept:?}");
        assert_eq!(
            std::fs::read_to_string(&path).ok().as_deref(),
            Some("not json"),
            "never written over"
        );
        std::fs::write(&path, written).unwrap_or_else(|error| panic!("{error}"));
        let mut gone = Vec::new();
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), true)));
        // A trash that fails leaves the file and the step where they are.
        let refused = store.undo_zone(|_| Err(io::Error::other("no trash")));
        assert!(matches!(refused, Err(StorageError::Write { .. })), "{refused:?}");
        assert_eq!(zones(&store), [zone("bois", "Bois", 0)]);
        assert_eq!(stepped(store.undo_zone(trash(&mut gone))), Some(("bois".into(), false)));
        assert_eq!(zones(&store), []);
    }
}
