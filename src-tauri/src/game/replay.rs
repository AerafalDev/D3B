//! The owner's game captures read through the game's state (`docs/game.md`, "Tests"): the life counted while it
//! regenerates against the server's count as each fight starts, the character's actor on every map, and its walks
//! starting where it stands. A capture holds the account's ticket: it prints counts only. Ignored unless
//! `D3B_CAPTURES` names their folder.

use std::time::Instant;

use super::character::{Character, Context};
use crate::protocol::Message;
use crate::protocol::replay::{decoded, folder};

/// What the replay counted, over every connection.
#[derive(Debug, Default)]
struct Counts {
    /// Fights whose `LifeRegenEndEvent` came, and those whose life the count missed by more than a point.
    regenerations: u32,
    missed_lives: u32,
    /// Map informations after a selection, and those that did not hold the character.
    maps: u32,
    maps_without_it: u32,
    /// The character's walks out of fights with its cell known, and those that did not start on it.
    walks: u32,
    walks_elsewhere: u32,
    /// Stated elements' updates out of fights, and those of an element not on the current map's list.
    updates: u32,
    updates_unknown: u32,
}

#[test]
#[ignore = "needs the owner's captures: set D3B_CAPTURES to their folder"]
fn the_owners_game_captures_read_through_the_games_state() {
    let start = Instant::now();
    let mut counts = Counts::default();
    decoded(&folder(), |messages| {
        let mut character = Character::default();
        // The life counted as the last fight started, before its characteristics, sent with the life at the
        // regeneration's start, replace it.
        let mut at_the_fight = None;
        for (at, message) in messages {
            let Some(message) = message else { continue };
            let now = start + *at;
            let view = character.view(now, 0);
            let me = view.who.as_ref().map(|who| who.id);
            match message {
                Message::ContextCreationEvent(created) if created.context == 1 => {
                    at_the_fight = view.life.map(|life| life.life);
                }
                Message::StatedElementUpdatedEvent(update) if view.context != Some(Context::Fight) => {
                    let id = update.element.as_ref().map(|element| element.element_id);
                    counts.updates += 1;
                    counts.updates_unknown +=
                        u32::from(!view.around.stated.iter().any(|known| Some(known.element_id) == id));
                }
                Message::LifeRegenEndEvent(end) => {
                    if let Some(counted) = at_the_fight.take() {
                        counts.regenerations += 1;
                        counts.missed_lives += u32::from((counted - i64::from(end.life)).abs() > 1);
                    }
                }
                Message::MapMovementEvent(walk) if me == Some(walk.actor_id) => {
                    if view.context != Some(Context::Fight)
                        && let Some(cell) = view.cell
                    {
                        counts.walks += 1;
                        counts.walks_elsewhere += u32::from(walk.cells.first() != Some(&cell));
                    }
                }
                _ => {}
            }
            character.apply(message, now, 0);
            if matches!(message, Message::MapInformationEvent(_)) && me.is_some() {
                counts.maps += 1;
                counts.maps_without_it += u32::from(character.view(now, 0).cell.is_none());
            }
        }
    });
    eprintln!("{counts:?}");
    assert!(
        counts.regenerations > 0 && counts.maps > 0 && counts.walks > 0,
        "nothing read: {counts:?}"
    );
    assert_eq!(counts.missed_lives, 0, "lives counted more than a point off");
    assert_eq!(counts.maps_without_it, 0, "maps without the character");
    // The study: 93,822 of 94,602 walks start on the known cell; the server does not say when one is cut short.
    assert!(
        counts.walks_elsewhere * 100 <= counts.walks,
        "walks elsewhere: {counts:?}"
    );
    // Measured on 2026-11-12: 4,169 of 71,882 updates name an element not on the list, likely a neighbouring map's or
    // one coming between a map change and its information (inferred); they are left out.
    assert!(
        counts.updates_unknown * 10 <= counts.updates,
        "updates of unknown elements: {counts:?}"
    );
}
