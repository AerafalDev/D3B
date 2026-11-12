//! A character followed live (`docs/game.md`, "In the app"): the observer of its connection's decoded messages, which
//! sends its view whenever it changed, at the protocol's ticks: at most five times a second, once per point while the
//! life regenerates, and `None` once the connection ended.

use std::time::Instant;

use super::character::{Character, View};
use crate::protocol::{Message, Observer};

/// The character of one game connection, and where its view goes.
pub(crate) struct Live<E: FnMut(Option<View>) + Send + 'static> {
    character: Character,
    sent: Option<View>,
    send: E,
}

impl<E: FnMut(Option<View>) + Send + 'static> Live<E> {
    /// A character not seen yet, whose views go to `send`.
    pub(crate) fn new(send: E) -> Self {
        Self {
            character: Character::default(),
            sent: None,
            send,
        }
    }
}

impl<E: FnMut(Option<View>) + Send + 'static> Observer for Live<E> {
    fn message(&mut self, message: &Message, at: Instant, dropped: u64) {
        self.character.apply(message, at, dropped);
    }

    fn tick(&mut self, now: Instant, dropped: u64) {
        let view = self.character.view(now, dropped);
        if self.sent.as_ref() != Some(&view) {
            (self.send)(Some(view.clone()));
            self.sent = Some(view);
        }
    }

    fn closed(&mut self) {
        if self.sent.take().is_some() {
            (self.send)(None);
        }
    }
}

#[cfg(test)]
mod tests {
    use std::sync::{Arc, Mutex};
    use std::time::Duration;

    use super::*;
    use crate::protocol::game;

    /// The views a [`Live`] sent, in order.
    type Sent = Arc<Mutex<Vec<Option<View>>>>;

    fn live() -> (Live<impl FnMut(Option<View>) + Send>, Sent) {
        let sent = Arc::new(Mutex::new(Vec::new()));
        let kept = Arc::clone(&sent);
        let live = Live::new(move |view| {
            kept.lock()
                .unwrap_or_else(std::sync::PoisonError::into_inner)
                .push(view);
        });
        (live, sent)
    }

    fn taken(sent: &Sent) -> Vec<Option<View>> {
        std::mem::take(&mut *sent.lock().unwrap_or_else(std::sync::PoisonError::into_inner))
    }

    #[test]
    fn sends_the_view_at_a_tick_only_when_it_changed() {
        let now = Instant::now();
        let (mut live, sent) = live();
        live.tick(now, 0);
        assert_eq!(taken(&sent).len(), 1, "the first view");

        live.tick(now + Duration::from_millis(200), 0);
        assert_eq!(taken(&sent), [], "nothing changed");

        let pods = Message::InventoryWeightEvent(game::InventoryWeightEvent {
            weight: 812,
            max_weight: 2_350,
        });
        live.message(&pods, now, 0);
        assert_eq!(taken(&sent), [], "a message waits for the tick");
        live.tick(now + Duration::from_millis(400), 0);
        let views = taken(&sent);
        assert_eq!(views.len(), 1);
        assert_eq!(
            views[0].as_ref().and_then(|view| view.pods).map(|pods| pods.weight),
            Some(812)
        );

        // Frames dropped make it stale: that is a change too.
        live.tick(now + Duration::from_millis(600), 3);
        assert_eq!(taken(&sent).len(), 1);
    }

    #[test]
    fn sends_none_once_closed_and_only_after_a_view() {
        let (mut live, sent) = live();
        live.closed();
        assert_eq!(taken(&sent), [], "nothing was shown");

        live.tick(Instant::now(), 0);
        live.closed();
        assert_eq!(taken(&sent).last(), Some(&None));
    }
}
