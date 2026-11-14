//! The player's folder (ADR 0015): one JSON file per object, a folder per kind, each file named by its object's id.
//! A file that does not read (not text, not JSON, not of its shape, a newer schema, a name that is not its id) is kept
//! untouched and reported with why, and never written over: what a newer D3B or the player's own hand wrote is never
//! lost.

use std::collections::HashSet;
use std::io;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};

use super::atomic;
use crate::bot::{self, Blueprint, Id, ReadError};

/// The folder of the player's blueprints, in the player's folder.
const BLUEPRINTS: &str = "blueprints";

/// The player's folder: `Documents/D3B`, or the app's data folder where a system has no documents folder; the caller
/// finds it, so this domain knows nothing of Tauri.
#[derive(Debug, Clone)]
pub(crate) struct Store {
    root: PathBuf,
    /// Held while a file is written, so that two saves never share a temporary file; shared by the store's clones.
    writing: Arc<Mutex<()>>,
}

/// A file D3B leaves as it is, and why.
#[derive(Debug)]
pub(crate) struct Kept {
    pub(crate) path: PathBuf,
    pub(crate) why: Why,
}

/// Why a file is kept as it is.
#[derive(Debug, thiserror::Error)]
pub(crate) enum Why {
    /// The system could not read it, or list its folder: its reason, which may pass (a sync client holding it).
    #[error("it could not be read: {0}")]
    Unreadable(io::Error),
    /// Its bytes are no UTF-8 text.
    #[error("it is not text")]
    NotText,
    /// It reads, but not as a blueprint of this version.
    #[error("{0}")]
    NotRead(ReadError),
    /// It holds another id than its name says: a copy made by hand, which Duplicate makes instead (the owner,
    /// 2026-11-14), or a name in another case, which a system that ignores case would take for this one.
    #[error("its name is not its id, {}", id.0)]
    NameIsNotId { id: Id },
    /// It holds the id of one of D3B's blueprints, which a blueprint of mine never takes.
    #[error("it holds {}, the id of one of D3B's blueprints", id.0)]
    GivenId { id: Id },
}

/// The blueprints of the player's folder, and the files kept as they are.
#[derive(Debug, Default)]
pub(crate) struct Blueprints {
    pub(crate) read: Vec<Blueprint>,
    pub(crate) kept: Vec<Kept>,
}

/// What keeps a blueprint from being saved.
#[derive(Debug, thiserror::Error)]
pub(crate) enum StorageError {
    #[error("{} could not be written: {source}", path.display())]
    Write { path: PathBuf, source: io::Error },
    /// Its file could not be read now, so it is not written over; a later save may pass.
    #[error("{} could not be read, so it is not written over: {source}", path.display())]
    Unreadable { path: PathBuf, source: io::Error },
    #[error("{} is kept as it is, not written over: {why}", path.display())]
    Kept { path: PathBuf, why: Why },
    #[error("{} is no id a file can be named by", id.0)]
    MalformedId { id: Id },
    #[error("{} is the id of one of D3B's blueprints", id.0)]
    GivenId { id: Id },
    #[error("no blueprint {}", id.0)]
    NoSuchBlueprint { id: Id },
}

impl Store {
    pub(crate) fn new(root: PathBuf) -> Self {
        Self {
            root,
            writing: Arc::default(),
        }
    }

    /// Every blueprint of the player's folder, in the order of their file names; none when it has no folder yet.
    pub(crate) fn blueprints(&self) -> Blueprints {
        let mut blueprints = Blueprints::default();
        let folder = self.root.join(BLUEPRINTS);
        let mut paths = Vec::new();
        match std::fs::read_dir(&folder) {
            Ok(entries) => {
                for entry in entries {
                    match entry {
                        Ok(entry) => paths.push(entry.path()),
                        Err(error) => blueprints.kept.push(Kept {
                            path: folder.clone(),
                            why: Why::Unreadable(error),
                        }),
                    }
                }
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => return blueprints,
            Err(error) => blueprints.kept.push(Kept {
                path: folder,
                why: Why::Unreadable(error),
            }),
        }
        // Only JSON files, in any case: a temporary file a crash left, or anything else the player put there, is not
        // ours.
        paths.retain(|path| {
            path.extension()
                .is_some_and(|extension| extension.eq_ignore_ascii_case("json"))
        });
        paths.sort();
        for path in paths {
            match read_blueprint(&path) {
                Ok(blueprint) if given_ids().contains(&blueprint.id) => {
                    blueprints.kept.push(Kept {
                        path,
                        why: Why::GivenId { id: blueprint.id },
                    });
                }
                Ok(blueprint) => blueprints.read.push(blueprint),
                Err(why) => blueprints.kept.push(Kept { path, why }),
            }
        }
        blueprints
    }

    /// Writes `blueprint` to its file, whole, unless a file there, or one whose name differs from it only by case,
    /// does not read as this blueprint.
    #[cfg_attr(
        not(test),
        expect(dead_code, reason = "the editor saves as the player edits, step 3's PR 13")
    )]
    pub(crate) fn save_blueprint(&self, blueprint: &Blueprint) -> Result<(), StorageError> {
        let _writing = self.lock();
        self.save_while_locked(blueprint)
    }

    /// Duplicate: a copy of the blueprint `id`, D3B's or mine, saved among mine with `texts`, under an id made from its
    /// name that no blueprint and no file of the folder holds, in any case; the copy's id. The id is chosen and the
    /// file written under one lock, so two copies at once never share a file.
    pub(crate) fn duplicate(&self, id: &str, texts: bot::Texts) -> Result<Id, StorageError> {
        let _writing = self.lock();
        let given = bot::given().unwrap_or_default();
        let mine = self.blueprints();
        let folder = self.root.join(BLUEPRINTS);
        if let Some(Kept {
            why: Why::Unreadable(error),
            ..
        }) = mine.kept.iter().find(|kept| kept.path == folder)
        {
            let source = io::Error::new(error.kind(), error.to_string());
            return Err(StorageError::Unreadable { path: folder, source });
        }
        let sources = given
            .iter()
            .map(|it| (it, true))
            .chain(mine.read.iter().map(|it| (it, false)));
        let found = sources.clone().find(|(it, _)| it.id.0 == id);
        let (source, is_given) = found.ok_or_else(|| StorageError::NoSuchBlueprint { id: Id(id.to_owned()) })?;
        let mut taken: HashSet<Id> = sources.map(|(it, _)| it.id.clone()).collect();
        for entry in std::fs::read_dir(&folder).into_iter().flatten().flatten() {
            let path = entry.path();
            if let Some(stem) = path.file_stem().and_then(|stem| stem.to_str()) {
                taken.insert(Id(stem.to_lowercase()));
            }
        }
        let copy = bot::copy(source, is_given, bot::id_for(&texts.name, &taken), texts);
        self.save_while_locked(&copy)?;
        Ok(copy.id)
    }

    /// One write at a time; a write that panicked left nothing to protect.
    fn lock(&self) -> std::sync::MutexGuard<'_, ()> {
        self.writing.lock().unwrap_or_else(std::sync::PoisonError::into_inner)
    }

    fn save_while_locked(&self, blueprint: &Blueprint) -> Result<(), StorageError> {
        let id = &blueprint.id;
        if !id.is_well_formed() {
            return Err(StorageError::MalformedId { id: id.clone() });
        }
        if given_ids().contains(id) {
            return Err(StorageError::GivenId { id: id.clone() });
        }
        let folder = self.root.join(BLUEPRINTS);
        let name = format!("{}.json", id.0);
        for entry in std::fs::read_dir(&folder).into_iter().flatten().flatten() {
            let other = entry.file_name();
            if other
                .to_str()
                .is_some_and(|other| other != name && other.eq_ignore_ascii_case(&name))
            {
                let why = Why::NameIsNotId { id: id.clone() };
                return Err(StorageError::Kept {
                    path: entry.path(),
                    why,
                });
            }
        }
        let path = folder.join(name);
        match read_blueprint(&path) {
            Ok(_) => {}
            Err(Why::Unreadable(error)) if error.kind() == io::ErrorKind::NotFound => {}
            Err(Why::Unreadable(source)) => return Err(StorageError::Unreadable { path, source }),
            Err(why) => return Err(StorageError::Kept { path, why }),
        }
        let write = |path: &Path| {
            std::fs::create_dir_all(&folder)?;
            atomic::write(path, bot::write(blueprint).as_bytes())
        };
        write(&path).map_err(|source| StorageError::Write { path, source })
    }
}

/// The ids of D3B's blueprints; their tests hold them readable.
fn given_ids() -> HashSet<Id> {
    bot::given()
        .unwrap_or_default()
        .into_iter()
        .map(|blueprint| blueprint.id)
        .collect()
}

fn read_blueprint(path: &Path) -> Result<Blueprint, Why> {
    let bytes = std::fs::read(path).map_err(Why::Unreadable)?;
    let text = String::from_utf8(bytes).map_err(|_| Why::NotText)?;
    let blueprint = bot::read(&text).map_err(Why::NotRead)?;
    let name = path.file_name().and_then(|name| name.to_str());
    if name == Some(format!("{}.json", blueprint.id.0).as_str()) {
        Ok(blueprint)
    } else {
        Err(Why::NameIsNotId { id: blueprint.id })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bot::HARVEST as FIXTURE;
    use crate::storage::tests::Folder;

    /// The hand-written fixture as a blueprint of mine: its id is one of D3B's, which mine never take.
    fn text() -> String {
        FIXTURE.replacen("automatic-harvest", "my-harvest", 1)
    }

    fn harvest() -> Blueprint {
        bot::read(&text()).unwrap_or_else(|error| panic!("{error}"))
    }

    fn put(folder: &Folder, name: &str, text: &str) -> PathBuf {
        let path = folder.path().join(BLUEPRINTS).join(name);
        std::fs::create_dir_all(folder.path().join(BLUEPRINTS)).unwrap_or_else(|error| panic!("{error}"));
        std::fs::write(&path, text).unwrap_or_else(|error| panic!("{error}"));
        path
    }

    #[test]
    fn a_folder_not_made_yet_holds_no_blueprint() {
        let folder = Folder::new("store-empty");
        let blueprints = Store::new(folder.path().join("D3B")).blueprints();
        assert!(blueprints.read.is_empty() && blueprints.kept.is_empty());
    }

    #[test]
    fn a_blueprint_saved_is_read_back_in_its_file_named_by_its_id() {
        let folder = Folder::new("store-saved");
        let store = Store::new(folder.path().to_path_buf());
        store
            .save_blueprint(&harvest())
            .unwrap_or_else(|error| panic!("{error}"));
        let text = std::fs::read_to_string(folder.path().join("blueprints/my-harvest.json"));
        assert_eq!(text.ok(), Some(bot::write(&harvest())));
        let blueprints = store.blueprints();
        assert_eq!((blueprints.read, blueprints.kept.len()), (vec![harvest()], 0));
    }

    #[test]
    fn a_blueprint_saved_again_replaces_its_file() {
        let folder = Folder::new("store-again");
        let store = Store::new(folder.path().to_path_buf());
        let mut blueprint = harvest();
        store
            .save_blueprint(&blueprint)
            .unwrap_or_else(|error| panic!("{error}"));
        blueprint.name = "My harvest".to_owned();
        store
            .save_blueprint(&blueprint)
            .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(store.blueprints().read, [blueprint]);
    }

    #[test]
    fn files_that_do_not_read_are_kept_and_said_why_beside_those_that_do() {
        let folder = Folder::new("store-kept");
        let store = Store::new(folder.path().to_path_buf());
        store
            .save_blueprint(&harvest())
            .unwrap_or_else(|error| panic!("{error}"));
        let newer = put(
            &folder,
            "newer.json",
            &text().replacen("\"schema\": 1", "\"schema\": 2", 1),
        );
        let broken = put(&folder, "broken.json", "{\"schema\": 1,");
        let copied = put(&folder, "copy.json", &text());
        put(&folder, "my-harvest.json.d3b-tmp", "half");
        put(&folder, "notes.txt", "mine");
        let blueprints = store.blueprints();
        assert_eq!(blueprints.read, [harvest()]);
        let kept: Vec<_> = blueprints.kept.iter().map(|kept| (&kept.path, &kept.why)).collect();
        assert!(
            matches!(kept[0], (path, Why::NotRead(ReadError::NotABlueprint(_))) if *path == broken),
            "{kept:?}"
        );
        assert!(matches!(kept[1], (path, Why::NameIsNotId { id }) if *path == copied && id.0 == "my-harvest"));
        assert!(
            matches!(kept[2], (path, Why::NotRead(ReadError::Newer { found: 2 })) if *path == newer),
            "{kept:?}"
        );
        assert_eq!(kept.len(), 3);
    }

    #[test]
    fn a_file_that_does_not_read_is_never_written_over() {
        let folder = Folder::new("store-never");
        let store = Store::new(folder.path().to_path_buf());
        let newer = text().replacen("\"schema\": 1", "\"schema\": 2", 1);
        let path = put(&folder, "my-harvest.json", &newer);
        let error = store
            .save_blueprint(&harvest())
            .err()
            .map(|error| error.to_string())
            .unwrap_or_default();
        assert!(
            error.contains("not written over") && error.contains("newer D3B"),
            "{error}"
        );
        assert_eq!(std::fs::read_to_string(&path).ok(), Some(newer));
        let mut other = harvest();
        other.id = Id("other".to_owned());
        put(&folder, "other.json", &text());
        assert!(
            matches!(store.save_blueprint(&other), Err(StorageError::Kept { .. })),
            "a name that is not its id"
        );
    }

    #[test]
    fn a_blueprint_whose_id_names_no_file_is_not_saved() {
        let folder = Folder::new("store-id");
        let mut blueprint = harvest();
        blueprint.id = Id("../escape".to_owned());
        let saved = Store::new(folder.path().to_path_buf()).save_blueprint(&blueprint);
        assert!(matches!(saved, Err(StorageError::MalformedId { .. })));
        assert!(!folder.path().join("escape.json").exists());
    }

    #[test]
    fn a_file_that_is_not_text_is_kept_and_never_written_over() {
        let folder = Folder::new("store-bytes");
        let path = put(&folder, "my-harvest.json", "");
        std::fs::write(&path, [0xFF, 0xFE]).unwrap_or_else(|error| panic!("{error}"));
        let store = Store::new(folder.path().to_path_buf());
        assert!(matches!(
            store.blueprints().kept.as_slice(),
            [Kept { why: Why::NotText, .. }]
        ));
        let saved = store.save_blueprint(&harvest());
        assert!(
            matches!(saved, Err(StorageError::Kept { why: Why::NotText, .. })),
            "{saved:?}"
        );
        assert_eq!(std::fs::read(&path).ok(), Some(vec![0xFF, 0xFE]));
    }

    #[test]
    fn a_file_named_for_the_id_in_another_case_is_kept_and_never_written_over() {
        // On a system that ignores case, `My-Harvest.json` is the file `my-harvest.json` opens.
        let folder = Folder::new("store-case");
        let path = put(&folder, "My-Harvest.json", &text());
        put(&folder, "other.JSON", &text().replacen("my-harvest", "other", 1));
        let store = Store::new(folder.path().to_path_buf());
        let kept = store.blueprints().kept;
        assert!(
            kept.iter().all(|kept| matches!(kept.why, Why::NameIsNotId { .. })) && kept.len() == 2,
            "{kept:?}"
        );
        let saved = store.save_blueprint(&harvest());
        assert!(
            matches!(
                saved,
                Err(StorageError::Kept {
                    why: Why::NameIsNotId { .. },
                    ..
                })
            ),
            "{saved:?}"
        );
        assert_eq!(std::fs::read_to_string(&path).ok().as_deref(), Some(text().as_str()));
    }

    fn copy_texts(name: &str) -> bot::Texts {
        bot::Texts {
            name: name.to_owned(),
            ..bot::Texts::default()
        }
    }

    #[test]
    fn duplicate_takes_a_free_id_past_a_file_kept_in_the_way_and_its_other_case() {
        let folder = Folder::new("store-duplicate");
        let store = Store::new(folder.path().to_path_buf());
        // A newer D3B's file, and one in another case, under the names the copy's name gives.
        let newer = text().replacen("\"schema\": 1", "\"schema\": 2", 1);
        put(&folder, "chasse-copie.json", &newer);
        put(&folder, "Chasse-Copie-2.JSON", "{");
        let copied = store.duplicate("hunt", copy_texts("Chasse (copie)"));
        assert_eq!(copied.ok(), Some(Id("chasse-copie-3".to_owned())));
        assert_eq!(
            std::fs::read_to_string(folder.path().join("blueprints/chasse-copie.json")).ok(),
            Some(newer)
        );
        let again = store.duplicate("chasse-copie-3", copy_texts("Ma chasse"));
        assert_eq!(again.ok(), Some(Id("ma-chasse".to_owned())), "a copy of mine too");
        let based: Vec<_> = store
            .blueprints()
            .read
            .into_iter()
            .map(|it| it.based_on.map(|based| based.id.0))
            .collect();
        assert_eq!(based, [Some("hunt".to_owned()), Some("hunt".to_owned())]);
    }

    #[test]
    fn duplicate_refuses_a_blueprint_there_is_not() {
        let folder = Folder::new("store-duplicate-none");
        let store = Store::new(folder.path().to_path_buf());
        assert!(matches!(
            store.duplicate("teleport", copy_texts("x")),
            Err(StorageError::NoSuchBlueprint { .. })
        ));
    }

    #[test]
    fn a_file_of_mine_with_the_id_of_one_of_d3bs_is_kept_and_never_saved() {
        let folder = Folder::new("store-given-id");
        let store = Store::new(folder.path().to_path_buf());
        put(&folder, "automatic-harvest.json", FIXTURE);
        assert!(matches!(
            store.blueprints().kept.as_slice(),
            [Kept {
                why: Why::GivenId { .. },
                ..
            }]
        ));
        let given = bot::read(FIXTURE).unwrap_or_else(|error| panic!("{error}"));
        assert!(matches!(
            store.save_blueprint(&given),
            Err(StorageError::GivenId { .. })
        ));
        assert_eq!(
            std::fs::read_to_string(folder.path().join("blueprints/automatic-harvest.json"))
                .ok()
                .as_deref(),
            Some(FIXTURE)
        );
    }
}
