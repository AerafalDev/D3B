use super::Npc;

/// The start of the French text of a reply that opens the bank, with or without its final full stop: the client marks
/// no bank reply otherwise (8 NPCs on 3.7.14.18, `docs/data.md`). This text is the one value of the client the code
/// holds, so that every bank of every build is found without a list of ids.
const BANK_REPLY: &str = "Consulter son coffre personnel";

/// The action to talk to an NPC, the one a banker must offer: `NpcActionRequest`'s action 3, Parler.
const TALK: i32 = 3;

/// An NPC that opens a bank, and the replies that open it, any of which its question may offer.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Banker {
    pub npc_id: i32,
    /// In the client's order, some shared by several bankers.
    pub replies: Vec<i32>,
}

/// The NPCs one can talk to that offer a reply opening the bank, in the order of `npcs`.
pub(super) fn bankers(npcs: &[Npc]) -> Vec<Banker> {
    npcs.iter()
        .filter(|npc| npc.actions.contains(&TALK))
        .filter_map(|npc| {
            let replies: Vec<i32> = npc
                .replies
                .iter()
                .filter(|reply| reply.text.as_ref().is_some_and(|text| text.fr.starts_with(BANK_REPLY)))
                .map(|reply| reply.id)
                .collect();
            (!replies.is_empty()).then_some(Banker {
                npc_id: npc.id,
                replies,
            })
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::data::npcs::NpcLine;
    use crate::data::text::Text;

    fn text(fr: &str) -> Text {
        Text {
            fr: fr.to_owned(),
            en: String::new(),
            de: String::new(),
            es: String::new(),
            pt: String::new(),
        }
    }

    fn npc(id: i32, actions: Vec<i32>, replies: Vec<(i32, Option<Text>)>) -> Npc {
        Npc {
            id,
            name: text("PNJ"),
            actions,
            questions: Vec::new(),
            replies: replies.into_iter().map(|(id, text)| NpcLine { id, text }).collect(),
        }
    }

    #[test]
    fn a_banker_is_an_npc_one_talks_to_offering_the_chests_reply() {
        let chest = || Some(text("Consulter son coffre personnel."));
        let npcs = [
            npc(
                1,
                vec![3],
                vec![
                    (10, Some(text("Consulter son coffre personnel"))),
                    (11, Some(text("Au revoir."))),
                    (12, chest()),
                ],
            ),
            npc(2, vec![1, 3], vec![(13, None), (14, Some(text("Acheter.")))]),
            npc(3, vec![1], vec![(15, chest())]),
        ];
        assert_eq!(
            bankers(&npcs),
            [Banker {
                npc_id: 1,
                replies: vec![10, 12]
            }]
        );
    }
}
