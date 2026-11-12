//! What is around a character on its map: the other actors and the elements (`docs/game.md`, "What is around";
//! 3.7.13.17, `artifacts/studies/live-view.md` §1 and §6).

use crate::protocol::game::{
    Actor, InteractiveElement, MapInformationEvent, StatedElement, roleplay_actor::Kind as Roleplay,
};

/// A monster of a group, at the grade whose level the client's monster table gives (verified, 721,776 of 721,776).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Monster {
    /// Its id in the client's monster table.
    pub id: i32,
    pub grade: i32,
    pub level: i32,
}

/// What an actor is (verified, 309,948 actors): no field holds another player's level.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum ActorKind {
    Player {
        name: String,
    },
    Npc {
        npc_id: i32,
    },
    MonsterGroup {
        main: Option<Monster>,
        others: Vec<Monster>,
    },
    /// A kind our `.proto` does not read (likely a tax collector or a prism), or none.
    Other,
}

/// Another actor on the map, and the cell it was last seen on: approximate for players, whose walks cut short are not
/// announced (verified: 9,867 of their 13,170 walks start where the last one ended).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Seen {
    pub id: i64,
    pub kind: ActorKind,
    pub cell: Option<i32>,
}

/// An element the character may use, such as a resource: usable when one of its skills is enabled.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Interactive {
    pub element_id: i32,
    /// Its kind's id in the client's interactives, or −1 for none (verified, 51,978 of 694,766).
    pub kind_id: i32,
    pub enabled_skills: Vec<i32>,
    pub disabled_skills: Vec<i32>,
}

/// An element with a state: 0 available, 2 being harvested, 1 harvested (the cycle verified, the meanings inferred).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Stated {
    pub element_id: i32,
    pub cell: Option<i32>,
    pub state: i32,
}

/// Everything around the character on its current map.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct Around {
    pub actors: Vec<Seen>,
    pub interactives: Vec<Interactive>,
    pub stated: Vec<Stated>,
}

impl Around {
    /// What a map's information holds, but the character itself (`own`) and the elements of the neighbouring maps,
    /// which it sends too with `on_current_map` false (verified, all of them on a neighbour's record).
    pub(crate) fn of_map(information: &MapInformationEvent, own: Option<i64>) -> Self {
        let mut around = Self::default();
        around.shown(&information.actors, own);
        for element in &information.interactive_elements {
            around.interactive(element);
        }
        for element in &information.stated_elements {
            around.stated(element);
        }
        around
    }

    /// Actors come onto the map, or are shown again: each replaces the one of its id.
    pub(crate) fn shown(&mut self, actors: &[Actor], own: Option<i64>) {
        for actor in actors.iter().filter(|actor| Some(actor.id) != own) {
            let seen = Seen {
                id: actor.id,
                kind: kind(actor),
                cell: actor
                    .disposition
                    .as_ref()
                    .and_then(|disposition| disposition.cell_id)
                    .and_then(on_the_map),
            };
            match self.actors.iter_mut().find(|known| known.id == actor.id) {
                Some(known) => *known = seen,
                None => self.actors.push(seen),
            }
        }
    }

    /// An actor left the map; an id never seen changes nothing.
    pub(crate) fn removed(&mut self, id: i64) {
        self.actors.retain(|actor| actor.id != id);
    }

    /// An actor walked to the last of `cells`; an actor never seen is not added, since its kind is unknown.
    pub(crate) fn walked(&mut self, id: i64, cells: &[i32]) {
        if let Some(actor) = self.actors.iter_mut().find(|actor| actor.id == id)
            && let Some(last) = cells.last().copied().and_then(on_the_map)
        {
            actor.cell = Some(last);
        }
    }

    /// An interactive element, new or changed: it replaces the known one of its id, and is added only when it says it
    /// is on the current map. Updates do not say so reliably (verified: 3,655 of 71,888 `InteractiveElementUpdatedEvent`
    /// and all 71,882 `StatedElementUpdatedEvent` say they are not), so a known element's update always applies.
    pub(crate) fn interactive(&mut self, element: &InteractiveElement) {
        let skills =
            |skills: &[crate::protocol::game::InteractiveSkill]| skills.iter().map(|skill| skill.skill_id).collect();
        let interactive = Interactive {
            element_id: element.element_id,
            kind_id: element.interactive_id,
            enabled_skills: skills(&element.enabled_skills),
            disabled_skills: skills(&element.disabled_skills),
        };
        match self
            .interactives
            .iter_mut()
            .find(|known| known.element_id == element.element_id)
        {
            Some(known) => *known = interactive,
            None if element.on_current_map => self.interactives.push(interactive),
            None => {}
        }
    }

    /// A stated element, new or changed, as [`Around::interactive`].
    pub(crate) fn stated(&mut self, element: &StatedElement) {
        let stated = Stated {
            element_id: element.element_id,
            cell: on_the_map(element.cell_id),
            state: element.state,
        };
        match self
            .stated
            .iter_mut()
            .find(|known| known.element_id == element.element_id)
        {
            Some(known) => *known = stated,
            None if element.on_current_map => self.stated.push(stated),
            None => {}
        }
    }
}

fn kind(actor: &Actor) -> ActorKind {
    let roleplay = actor
        .information
        .as_ref()
        .and_then(|information| information.roleplay.as_ref());
    match roleplay.and_then(|roleplay| roleplay.kind.as_ref()) {
        Some(Roleplay::Named(named)) => ActorKind::Player {
            name: named.name.clone(),
        },
        Some(Roleplay::Npc(npc)) => ActorKind::Npc { npc_id: npc.npc_id },
        Some(Roleplay::MonsterGroup(group)) => {
            let group = group.monsters.clone().unwrap_or_default();
            let monster = |monster: crate::protocol::game::Monster| Monster {
                id: monster.monster_id,
                grade: monster.grade,
                level: monster.level,
            };
            ActorKind::MonsterGroup {
                main: group.main.map(monster),
                others: group.others.into_iter().map(monster).collect(),
            }
        }
        None => ActorKind::Other,
    }
}

/// `cell` when it is one of a map's 560 cells (3.7.13.17, `docs/data.md`): a frame may hold any number.
pub(super) fn on_the_map(cell: i32) -> Option<i32> {
    (0..560).contains(&cell).then_some(cell)
}

#[cfg(test)]
mod tests;
