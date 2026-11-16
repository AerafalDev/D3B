//! Undo and redo of the editor's changes to a blueprint of mine (step 3's PR 13, the owner's choice of 2026-11-16):
//! the blueprint as it was before each change, kept by Rust for the session, across its graphs, so that the file and
//! the editor never disagree. Undo and redo write the file whole, under the lock every save takes.

use std::collections::VecDeque;

use super::store::{StorageError, Store};
use crate::bot::Blueprint;

/// How many changes are undone at most; the oldest goes past them.
const STEPS: usize = 100;

/// A blueprint's states before the changes undone, the latest last, and after those undone, the latest undone last.
#[derive(Debug, Default)]
pub(crate) struct History {
    past: VecDeque<Blueprint>,
    future: Vec<Blueprint>,
}

impl History {
    /// A change made from `before`: the changes undone are no longer redone.
    pub(super) fn record(&mut self, before: Blueprint) {
        if self.past.len() == STEPS {
            self.past.pop_front();
        }
        self.past.push_back(before);
        self.future.clear();
    }

    /// The state undo, or redo, brings back.
    fn next(&self, back: bool) -> Option<&Blueprint> {
        if back { self.past.back() } else { self.future.last() }
    }

    /// Undo, or redo, was written over `now`, which the other one then brings back.
    fn went(&mut self, back: bool, now: Blueprint) {
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
        let history = histories.entry(id).or_default();
        let Some(then) = history.next(back).cloned() else {
            return Ok(now);
        };
        self.save_while_locked(&then)?;
        history.went(back, now);
        Ok(then)
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
}
