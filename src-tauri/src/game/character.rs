//! What D3B knows of the character of one game connection, built from its decoded messages (`docs/game.md`, "The
//! state of a character"; 3.7.13.17, `artifacts/studies/live-view.md`), and the view of it the app shows.

use std::time::Instant;

use super::life::{Characteristics, Life, LifeNow};
use crate::protocol::Message;
use crate::protocol::game::{
    CharacterSelectionEvent, character_selection_event::Result as Selection, context_creation_event::GameContext,
};

/// The character chosen (verified: its actor on each map bears its id, 66,171 of 66,171).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Who {
    pub id: i64,
    pub name: String,
    pub level: i32,
    pub breed: i32,
}

/// Roleplay, or a fight: `ContextCreationEvent`'s 0 or 1 (verified, 683 and 634 frames).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Context {
    Roleplay,
    Fight,
}

/// What the character carries against what it can, in pods.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Pods {
    pub weight: i32,
    pub max_weight: i32,
}

/// Which parts may be out of date: the protocol dropped frames since they were last refreshed.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
#[expect(
    clippy::struct_excessive_bools,
    reason = "one mark per part of the view, each independent of the others"
)]
pub(crate) struct Stale {
    pub map: bool,
    pub life: bool,
    pub pods: bool,
    pub kamas: bool,
}

/// The character as the app shows it at one moment.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct View {
    pub who: Option<Who>,
    pub context: Option<Context>,
    pub map_id: Option<i64>,
    /// Its cell on the map, from 0 to 559; none in a fight, whose positions are not read yet.
    pub cell: Option<i32>,
    pub life: Option<LifeNow>,
    pub characteristics: Option<Characteristics>,
    pub pods: Option<Pods>,
    pub kamas: Option<i64>,
    pub stale: Stale,
}

/// The protocol's dropped count when each part was last refreshed.
#[derive(Debug, Clone, Copy, Default)]
struct Refreshed {
    map: u64,
    life: u64,
    pods: u64,
    kamas: u64,
}

/// The state of one character, fed each decoded message of its connection.
#[derive(Debug, Default)]
pub(crate) struct Character {
    who: Option<Who>,
    context: Option<Context>,
    map_id: Option<i64>,
    cell: Option<i32>,
    characteristics: Option<Characteristics>,
    /// Whether the characteristics were read since the last selection: the first come before it (verified, 51 of 51).
    characteristics_since_selection: bool,
    life: Life,
    pods: Option<Pods>,
    kamas: Option<i64>,
    refreshed: Refreshed,
}

impl Character {
    /// Reads `message`, read at `at` while the connection had dropped `dropped` frames.
    pub(crate) fn apply(&mut self, message: &Message, at: Instant, dropped: u64) {
        match message {
            Message::CharacterSelectionEvent(selection) => self.select(selection),
            // Its field 1 is the new level (verified, 36 of 36 level-ups).
            Message::CharacterLevelUpEvent(level) => {
                if let Some(who) = &mut self.who {
                    who.level = level.level;
                }
            }
            Message::CharacterStatsEvent(stats) => {
                let stats = stats.stats.clone().unwrap_or_default();
                let read = Characteristics::read(&stats);
                self.life.sent(read.life, read.max_life);
                self.characteristics = Some(read);
                self.characteristics_since_selection = true;
                self.kamas = Some(stats.kamas);
                self.refreshed.life = dropped;
                self.refreshed.kamas = dropped;
            }
            Message::LifeRegenStartEvent(start) => {
                self.life.start(start.interval, at);
                self.refreshed.life = dropped;
            }
            Message::LifeRegenEndEvent(end) => {
                self.life.end(i64::from(end.life), i64::from(end.max_life));
                self.refreshed.life = dropped;
            }
            Message::InventoryWeightEvent(weight) => {
                self.pods = Some(Pods {
                    weight: weight.weight,
                    max_weight: weight.max_weight,
                });
                self.refreshed.pods = dropped;
            }
            // The new total (verified: the next characteristics' kamas equal it, 480 of 480).
            Message::KamasUpdateEvent(kamas) => {
                self.kamas = Some(kamas.kamas);
                self.refreshed.kamas = dropped;
            }
            Message::ContextCreationEvent(created) => match GameContext::try_from(created.context) {
                Ok(GameContext::Fight) => {
                    self.context = Some(Context::Fight);
                    self.cell = None;
                    self.life.stop(at);
                }
                Ok(GameContext::Roleplay) => self.context = Some(Context::Roleplay),
                // A context this build's enum lacks: neither is claimed.
                Err(_) => self.context = None,
            },
            Message::MapCurrentEvent(current) => {
                if self.map_id != Some(current.map_id) {
                    self.cell = None;
                }
                self.map_id = Some(current.map_id);
                self.refreshed.map = dropped;
            }
            Message::MapInformationEvent(information) => {
                // Its own map id wins: a `MapCurrentEvent` before it may have been dropped.
                self.map_id = Some(information.map_id);
                // Its actor bears the selection's id (verified, 66,171 of 66,171).
                self.cell = self.who.as_ref().and_then(|who| {
                    information
                        .actors
                        .iter()
                        .find(|actor| actor.id == who.id)
                        .and_then(|actor| actor.disposition.as_ref()?.cell_id)
                        .and_then(on_the_map)
                });
                self.refreshed.map = dropped;
            }
            // A walk lists every cell it crosses, from where the walker stood (verified, 93,822 of the character's 94,602
            // walks): it stops on the last. In a fight, pushes and teleports are not read yet, so walks are not either.
            Message::MapMovementEvent(movement) => {
                if self.context != Some(Context::Fight)
                    && self.who.as_ref().is_some_and(|who| who.id == movement.actor_id)
                    && let Some(last) = movement.cells.last().copied().and_then(on_the_map)
                {
                    self.cell = Some(last);
                }
            }
            _ => {}
        }
    }

    /// A character chosen: everything is its own from now on, but the characteristics sent just before it.
    fn select(&mut self, selection: &CharacterSelectionEvent) {
        let Some(Selection::Success(success)) = &selection.result else {
            return;
        };
        let character = success.character.clone().unwrap_or_default();
        let basics = character.basics.unwrap_or_default();
        let kept = self.characteristics_since_selection.then_some((
            self.characteristics,
            self.life,
            self.kamas,
            self.refreshed,
        ));
        *self = Self::default();
        if let Some((characteristics, life, kamas, refreshed)) = kept {
            (self.characteristics, self.life, self.kamas) = (characteristics, life, kamas);
            (self.refreshed.life, self.refreshed.kamas) = (refreshed.life, refreshed.kamas);
        }
        self.who = Some(Who {
            id: character.id,
            name: basics.name,
            level: basics.level.unwrap_or_default(),
            breed: basics.look.map(|look| look.breed_id).unwrap_or_default(),
        });
    }

    /// Whether the view changes at `now` with no message: a regeneration counting.
    pub(crate) fn changing(&self, now: Instant) -> bool {
        self.life.regenerating(now)
    }

    /// The view at `now`, while the connection has dropped `dropped` frames.
    pub(crate) fn view(&self, now: Instant, dropped: u64) -> View {
        let refreshed = self.refreshed;
        View {
            who: self.who.clone(),
            context: self.context,
            map_id: self.map_id,
            cell: self.cell,
            life: self.life.at(now),
            characteristics: self.characteristics,
            pods: self.pods,
            kamas: self.kamas,
            stale: Stale {
                map: dropped > refreshed.map,
                life: dropped > refreshed.life,
                pods: dropped > refreshed.pods,
                kamas: dropped > refreshed.kamas,
            },
        }
    }
}

/// `cell` when it is one of a map's 560 cells (3.7.13.17, `docs/data.md`): a frame may hold any number.
fn on_the_map(cell: i32) -> Option<i32> {
    (0..560).contains(&cell).then_some(cell)
}

#[cfg(test)]
mod tests;
