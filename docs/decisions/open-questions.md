# Open questions

Settled questions leave this table, and the decision that settles one says so.

| Id | Question | Settled in |
| --- | --- | --- |
| Q5 | Which fight formulas of Bubble emu (Dofus 2) still hold in Dofus 3? Measured in 9d for damage, steals, heals, critical hits and the zone's decrease (D195), in 9e2 for a critical hit's chance (D207) and a push's damage (D214), in 9e3 for a sword's blow (D222); left: shields, AP and MP removed and their dodge, erosion, the final damage's rounding, a weapon's critical bonus, power and mastery (D220). | The captures of D196 left (D214), then 9f |
| Q12 | Which facts must the bot learn, beyond its state, for the criteria it cannot answer: quests, breed, alignment, achievements, the server's own codes; from which messages, and what does each code mean, measured (D71)? On doors, achievements would open the most, then `Sc` and `SC`, then quests (D76). | The large effort on criteria the owner noted, then as each step needs them |
| Q16 | Which casts and walks does the server refuse that D3B's rules allow, or allow that they refuse: how it counts a corner beside what blocks, a proportional diagonal, the 11 zones no capture cast (D190, D191)? It refuses with an error text, 29 ms after the request (D203); which casts, but a Zobal's masks, is left. It allowed 11 casts the rules refuse, each a breed's mechanism: spells whose cost or range a gathered state changes, and summons (D214). The Zobal's 6 refused were the masks' states the bot kept, whose end it did not read (D224, D225). | Captures the owner makes, the AI sending its casts (D202), with those of D196; the breeds' mechanisms in 9f |
| Q17 | When a client build changes its schema, does it shuffle and renumber the game's fields, and how is its game mapped from the previous build's: messages by order and shape, fields of one type told apart, numbers rewritten on the wire (D54, D56, D361)? | The session after the first client update whose schema changes |
