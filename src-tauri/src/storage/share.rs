//! The `.d3b` file (ADR 0017): one blueprint shared with the zones and routes it names, as one readable JSON, imported
//! on another D3B after a preview. Its blueprint is its file as `bot` writes it, its zones their files as `zone` writes
//! them; its routes are empty until routes of mine exist (step 3's PR 21), which fill them.

use std::collections::HashSet;
use std::io;
use std::path::Path;

use serde::{Deserialize, Serialize};
use serde_json::value::RawValue;

use super::store::{StorageError, Store, Why};
use super::zone::{self, Zone};
use crate::bot::{self, Blueprint, Id, ReadError};

/// The format this version writes and the newest it reads; a change to the file's shape raises it.
pub(crate) const FORMAT: u32 = 1;

/// The extension a `.d3b` file has, in any case.
pub(crate) const EXTENSION: &str = "d3b";

/// Why a `.d3b` file is not imported.
#[derive(Debug, thiserror::Error)]
pub(crate) enum ShareError {
    /// Its name does not end in `.d3b`: whatever it holds, D3B does not take it for a shared blueprint.
    #[error("it is not a .d3b file")]
    NotD3b,
    #[error("it could not be read: {0}")]
    Unreadable(io::Error),
    #[error("it is not text")]
    NotText,
    /// Written by a newer D3B, of this format.
    #[error("written by a newer D3B: format {found}, and this version reads up to {FORMAT}")]
    Newer { found: u64 },
    /// It is no `.d3b` of this format: not JSON, or not of its shape.
    #[error("not a .d3b file of format {FORMAT}: {0}")]
    NotOfItsShape(serde_json::Error),
    /// Its blueprint does not read: a newer schema, or not a blueprint's shape.
    #[error("its blueprint does not read: {0}")]
    Blueprint(ReadError),
    /// One of its zones does not read: a newer schema, or not a zone's shape.
    #[error("one of its zones does not read: {0}")]
    Zone(Why),
    /// It holds routes, which this version cannot import yet: a newer D3B wrote it.
    #[error("it holds routes, which this version does not import")]
    HoldsRoutes,
}

/// What a `.d3b` shares: a blueprint, and the zones of mine it names.
#[derive(Debug, Clone, PartialEq)]
pub(crate) struct Shared {
    pub(crate) blueprint: Blueprint,
    pub(crate) zones: Vec<Zone>,
}

/// A `.d3b` file as this version writes it; the blueprint and the zones are kept as their own texts when read, so that
/// each is checked as a file of the folder is.
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct File<B, Z> {
    d3b: u32,
    blueprint: B,
    zones: Vec<Z>,
    routes: Vec<serde_json::Value>,
}

/// The text of the `.d3b` file sharing `shared`: indented, ending with a new line.
pub(crate) fn write(shared: &Shared) -> String {
    let file = File {
        d3b: FORMAT,
        blueprint: &shared.blueprint,
        zones: shared.zones.iter().collect(),
        routes: Vec::new(),
    };
    // Only what `bot::write` and a zone's file write, and an empty list: nothing serde_json can refuse.
    let mut text = serde_json::to_string_pretty(&file).unwrap_or_else(|error| unreachable!("{error}"));
    text.push('\n');
    text
}

/// What `text` shares. The format is read first, so that a newer D3B's file is said to be one rather than half read.
pub(crate) fn read(text: &str) -> Result<Shared, ShareError> {
    #[derive(Deserialize)]
    struct Head {
        d3b: Option<serde_json::Value>,
    }
    let head: Head = serde_json::from_str(text).map_err(ShareError::NotOfItsShape)?;
    match head.d3b.as_ref().and_then(serde_json::Value::as_u64) {
        Some(found) if found > u64::from(FORMAT) => return Err(ShareError::Newer { found }),
        Some(found) if found == u64::from(FORMAT) => {}
        _ => {
            let why = format!("its d3b must be {FORMAT}");
            return Err(ShareError::NotOfItsShape(serde::de::Error::custom(why)));
        }
    }
    let file: File<Box<RawValue>, Box<RawValue>> = serde_json::from_str(text).map_err(ShareError::NotOfItsShape)?;
    if !file.routes.is_empty() {
        return Err(ShareError::HoldsRoutes);
    }
    let blueprint = bot::read(file.blueprint.get()).map_err(ShareError::Blueprint)?;
    let zones = file.zones.iter().map(|zone| zone::parse(zone.get()));
    let zones = zones.collect::<Result<_, _>>().map_err(ShareError::Zone)?;
    Ok(Shared { blueprint, zones })
}

/// What the `.d3b` file at `path` shares.
pub(crate) fn read_file(path: &Path) -> Result<Shared, ShareError> {
    let extension = path.extension().and_then(|extension| extension.to_str());
    if !extension.is_some_and(|extension| extension.eq_ignore_ascii_case(EXTENSION)) {
        return Err(ShareError::NotD3b);
    }
    let bytes = std::fs::read(path).map_err(ShareError::Unreadable)?;
    let text = String::from_utf8(bytes).map_err(|_| ShareError::NotText)?;
    read(&text)
}

/// A name for the file sharing a blueprint named `name`: its name, without what a file name cannot hold on some system.
pub(crate) fn file_name(name: &str) -> String {
    let cleaned: String = name
        .chars()
        .map(|character| match character {
            '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*' => '-',
            character if character.is_control() => '-',
            character => character,
        })
        .collect();
    // Windows drops a name's trailing dots and spaces.
    let cleaned = cleaned.trim().trim_end_matches('.');
    let stem = if cleaned.is_empty() { "blueprint" } else { cleaned };
    format!("{stem}.{EXTENSION}")
}

impl Store {
    /// The blueprint `id`, D3B's or mine, as a `.d3b` shares it, with the zones of mine it names. One of D3B's is
    /// shared as mine based on it, with `texts`, the player's words for its translations, under an id made from its
    /// name.
    pub(crate) fn shared(&self, id: &str, texts: Option<bot::Texts>) -> Result<Shared, StorageError> {
        let given = bot::given().unwrap_or_default();
        let blueprint = if let Some(source) = given.iter().find(|blueprint| blueprint.id.0 == id) {
            let given_ids: HashSet<Id> = given.iter().map(|blueprint| blueprint.id.clone()).collect();
            let texts = texts.unwrap_or_else(|| bot::Texts {
                name: source.name.clone(),
                description: source.description.clone(),
                ..bot::Texts::default()
            });
            let id = bot::id_for(&texts.name, "blueprint", &given_ids);
            bot::copy(source, true, id, texts)
        } else {
            let mine = self.blueprints().read;
            mine.into_iter()
                .find(|blueprint| blueprint.id.0 == id)
                .ok_or_else(|| StorageError::NoSuchBlueprint { id: Id(id.to_owned()) })?
        };
        let named = super::share_zones::named(&blueprint);
        let zones = self
            .zones()
            .read
            .into_iter()
            .filter(|zone| named.contains(&zone.id))
            .collect();
        Ok(Shared { blueprint, zones })
    }

    /// Saves what `shared` shares among mine, never over a file: its zones first (one identical to a zone of mine is
    /// that zone, one whose id is taken takes a free one, the blueprint rewritten to name it), then its blueprint under
    /// its own id when no blueprint and no file of the folder holds it, in any case, else under one made from it; the
    /// id it took. Chosen and written under one lock.
    pub(crate) fn import(&self, shared: Shared) -> Result<Id, StorageError> {
        let mut writing = self.lock();
        let Shared { mut blueprint, zones } = shared;
        let renamed = self.add_zones_while_locked(zones)?;
        super::share_zones::rename(&mut blueprint, &renamed);
        let taken = self.taken()?;
        if taken.contains(&blueprint.id) || !blueprint.id.is_well_formed() {
            blueprint.id = bot::id_for(&blueprint.id.0, "blueprint", &taken);
        }
        self.save_while_locked(&blueprint)?;
        // A blueprint deleted by hand may have left its id free, never its undo history to the one imported.
        writing.remove(&blueprint.id);
        Ok(blueprint.id)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::HARVEST;
    use crate::storage::tests::Folder;

    fn mine(id: &str) -> Blueprint {
        let text = HARVEST.replacen("automatic-harvest", id, 1);
        bot::read(&text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn alone(blueprint: Blueprint) -> Shared {
        Shared {
            blueprint,
            zones: Vec::new(),
        }
    }

    fn put(folder: &Folder, name: &str, text: &str) -> std::path::PathBuf {
        let path = folder.path().join(name);
        std::fs::write(&path, text).unwrap_or_else(|error| panic!("{error}"));
        path
    }

    #[test]
    fn a_blueprint_shared_reads_back_the_same_from_its_d3b() {
        let shared = alone(mine("my-harvest"));
        let text = write(&shared);
        assert!(text.starts_with("{\n  \"d3b\": 1,\n  \"blueprint\": {"), "{text}");
        assert!(text.ends_with("\"zones\": [],\n  \"routes\": []\n}\n"), "{text}");
        assert_eq!(read(&text).ok(), Some(shared));
    }

    #[test]
    fn a_d3b_that_is_not_of_this_format_is_refused_with_why() {
        let text = write(&alone(mine("my-harvest")));
        let newer = text.replacen("\"d3b\": 1", "\"d3b\": 2", 1);
        assert!(matches!(read(&newer), Err(ShareError::Newer { found: 2 })));
        for broken in [
            "{",
            "[]",
            "{\"blueprint\": {}}",
            &text.replacen("\"d3b\": 1", "\"d3b\": 0", 1),
            &text.replacen("\"routes\": []", "\"routes\": [], \"extra\": 1", 1),
            &text.replacen("\"routes\": []", "\"routes\": [], \"d3b\": 1", 1),
        ] {
            assert!(matches!(read(broken), Err(ShareError::NotOfItsShape(_))), "{broken}");
        }
        let routes = text.replacen("\"routes\": []", "\"routes\": [{\"id\": \"loop\"}]", 1);
        assert!(matches!(read(&routes), Err(ShareError::HoldsRoutes)));
        let zone = text.replacen("\"zones\": []", "\"zones\": [{\"schema\": 1, \"id\": \"wheat\"}]", 1);
        assert!(matches!(read(&zone), Err(ShareError::Zone(Why::NotAZone(_)))));
        let newer_zone = text.replacen("\"zones\": []", "\"zones\": [{\"schema\": 9}]", 1);
        assert!(matches!(
            read(&newer_zone),
            Err(ShareError::Zone(Why::NewerZone { found: 9 }))
        ));
        let schema = text.replacen("\"schema\": 1", "\"schema\": 2", 1);
        assert!(matches!(
            read(&schema),
            Err(ShareError::Blueprint(ReadError::Newer { found: 2 }))
        ));
        let shape = text.replacen("\"name\": \"Automatic harvest\"", "\"nom\": \"x\"", 1);
        assert!(matches!(
            read(&shape),
            Err(ShareError::Blueprint(ReadError::NotABlueprint(_)))
        ));
    }

    #[test]
    fn only_a_d3b_file_that_reads_as_text_is_taken() {
        let folder = Folder::new("share-file");
        let text = write(&alone(mine("my-harvest")));
        let file = put(&folder, "Ma récolte.D3B", &text);
        assert_eq!(read_file(&file).ok(), Some(alone(mine("my-harvest"))), "in any case");
        let json = put(&folder, "my-harvest.json", &text);
        assert!(matches!(read_file(&json), Err(ShareError::NotD3b)));
        assert!(matches!(
            read_file(&folder.path().join("gone.d3b")),
            Err(ShareError::Unreadable(_))
        ));
        let bytes = folder.path().join("bytes.d3b");
        std::fs::write(&bytes, [0xFF, 0xFE]).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(read_file(&bytes), Err(ShareError::NotText)));
    }

    #[test]
    fn a_file_name_holds_on_every_system() {
        assert_eq!(file_name("Ma récolte"), "Ma récolte.d3b");
        assert_eq!(file_name("A/B: c?*"), "A-B- c--.d3b");
        assert_eq!(file_name(" ... "), "blueprint.d3b");
    }

    #[test]
    fn an_import_takes_its_own_id_when_free_else_a_free_one_never_over_a_file() {
        let folder = Folder::new("share-import");
        let store = Store::new(folder.path().to_path_buf());
        let import = |blueprint| store.import(alone(blueprint)).ok();
        assert_eq!(import(mine("ma-recolte")), Some(Id("ma-recolte".into())));
        assert_eq!(
            import(mine("ma-recolte")),
            Some(Id("ma-recolte-2".into())),
            "its id is taken"
        );
        // A file kept as it is, in another case, under the id the next would take.
        let kept = folder.path().join("blueprints/Ma-Recolte-3.json");
        std::fs::write(&kept, "{").unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(import(mine("ma-recolte")), Some(Id("ma-recolte-4".into())));
        assert_eq!(std::fs::read_to_string(&kept).ok().as_deref(), Some("{"));
        let mut odd = mine("x");
        odd.id = Id("../Escape".into());
        assert_eq!(import(odd), Some(Id("escape".into())), "an id no file can be named by");
        assert_eq!(import(mine("hunt")), Some(Id("hunt-2".into())), "one of D3B's ids");
        let ids: Vec<_> = store.blueprints().read.into_iter().map(|it| it.id.0).collect();
        assert_eq!(ids, ["escape", "hunt-2", "ma-recolte-2", "ma-recolte-4", "ma-recolte"]);
    }

    #[test]
    fn a_given_blueprint_is_shared_as_mine_based_on_it_and_mine_as_it_is() {
        let folder = Folder::new("share-shared");
        let store = Store::new(folder.path().to_path_buf());
        let texts = bot::Texts {
            name: "Chasse".into(),
            description: "Chasse les monstres d'une zone.".into(),
            ..bot::Texts::default()
        };
        let hunt = store
            .shared("hunt", Some(texts))
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            (hunt.blueprint.id.0.as_str(), hunt.blueprint.name.as_str()),
            ("chasse", "Chasse")
        );
        assert_eq!(hunt.blueprint.based_on.map(|based| based.id.0).as_deref(), Some("hunt"));
        assert_eq!(hunt.zones, [], "D3B's name no zone of mine");
        let untranslated = store.shared("hunt", None).unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(untranslated.blueprint.id.0, "hunt-2", "its name's id is one of D3B's");
        store
            .import(alone(mine("ma-recolte")))
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(store.shared("ma-recolte", None).ok(), Some(alone(mine("ma-recolte"))));
        assert!(matches!(
            store.shared("teleport", None),
            Err(StorageError::NoSuchBlueprint { .. })
        ));
    }
}
