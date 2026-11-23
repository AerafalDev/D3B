//! The non-player characters and the actions taken on them: whom a character talks to, and what each dialog offers.
//!
//! The client's `NpcData` and `NpcActionData` (measured on 3.7.14.18 for format 6, `docs/data.md`). An NPC keeps its
//! name, its actions, and its dialog's questions and replies with their texts (the owner, 2026-11-23). The client
//! places no NPC on a map, the server does with the map's information, and says nothing of what a reply does but its
//! text: the bankers and the replies that open a bank are derived from it ([`Banker`]).

mod banker;

use std::path::Path;

use serde::Deserialize;

use super::DataError;
use super::items::unique;
use super::manifest::Manifest;
use super::text::Text;
pub use banker::Banker;

/// A non-player character, such as a banker.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct Npc {
    pub id: i32,
    pub name: Text,
    /// The actions a character may take on it, ids of the NPC actions, such as 3 to talk.
    pub actions: Vec<i32>,
    /// The questions it may ask, in the client's order.
    pub questions: Vec<NpcLine>,
    /// The replies it may offer, in the client's order.
    pub replies: Vec<NpcLine>,
}

/// A question or a reply of a dialog: the id the server names it by, the same whatever the NPC, and its text.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct NpcLine {
    pub id: i32,
    /// `None` when a language lacks it: 190 replies and 112 questions on 3.7.14.18 (the owner, 2026-11-23).
    pub text: Option<Text>,
}

/// An action a character may take on an NPC, such as 3, Parler.
#[derive(Debug, Clone, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct NpcAction {
    pub id: i32,
    /// The action it stands for: 17, Examiner, is 3, as 3 itself is (3.7.14.18).
    pub real_id: i32,
    pub name: Text,
}

/// The NPCs and their actions of a data folder, each in order of id, and the bankers derived from them.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NpcTables {
    pub npcs: Vec<Npc>,
    pub actions: Vec<NpcAction>,
    /// The NPCs that open a bank, in order of id.
    pub bankers: Vec<Banker>,
}

impl NpcTables {
    /// Reads the two tables that `manifest` lists in `folder`, refusing them when an id is held twice or an NPC
    /// offers an action the folder lacks.
    pub fn load(folder: &Path, manifest: &Manifest) -> Result<Self, DataError> {
        Self::checked(manifest.read(folder, "npcs")?, manifest.read(folder, "npc-actions")?)
    }

    fn checked(npcs: Vec<Npc>, actions: Vec<NpcAction>) -> Result<Self, DataError> {
        unique("npcs", npcs.iter().map(|npc| npc.id))?;
        let known = unique("npc-actions", actions.iter().map(|action| action.id))?;
        for npc in &npcs {
            if let Some(&action) = npc.actions.iter().find(|action| !known.contains(action)) {
                return Err(DataError::Reference {
                    record: format!("npc {}", npc.id),
                    field: "actions",
                    target: action,
                });
            }
        }
        let bankers = banker::bankers(&npcs);
        Ok(Self { npcs, actions, bankers })
    }

    /// The reply that opens `npc`'s bank among the replies its question `offered`, or `None` when `npc` is no banker
    /// or offers none of them there (a banker may stand where his question opens no bank, C# D251).
    pub fn bank_reply(&self, npc: i32, offered: &[i32]) -> Option<i32> {
        let banker = self.bankers.iter().find(|banker| banker.npc_id == npc)?;
        offered.iter().copied().find(|reply| banker.replies.contains(reply))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The extractor's own tests write these files byte for byte (tools/extractor/tests/Npcs/).
    const NPCS: &str = include_str!("../../../../tools/extractor/tests/fixtures/npcs/npcs.json");
    const ACTIONS: &str = include_str!("../../../../tools/extractor/tests/fixtures/npcs/npc-actions.json");

    fn read<T: serde::de::DeserializeOwned>(text: &str) -> Vec<T> {
        serde_json::from_str(text).unwrap_or_else(|error| panic!("{error}"))
    }

    fn fixtures() -> Result<NpcTables, DataError> {
        NpcTables::checked(read(NPCS), read(ACTIONS))
    }

    #[test]
    fn reads_the_npcs_and_actions_the_extractor_writes() {
        let tables = fixtures().unwrap_or_else(|error| panic!("{error}"));
        let banker = &tables.npcs[0];
        assert_eq!(
            (banker.id, banker.name.fr.as_str(), banker.actions.as_slice()),
            (7, "Banquier", &[3][..])
        );
        assert_eq!(banker.questions[0].id, 21);
        assert_eq!(
            banker.replies.iter().map(|reply| reply.id).collect::<Vec<_>>(),
            [11, 12]
        );
        assert_eq!(tables.npcs[1].replies[1], NpcLine { id: 13, text: None });
        assert_eq!((tables.actions[1].id, tables.actions[1].real_id), (17, 3));
    }

    #[test]
    fn finds_the_reply_that_opens_a_bankers_bank_among_those_offered() {
        let tables = fixtures().unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(
            tables.bankers,
            [Banker {
                npc_id: 7,
                replies: vec![11]
            }]
        );
        assert_eq!(tables.bank_reply(7, &[12, 11]), Some(11));
        assert_eq!(tables.bank_reply(7, &[12]), None);
        assert_eq!(tables.bank_reply(9, &[12, 13]), None);
    }

    #[test]
    fn refuses_an_action_the_folder_lacks_or_an_id_held_twice() {
        let actions: Vec<NpcAction> = read(ACTIONS);
        assert!(matches!(
            NpcTables::checked(read(NPCS), actions[..1].to_vec()),
            Err(DataError::Reference { record, field: "actions", target: 17 }) if record == "npc 9"
        ));
        let mut npcs: Vec<Npc> = read(NPCS);
        npcs[1].id = 7;
        assert!(matches!(
            NpcTables::checked(npcs, read(ACTIONS)),
            Err(DataError::Duplicate { table: "npcs", id: 7 })
        ));
        let mut twice = actions;
        twice[1].id = 3;
        assert!(matches!(
            NpcTables::checked(read(NPCS), twice),
            Err(DataError::Duplicate {
                table: "npc-actions",
                id: 3
            })
        ));
    }
}
