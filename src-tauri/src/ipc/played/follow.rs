//! My blueprint's variables changed in its editor (step 3's (14), the owner's choices of 2026-11-17): each character
//! running it keeps the values of its settings that still fit, as when it chooses another blueprint (`bot::carried`),
//! and drops the others: a variable deleted, retyped, out of its new bounds, no longer set per character, or a value
//! now its default.

use crate::bot::{self, Variable};
use crate::storage::{StorageError, Store};

/// Carries the settings of every character running my blueprint `id` from its variables `before` to `after`; whether
/// any character's file changed. Nothing is carried when the variables are the same.
pub(crate) fn follow(store: &Store, id: &str, before: &[Variable], after: &[Variable]) -> bool {
    if before == after {
        return false;
    }
    let mut any = false;
    for character in store.played().read {
        if character.blueprint.as_deref() != Some(id) {
            continue;
        }
        let mut changed = false;
        let done = store.change_character(&character.key(), |character| {
            // Read again under the lock: the player may have chosen another blueprint since.
            if character.blueprint.as_deref() == Some(id) {
                let kept = bot::carried(&character.settings, Some(before), after);
                changed = kept != character.settings;
                character.settings = kept;
            }
            Ok::<(), StorageError>(())
        });
        match done {
            Ok(()) => any |= changed,
            Err(error) => tauri_plugin_log::log::warn!(
                "{}'s settings do not follow its blueprint's variables: {error}",
                character.name
            ),
        }
    }
    any
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use serde_json::json;

    use super::*;
    use crate::bot::ValueType;
    use crate::storage::tests::Folder;
    use crate::storage::{CHARACTER_SCHEMA, Played};

    fn played(id: i64, blueprint: &str) -> Played {
        Played {
            schema: CHARACTER_SCHEMA,
            id,
            name: format!("c{id}"),
            level: 54,
            breed: 8,
            sex: None,
            last_seen: 1,
            map_id: None,
            blueprint: Some(blueprint.into()),
            settings: BTreeMap::from([
                ("pods".to_owned(), json!(80)),
                ("zone".to_owned(), json!({"sub_area": 445})),
            ]),
            blueprint_version: None,
            dropped: Vec::new(),
        }
    }

    fn variables() -> Vec<Variable> {
        let text = bot::HARVEST.replacen("automatic-harvest", "my-harvest", 1);
        bot::read(&text).unwrap_or_else(|error| panic!("{error}")).variables
    }

    fn settings(store: &Store, id: i64) -> Vec<String> {
        let played = store.played().read;
        let character = played.iter().find(|it| it.id == id);
        character
            .map(|it| it.settings.keys().cloned().collect())
            .unwrap_or_default()
    }

    #[test]
    fn the_characters_running_it_keep_what_still_fits_and_the_others_are_untouched() {
        let folder = Folder::new("ipc-follow");
        let store = Store::new(folder.path().to_path_buf());
        for character in [played(1, "my-harvest"), played(2, "other")] {
            store.remember(&character).unwrap_or_else(|error| panic!("{error}"));
        }
        let before = variables();
        assert!(
            !follow(&store, "my-harvest", &before, &before),
            "the same variables carry nothing"
        );
        let mut after = before.clone();
        after[1].value_type = ValueType::Number;
        assert!(follow(&store, "my-harvest", &before, &after));
        assert_eq!(settings(&store, 1), ["zone"], "pods retyped goes");
        assert_eq!(
            settings(&store, 2),
            ["pods", "zone"],
            "another blueprint's character keeps its own"
        );
        after.remove(0);
        assert!(follow(&store, "my-harvest", &before, &after));
        assert_eq!(settings(&store, 1), Vec::<String>::new(), "zone deleted goes");
        assert!(
            !follow(&store, "my-harvest", &after, &after[..1]),
            "nothing left to drop"
        );
    }
}
