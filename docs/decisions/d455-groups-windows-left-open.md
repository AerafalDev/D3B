# Groups, the windows the bot answered (D455)

A part of [the decisions](README.md); each decision has one line in [the index](../decisions-index.md). The party by the
bot is in [D437](d437-step-10-the-chain.md), the group's bank in [D442](d442-step-10-the-groups-bank.md) and
[D452](d452-group-bank-master-empties.md).

## D455: The windows of an invitation or an exchange the bot answered stay open, closed by hand

*2026-11-19, a study after D437 and D452, no code changed; leaving them the owner's choice among three, against the lead's
recommendation of withholding the event that opens them. Builds D60, D367, D437, D442.*

- **Measured** on the 31 game captures of 3.7.14.18 from 2026-11-18 and 2026-11-19 (`scripts/capture.cs --messages`,
  message names, field types and times only): a click on Accept sends the server one acceptance and nothing else, for an
  invitation into a party as for an exchange asked, so the window closes on the client alone. When D3B accepts, the
  server's messages are the same and the window, opened by the server's event, waits for a click that never comes.
- **Measured, the window closed by hand**: five times the follower's client then sent a refusal of the invitation, 40 s
  to 6 min after D3B had accepted it; the server answered none, and no follower left its party. Closing it is harmless,
  though a refusal of a party already joined is a request no client sends without D3B.
- **Measured, the exchange's screens**: the asking character's client sees the same messages whether its player or its
  bot asks; the asked character's window is the one D3B's acceptance leaves, the master's at the group's bank by the
  wire. Which window stays on which screen was not looked at.
- **Kept**: nothing is sent to the client, and no frame of the game it receives is held or dropped (D60); the player
  closes the windows by hand.
- **Rejected.**
  - Withholding from the client the event that opens the window when the bot answers it, the lead's recommendation: a
    party joined with no invitation is measured only as a client connects, an exchange opened with no request not at all,
    and the relay would rewrite the server's stream for the first time, for the screen alone.
  - Injecting the event that closes the window, D3B's first message to the client: the guest's cancellation is not
    pinned in the build, its effect on a client already in the party is unknown, and the only event known to close the
    exchange's request ends the exchange on the client, whose own leave would then end the real one.
