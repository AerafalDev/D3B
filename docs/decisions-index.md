# Decisions index

One line per decision of [the decisions](decisions/README.md), in order, grouped by the roadmap step that took it. Read this first,
then open the entries about the area you work on: the full file is too long to read whole. A decision that supersedes
or refines another says so in its own entry. Add a line here with every new decision.


## Foundation (D1 to D18)

- [D1](decisions/d001-d018-foundation.md#d1-net-10-on-windows) .NET 10 on Windows (2026-10-27)
- [D2](decisions/d001-d018-foundation.md#d2-what-d3b-never-contains) What D3B never contains (2026-10-27)
- [D3](decisions/d001-d018-foundation.md#d3-the-references-in-vendor-give-ideas-never-code) The references in `vendor/` give ideas, never code (2026-10-27)
- [D4](decisions/d001-d018-foundation.md#d4-mit-license-private-repository-until-the-first-milestone) MIT license, private repository until the first milestone (2026-10-27)
- [D5](decisions/d001-d018-foundation.md#d5-modules-assembled-as-in-bevy-for-composition-only) Modules assembled as in Bevy, for composition only (2026-10-27)
- [D6](decisions/d001-d018-foundation.md#d6-external-plugins-load-once-never-unload) External plugins load once, never unload (2026-10-27)
- [D7](decisions/d001-d018-foundation.md#d7-a-headless-host-runs-the-bots-the-desktop-app-is-its-client) A headless host runs the bots, the desktop app is its client (2026-10-27)
- [D8](decisions/d001-d018-foundation.md#d8-the-network-goes-through-kestrels-socket-layer-socket-mode-first) The network goes through Kestrel's socket layer, socket mode first (2026-10-27)
- [D9](decisions/d001-d018-foundation.md#d9-each-bot-is-an-async-actor) Each bot is an async actor (2026-10-27)
- [D10](decisions/d001-d018-foundation.md#d10-game-data-comes-from-file-based-apps-and-ships-as-packs) Game data comes from file-based apps, and ships as packs (2026-10-27)
- [D11](decisions/d001-d018-foundation.md#d11-bot-code-sees-clean-protobuf-classes-translated-per-build-by-our-own-pipeline) Bot code sees clean protobuf classes, translated per build by our own pipeline (2026-10-27)
- [D12](decisions/d001-d018-foundation.md#d12-lua-for-routes-and-routines-net-plugins-for-features) Lua for routes and routines, .NET plugins for features (2026-10-27)
- [D13](decisions/d001-d018-foundation.md#d13-fights-run-on-a-simulator-of-our-own) Fights run on a simulator of our own (2026-10-27)
- [D14](decisions/d001-d018-foundation.md#d14-avalonia-and-shadui-in-french-and-english) Avalonia and ShadUI, in French and English (2026-10-27)
- [D15](decisions/d001-d018-foundation.md#d15-the-rest-of-the-stack) The rest of the stack (2026-10-27)
- [D16](decisions/d001-d018-foundation.md#d16-the-first-milestone-is-harvesting-and-banking) The first milestone is harvesting and banking (2026-10-27)
- [D17](decisions/d001-d018-foundation.md#d17-one-large-task-per-session-documents-in-markdown) One large task per session, documents in Markdown (2026-10-27)
- [D18](decisions/d001-d018-foundation.md#d18-the-project-is-named-d3b) The project is named D3B (2026-10-27)

## Step 1 (D19 to D23)

- [D19](decisions/d019-d023-step-1.md#d19-the-solutions-layout-and-build) The solution's layout and build (2026-10-27)
- [D20](decisions/d019-d023-step-1.md#d20-how-modules-compose) How modules compose (2026-10-27)
- [D21](decisions/d019-d023-step-1.md#d21-the-plugin-contract) The plugin contract (2026-10-27)
- [D22](decisions/d019-d023-step-1.md#d22-plugins-compile-against-the-d3bcore-package) Plugins compile against the D3B.Core package (2026-10-27)
- [D23](decisions/d019-d023-step-1.md#d23-mitm-only-no-socket-mode) MITM only, no socket mode (2026-10-27)

## Step 2 (D24 to D33)

- [D24](decisions/d024-d033-step-2.md#d24-packs-ship-as-releases-of-the-d3b-repository-one-release-per-client-build) Packs ship as releases of the D3B repository, one release per client build (2026-10-27)
- [D25](decisions/d024-d033-step-2.md#d25-the-pack-format) The pack format (2026-10-27)
- [D26](decisions/d024-d033-step-2.md#d26-the-data-tools) The data tools (2026-10-27)
- [D27](decisions/d024-d033-step-2.md#d27-maps-and-the-world-in-the-pack-format-2) Maps and the world in the pack, format 2 (2026-10-27)
- [D28](decisions/d024-d033-step-2.md#d28-packs-compressed-at-zstd-19-several-entries-at-once-without-a-dictionary) Packs compressed at zstd 19, several entries at once, without a dictionary (2026-10-27)
- [D29](decisions/d024-d033-step-2.md#d29-game-tables-in-the-pack-format-3) Game tables in the pack, format 3 (2026-10-27)
- [D30](decisions/d024-d033-step-2.md#d30-still-no-zstd-dictionary) Still no zstd dictionary (2026-10-27)
- [D31](decisions/d024-d033-step-2.md#d31-the-host-gets-the-pack-of-the-installed-build-before-it-starts) The host gets the pack of the installed build before it starts (2026-10-27)
- [D32](decisions/d024-d033-step-2.md#d32-the-downloads-token-comes-from-the-configuration-else-from-the-github-cli) The download's token comes from the configuration, else from the GitHub CLI (2026-10-27)
- [D33](decisions/d024-d033-step-2.md#d33-publishing-a-client-builds-release) Publishing a client build's release (2026-10-27)

## Step 3 (D34 to D63)

- [D34](decisions/d034-d063-step-3.md#d34-a-hosts-line-brings-each-client-to-d3b-which-rewrites-the-game-servers-address) A hosts line brings each client to D3B, which rewrites the game server's address (2026-10-27)
- [D35](decisions/d034-d063-step-3.md#d35-the-clients-configuration-brings-it-to-d3b-rewritten-by-the-host-while-it-runs) The client's configuration brings it to D3B, rewritten by the host while it runs (2026-10-27)
- [D36](decisions/d034-d063-step-3.md#d36-d3bnetwork-relays-the-connection-phase) D3B.Network relays the connection phase (2026-10-27)
- [D37](decisions/d034-d063-step-3.md#d37-captures-record-raw-reads-in-a-format-of-our-own) Captures record raw reads, in a format of our own (2026-10-27)
- [D38](decisions/d034-d063-step-3.md#d38-the-connection-is-varint-framed-protobuf-in-the-clear) The connection is varint-framed protobuf, in the clear (2026-10-27)
- [D39](decisions/d034-d063-step-3.md#d39-the-obfuscated-schema-is-a-protobuf-filedescriptorset) The obfuscated schema is a protobuf FileDescriptorSet (2026-10-27)
- [D40](decisions/d034-d063-step-3.md#d40-the-schema-is-read-from-the-clients-metadata-and-its-encodings-from-the-machine-code) The schema is read from the client's metadata, and its encodings from the machine code (2026-10-27)
- [D41](decisions/d034-d063-step-3.md#d41-step-3-after-session-3c-the-connections-mapping-first-the-games-after-its-captures) Step 3 after session 3c: the connection's mapping first, the game's after its captures (2026-10-27)
- [D42](decisions/d034-d063-step-3.md#d42-the-readable-protocol-of-the-connection-phase) The readable protocol of the connection phase (2026-10-27)
- [D43](decisions/d034-d063-step-3.md#d43-matching-pins-anchors-from-captures-fields-then-scores) Matching: pins, anchors from captures, fields, then scores (2026-10-27)
- [D44](decisions/d034-d063-step-3.md#d44-the-contract-audit) The contract audit (2026-10-27)
- [D45](decisions/d034-d063-step-3.md#d45-the-mapping-is-a-protobuf-message-of-our-own) The mapping is a protobuf message of our own (2026-10-27)
- [D46](decisions/d034-d063-step-3.md#d46-session-3e-split-the-translation-and-the-gate-then-the-game-connection) Session 3e split: the translation and the gate, then the game connection (2026-10-28)
- [D47](decisions/d034-d063-step-3.md#d47-the-translation-decodes-once-through-the-readable-classes) The translation decodes once, through the readable classes (2026-10-28)
- [D48](decisions/d034-d063-step-3.md#d48-the-version-gate) The version gate (2026-10-28)
- [D49](decisions/d034-d063-step-3.md#d49-the-relay-reads-frames-and-logs-the-connection-phase-by-what-it-is) The relay reads frames, and logs the connection phase by what it is (2026-10-28)
- [D50](decisions/d034-d063-step-3.md#d50-the-release-tool-publishes-the-schema-and-the-mapping) The release tool publishes the schema and the mapping (2026-10-28)
- [D51](decisions/d034-d063-step-3.md#d51-the-relay-holds-the-server-selections-answer-alone-and-rewrites-it-once-understood-whole) The relay holds the server selection's answer alone, and rewrites it once understood whole (2026-10-28)
- [D52](decisions/d034-d063-step-3.md#d52-the-game-connection-is-matched-by-the-ticket-it-presents) The game connection is matched by the ticket it presents (2026-10-28)
- [D53](decisions/d034-d063-step-3.md#d53-the-game-connection-is-relayed-on-a-port-of-its-own-unread-and-captured-apart) The game connection is relayed on a port of its own, unread, and captured apart (2026-10-28)
- [D54](decisions/d034-d063-step-3.md#d54-the-game-is-paired-by-pins-where-its-structure-is-silent-then-from-build-to-build) The game is paired by pins where its structure is silent, then from build to build (2026-10-28)
- [D55](decisions/d034-d063-step-3.md#d55-the-games-events-read-only-what-bot-code-uses-its-requests-are-whole) The game's events read only what bot code uses; its requests are whole (2026-10-28)
- [D56](decisions/d034-d063-step-3.md#d56-the-readable-protocol-of-the-game) The readable protocol of the game (2026-10-28)
- [D57](decisions/d034-d063-step-3.md#d57-the-games-anchors-and-the-messages-its-frames-carry) The game's anchors, and the messages its frames carry (2026-10-28)
- [D58](decisions/d034-d063-step-3.md#d58-the-translation-reads-a-game-frame-through-its-type-url) The translation reads a game frame through its type URL (2026-10-28)
- [D59](decisions/d034-d063-step-3.md#d59-mapping-format-2) Mapping format 2 (2026-10-28)
- [D60](decisions/d034-d063-step-3.md#d60-the-bot-sends-the-requests-that-start-an-action-and-the-client-does-what-follows) The bot sends the requests that start an action, and the client does what follows (2026-10-28)
- [D61](decisions/d034-d063-step-3.md#d61-d3bs-frames-go-to-the-server-between-the-clients-and-captures-record-them) D3B's frames go to the server between the client's, and captures record them (2026-10-28)
- [D62](decisions/d034-d063-step-3.md#d62-the-game-connection-is-read-through-the-translation-for-the-modules-that-observe-it) The game connection is read through the translation, for the modules that observe it (2026-10-28)
- [D63](decisions/d034-d063-step-3.md#d63-replays-with-injection) Replays with injection (2026-10-28)

## Step 4 (D64 to D90)

- [D64](decisions/d064-d090-step-4.md#d64-step-4-in-six-sessions) Step 4 in six sessions (2026-10-28)
- [D65](decisions/d064-d090-step-4.md#d65-one-bot-per-character-its-account-known-by-the-ticket) One bot per character, its account known by the ticket (2026-10-28)
- [D66](decisions/d064-d090-step-4.md#d66-the-player-wins-and-the-bot-pauses-until-it-is-resumed) The player wins, and the bot pauses until it is resumed (2026-10-28)
- [D67](decisions/d064-d090-step-4.md#d67-the-bots-run-time) The bot's run time (2026-10-28)
- [D68](decisions/d064-d090-step-4.md#d68-cells-and-paths-in-d3bengine) Cells and paths in D3B.Engine (2026-10-28)
- [D69](decisions/d064-d090-step-4.md#d69-the-first-behavior-walking-on-a-map-and-its-refusal-named) The first behavior, walking on a map, and its refusal named (2026-10-28)
- [D70](decisions/d064-d090-step-4.md#d70-bot-states-compared-with-snapshots-written-in-the-tests-not-verify) Bot states compared with snapshots written in the tests, not Verify (2026-10-28)
- [D71](decisions/d064-d090-step-4.md#d71-paths-through-the-world-in-d3bengine-their-criteria-answered-strictly) Paths through the world in D3B.Engine, their criteria answered strictly (2026-10-28)
- [D72](decisions/d064-d090-step-4.md#d72-the-accounts-subscription-read-from-its-identification-prudently) The account's subscription, read from its identification, prudently (2026-10-28)
- [D73](decisions/d064-d090-step-4.md#d73-changing-maps) Changing maps (2026-10-28)
- [D74](decisions/d064-d090-step-4.md#d74-travelling-from-map-to-map) Travelling from map to map (2026-10-28)
- [D75](decisions/d064-d090-step-4.md#d75-using-an-element-of-the-map) Using an element of the map (2026-10-28)
- [D76](decisions/d064-d090-step-4.md#d76-doors-in-the-path-through-the-world-their-criteria-answered-strictly) Doors in the path through the world, their criteria answered strictly (2026-10-28)
- [D77](decisions/d064-d090-step-4.md#d77-the-bot-sends-nothing-until-the-client-drew-the-map) The bot sends nothing until the client drew the map (2026-10-28)
- [D78](decisions/d064-d090-step-4.md#d78-mapping-format-4) Mapping format 4 (2026-10-28)
- [D79](decisions/d064-d090-step-4.md#d79-a-banks-inside-is-found-in-play-and-remembered-by-the-host) A bank's inside is found in play, and remembered by the host (2026-10-28)
- [D80](decisions/d064-d090-step-4.md#d80-banking) Banking (2026-10-28)
- [D81](decisions/d064-d090-step-4.md#d81-the-players-leaving-is-told-from-the-clients-by-the-dialog) The player's leaving is told from the client's by the dialog (2026-10-28)
- [D82](decisions/d064-d090-step-4.md#d82-mapping-format-5) Mapping format 5 (2026-10-28)
- [D83](decisions/d064-d090-step-4.md#d83-the-path-through-the-world-teleports-from-where-the-character-stands) The path through the world teleports from where the character stands (2026-10-28)
- [D84](decisions/d064-d090-step-4.md#d84-the-teleports-messages-named-their-refusals-texts-mapping-format-6) The teleports' messages named, their refusals texts: mapping format 6 (2026-10-28)
- [D85](decisions/d064-d090-step-4.md#d85-a-walk-the-client-does-not-follow-leaves-the-character-where-the-server-has-it) A walk the client does not follow leaves the character where the server has it (2026-10-28)
- [D86](decisions/d064-d090-step-4.md#d86-floors-count-on-a-map-with-stairs) Floors count on a map with stairs (2026-10-28)
- [D87](decisions/d064-d090-step-4.md#d87-activities-steps-chosen-from-the-state) Activities, steps chosen from the state (2026-10-28)
- [D88](decisions/d064-d090-step-4.md#d88-a-fight-is-an-interruption-until-step-9-fights-it) A fight is an interruption, until step 9 fights it (2026-10-28)
- [D89](decisions/d064-d090-step-4.md#d89-recovery-the-clients-new-connection-and-a-walk-the-server-holds) Recovery, the client's new connection, and a walk the server holds (2026-10-28)
- [D90](decisions/d064-d090-step-4.md#d90-plugins-not-lua) Plugins, not Lua (2026-10-28)

## Step 5 (D91 to D102)

- [D91](decisions/d091-d102-step-5.md#d91-the-bots-run-in-the-apps-process) The bots run in the app's process (2026-10-29)
- [D92](decisions/d091-d102-step-5.md#d92-step-5-in-five-sessions) Step 5 in five sessions (2026-10-29)
- [D93](decisions/d091-d102-step-5.md#d93-activities-the-app-starts-from-a-catalogue-the-modules-fill) Activities the app starts, from a catalogue the modules fill (2026-10-29)
- [D94](decisions/d091-d102-step-5.md#d94-the-apps-first-version) The app's first version (2026-10-29)
- [D95](decisions/d091-d102-step-5.md#d95-plugins-compile-against-the-d3bbot-package) Plugins compile against the D3B.Bot package (2026-10-29)
- [D96](decisions/d091-d102-step-5.md#d96-a-plugins-page-in-the-apps-rail) A plugin's page, in the app's rail (2026-10-29)
- [D97](decisions/d091-d102-step-5.md#d97-the-loader-reads-a-plugins-root-module-from-its-metadata) The loader reads a plugin's root module from its metadata (2026-10-29)
- [D98](decisions/d091-d102-step-5.md#d98-an-activitys-settings-a-class-its-module-describes-in-code) An activity's settings, a class its module describes in code (2026-10-29)
- [D99](decisions/d091-d102-step-5.md#d99-accounts-seen-reusable-profiles-a-characters-own) Accounts seen, reusable profiles, a character's own (2026-10-29)
- [D100](decisions/d091-d102-step-5.md#d100-d3bs-database-sqlite-through-ef-core-in-a-project-of-its-own-no-secret) D3B's database: SQLite through EF Core, in a project of its own, no secret (2026-10-29)
- [D101](decisions/d091-d102-step-5.md#d101-the-apps-pages-a-rail-always-accounts-by-account-profiles-in-a-column) The app's pages: a rail always, accounts by account, profiles in a column (2026-10-29)
- [D102](decisions/d091-d102-step-5.md#d102-lua-scripts-replace-the-external-net-plugins) Lua scripts replace the external .NET plugins (2026-10-29)

## Step 7 (D103 to D109)

- [D103](decisions/d103-d109-step-7.md#d103-luacsharp-holds-q1-settled) LuaCSharp holds, Q1 settled (2026-10-29)
- [D104](decisions/d103-d109-step-7.md#d104-moonbot-among-the-references-and-what-the-owners-scripts-call) MoonBot among the references, and what the owner's scripts call (2026-10-29)
- [D105](decisions/d103-d109-step-7.md#d105-the-lua-api-is-d3bs-own) The Lua API is D3B's own (2026-10-29)
- [D106](decisions/d103-d109-step-7.md#d106-scripts-reach-the-protocol-through-d3bs-readable-requests) Scripts reach the protocol through D3B's readable requests (2026-10-29)
- [D107](decisions/d103-d109-step-7.md#d107-a-scripts-settings-a-table-onto-d98s-kinds) A script's settings, a table onto D98's kinds (2026-10-29)
- [D108](decisions/d103-d109-step-7.md#d108-scripts-live-in-a-watched-folder-with-the-apis-definitions) Scripts live in a watched folder, with the API's definitions (2026-10-29)
- [D109](decisions/d103-d109-step-7.md#d109-the-external-plugins-removed) The external plugins removed (2026-10-29)

## Step 5 (D110 to D125)

- [D110](decisions/d110-d125-step-5.md#d110-lua-scripts-measured-through-the-app) Lua scripts, measured through the app (2026-10-29)
- [D111](decisions/d110-d125-step-5.md#d111-the-logs-and-the-protocol-inspector-two-pages-of-the-rail) The logs and the protocol inspector, two pages of the rail (2026-10-29)
- [D112](decisions/d110-d125-step-5.md#d112-a-bots-journal-says-its-actions-not-the-protocol) A bot's journal says its actions, not the protocol (2026-10-29)
- [D113](decisions/d110-d125-step-5.md#d113-the-logs-and-the-frames-kept-in-memory-always) The logs and the frames kept in memory, always (2026-10-29)
- [D114](decisions/d110-d125-step-5.md#d114-the-hosts-problems-on-the-logs-page) The host's problems on the logs page (2026-10-29)
- [D115](decisions/d110-d125-step-5.md#d115-the-protocol-inspector) The protocol inspector (2026-10-29)
- [D116](decisions/d110-d125-step-5.md#d116-lua-scripts-go-in-a-session-of-their-own) Lua scripts go, in a session of their own (2026-10-29)
- [D117](decisions/d110-d125-step-5.md#d117-mods-in-blueprint-style-after-step-6) Mods in blueprint style, after step 6 (2026-10-29)
- [D118](decisions/d110-d125-step-5.md#d118-no-activity-of-d3bs-own-until-step-6) No activity of D3B's own until step 6 (2026-10-29)
- [D119](decisions/d110-d125-step-5.md#d119-the-lua-scripts-removed) The Lua scripts removed (2026-10-29)
- [D120](decisions/d110-d125-step-5.md#d120-the-live-map-a-page-of-the-rail) The live map, a page of the rail (2026-10-29)
- [D121](decisions/d110-d125-step-5.md#d121-the-live-map-shows-what-the-pack-and-the-state-hold-in-layers) The live map shows what the pack and the state hold, in layers (2026-10-29)
- [D122](decisions/d110-d125-step-5.md#d122-the-live-map-drawn-flat-from-the-pack) The live map, drawn flat from the pack (2026-10-29)
- [D123](decisions/d110-d125-step-5.md#d123-maps-for-settings-wait-for-the-activity-that-needs-them) Maps for settings wait for the activity that needs them (2026-10-29)
- [D124](decisions/d110-d125-step-5.md#d124-the-live-map-measured-through-the-app) The live map, measured through the app (2026-10-29)
- [D125](decisions/d110-d125-step-5.md#d125-a-fights-placement-cells-from-the-server-at-step-9) A fight's placement cells, from the server at step 9 (2026-10-29)

## Step 6 (D126 to D175)

- [D126](decisions/d126-d175-step-6.md#d126-step-6-in-three-sessions-the-icons-in-their-own) Step 6 in three sessions, the icons in their own (2026-10-29)
- [D127](decisions/d126-d175-step-6.md#d127-the-harvesting-module-its-settings-and-the-profile-editor) The harvesting module, its settings, and the profile editor (2026-10-29)
- [D128](decisions/d126-d175-step-6.md#d128-the-next-resource-and-the-next-map-tours-through-the-zone-the-nearest-first) The next resource and the next map: tours through the zone, the nearest first (2026-10-29)
- [D129](decisions/d126-d175-step-6.md#d129-banking-full-and-the-way-back) Banking full, and the way back (2026-10-29)
- [D130](decisions/d126-d175-step-6.md#d130-a-fight-a-new-connection-the-players-pause-the-activity-resumes-from-the-state) A fight, a new connection, the player's pause: the activity resumes from the state (2026-10-29)
- [D131](decisions/d126-d175-step-6.md#d131-an-activity-says-what-it-does-its-report-on-the-bots-card-and-the-map-page) An activity says what it does: its report, on the bot's card and the map page (2026-10-29)
- [D132](decisions/d126-d175-step-6.md#d132-banking-lets-the-client-show-its-windows) Banking lets the client show its windows (2026-10-29)
- [D133](decisions/d126-d175-step-6.md#d133-harvesting-measured-through-the-app) Harvesting, measured through the app (2026-10-29)
- [D134](decisions/d126-d175-step-6.md#d134-no-human-timings) No human timings (2026-10-29)
- [D135](decisions/d126-d175-step-6.md#d135-the-icons-of-6b-the-items-a-harvest-names-and-the-professions) The icons of 6b: the items a harvest names, and the professions (2026-10-29)
- [D136](decisions/d126-d175-step-6.md#d136-icons-64-pixels-square-in-raw-rgba) Icons 64 pixels square, in raw RGBA (2026-10-29)
- [D137](decisions/d126-d175-step-6.md#d137-bc7-decoded-by-assetrippertexturedecoder-in-the-extractor-alone) BC7 decoded by AssetRipper.TextureDecoder, in the extractor alone (2026-10-29)
- [D138](decisions/d126-d175-step-6.md#d138-the-maps-graphics-learned-by-the-host-foretell-a-tour) The maps' graphics, learned by the host, foretell a tour (2026-10-29)
- [D139](decisions/d126-d175-step-6.md#d139-pack-format-4-and-its-release) Pack format 4, and its release (2026-10-29)
- [D140](decisions/d126-d175-step-6.md#d140-bases-resources-harvested-in-6c) Base's resources harvested in 6c (2026-10-29)
- [D141](decisions/d126-d175-step-6.md#d141-the-icons-in-the-app) The icons in the app (2026-10-29)
- [D142](decisions/d126-d175-step-6.md#d142-6b-measured-through-the-app) 6b measured through the app (2026-10-29)
- [D143](decisions/d126-d175-step-6.md#d143-a-pass-on-the-apps-ui-ux-and-options-in-a-session-of-its-own) A pass on the app's UI, UX and options, in a session of its own (2026-10-29)
- [D144](decisions/d126-d175-step-6.md#d144-a-fight-while-harvesting-resumed-paused-or-stopped-as-the-profile-says) A fight while harvesting: resumed, paused or stopped, as the profile says (2026-10-29)
- [D145](decisions/d126-d175-step-6.md#d145-the-resources-growing-back-learned-by-the-host) The resources' growing back, learned by the host (2026-10-29)
- [D146](decisions/d126-d175-step-6.md#d146-harvest-statistics-in-d3bs-database-and-a-page-of-their-own) Harvest statistics, in D3B's database and a page of their own (2026-10-29)
- [D147](decisions/d126-d175-step-6.md#d147-the-guilds-chest-measured-first) The guild's chest, measured first (2026-10-29)
- [D148](decisions/d126-d175-step-6.md#d148-upkeep-resource-bags-opened-the-character-healed-under-a-share-of-its-life) Upkeep: resource bags opened, the character healed under a share of its life (2026-10-29)
- [D149](decisions/d126-d175-step-6.md#d149-a-zones-isolated-maps-left-out-unless-the-profile-takes-them) A zone's isolated maps, left out unless the profile takes them (2026-10-29)
- [D150](decisions/d126-d175-step-6.md#d150-bases-resources-harvested-as-the-others) Base's resources harvested as the others (2026-10-29)
- [D151](decisions/d126-d175-step-6.md#d151-the-clients-display-time-and-every-delay-of-d3b-in-a-settings-page) The client's display time, and every delay of D3B in a settings page (2026-10-29)
- [D152](decisions/d126-d175-step-6.md#d152-the-owners-harvesting-settings-in-6e-no-human-or-random-timings) The owner's harvesting settings in 6e; no human or random timings (2026-10-29)
- [D153](decisions/d126-d175-step-6.md#d153-upkeep-and-the-guilds-chest-measured-mapping-format-7) Upkeep and the guild's chest, measured: mapping format 7 (2026-10-29)
- [D154](decisions/d126-d175-step-6.md#d154-what-the-first-check-through-the-app-found-and-the-time-between-two-actions) What the first check through the app found, and the time between two actions (2026-10-29)
- [D155](decisions/d126-d175-step-6.md#d155-6d-a-redesign-of-the-apps-interface) 6d, a redesign of the app's interface (2026-10-29)
- [D156](decisions/d126-d175-step-6.md#d156-using-several-objects-at-once-named-mapping-format-8) Using several objects at once named: mapping format 8 (2026-10-29)
- [D157](decisions/d126-d175-step-6.md#d157-6c-checked-through-the-app) 6c checked through the app (2026-10-29)
- [D158](decisions/d126-d175-step-6.md#d158-the-repository-public-once-the-app-ships-with-its-data-and-updates-itself) The repository public, once the app ships with its data and updates itself (2026-10-29)
- [D159](decisions/d126-d175-step-6.md#d159-the-profile-editor-a-page-of-its-own-its-preview-beside-one-scale-for-the-app) The profile editor, a page of its own, its preview beside; one scale for the app (2026-10-29)
- [D160](decisions/d126-d175-step-6.md#d160-resources-by-profession-or-by-list-in-6e) Resources by profession or by list, in 6e (2026-10-29)
- [D161](decisions/d126-d175-step-6.md#d161-lucides-icons-as-the-apps-geometries) Lucide's icons, as the app's geometries (2026-10-29)
- [D162](decisions/d126-d175-step-6.md#d162-what-the-redesign-settled-while-making-the-pages) What the redesign settled while making the pages (2026-10-29)
- [D163](decisions/d126-d175-step-6.md#d163-the-redesign-made-consistent-where-the-old-and-the-new-still-mixed) The redesign made consistent, where the old and the new still mixed (2026-10-29)
- [D164](decisions/d126-d175-step-6.md#d164-6d-checked-through-the-app) 6d checked through the app (2026-10-29)
- [D165](decisions/d126-d175-step-6.md#d165-resources-by-profession-or-by-list-made) Resources by profession or by list, made (2026-10-29)
- [D166](decisions/d126-d175-step-6.md#d166-the-place-chosen-by-the-activity-the-sub-area-richest-in-what-the-character-can-harvest) The place chosen by the activity, the sub-area richest in what the character can harvest (2026-10-29)
- [D167](decisions/d126-d175-step-6.md#d167-a-zaap-only-when-it-saves-enough-maps) A zaap only when it saves enough maps (2026-10-29)
- [D168](decisions/d126-d175-step-6.md#d168-profiles-given-by-default-stored-once) Profiles given by default, stored once (2026-10-29)
- [D169](decisions/d126-d175-step-6.md#d169-after-6e-6f-then-fights-and-their-ai-then-blueprints) After 6e, 6f, then fights and their AI, then blueprints (2026-10-29)
- [D170](decisions/d126-d175-step-6.md#d170-6e-checked-through-the-app) 6e checked through the app (2026-10-29)
- [D171](decisions/d126-d175-step-6.md#d171-the-app-published-by-ci-with-velopack-on-a-tag) The app published by CI with Velopack, on a tag (2026-10-29)
- [D172](decisions/d126-d175-step-6.md#d172-the-app-ships-its-client-builds-files) The app ships its client build's files (2026-10-29)
- [D173](decisions/d126-d175-step-6.md#d173-the-app-updates-itself-from-the-repositorys-releases-when-its-user-chooses) The app updates itself from the repository's releases, when its user chooses (2026-10-29)
- [D174](decisions/d126-d175-step-6.md#d174-the-repository-public-at-6fs-end-each-step-on-the-owners-go) The repository public at 6f's end, each step on the owner's go (2026-10-29)
- [D175](decisions/d126-d175-step-6.md#d175-6f-checked-on-the-owners-machine-before-anything-is-published) 6f checked on the owner's machine, before anything is published (2026-10-29)

## Step 9 (D176 to D292)

- [D176](decisions/d176-d208-step-9.md#d176-the-release-v010-checked-installed-from-github) The release v0.1.0 checked, installed from GitHub (2026-10-29)
- [D177](decisions/d176-d208-step-9.md#d177-step-9-in-seven-sessions-from-the-protocol-up) Step 9 in seven sessions, from the protocol up (2026-10-29)
- [D178](decisions/d176-d208-step-9.md#d178-the-fights-protocol-measured-mapping-format-9) The fight's protocol, measured: mapping format 9 (2026-10-30)
- [D179](decisions/d176-d208-step-9.md#d179-the-fights-state-in-the-characters) The fight's state, in the character's (2026-10-30)
- [D180](decisions/d176-d208-step-9.md#d180-the-placement-cells-and-the-fighters-on-the-map) The placement cells and the fighters on the map (2026-10-30)
- [D181](decisions/d176-d208-step-9.md#d181-what-the-references-give-step-9-read-again-for-9b) What the references give step 9, read again for 9b (2026-10-30)
- [D182](decisions/d176-d208-step-9.md#d182-the-fights-data-in-the-pack-format-5) The fight's data in the pack, format 5 (2026-10-30)
- [D183](decisions/d176-d208-step-9.md#d183-the-flags-and-names-read-from-the-clients-code) The flags and names read from the client's code (2026-10-30)
- [D184](decisions/d176-d208-step-9.md#d184-the-casts-level-and-the-fighters-and-loots-monsters-checked-monsters-named-on-the-map) The cast's level and the fighters' and loot's monsters, checked; monsters named on the map (2026-10-30)
- [D185](decisions/d176-d208-step-9.md#d185-9cs-rules-in-d3bengine-pure-measured-on-every-cast-and-walk-captured) 9c's rules in `D3B.Engine`, pure, measured on every cast and walk captured (2026-10-30)
- [D186](decisions/d176-d208-step-9.md#d186-range-and-line-of-sight) Range and line of sight (2026-10-30)
- [D187](decisions/d176-d208-step-9.md#d187-zones-and-target-masks) Zones and target masks (2026-10-30)
- [D188](decisions/d176-d208-step-9.md#d188-a-casts-conditions-costs-and-cooldowns) A cast's conditions, costs and cooldowns (2026-10-30)
- [D189](decisions/d176-d208-step-9.md#d189-walking-in-a-fight-and-tackle) Walking in a fight, and tackle (2026-10-30)
- [D190](decisions/d176-d208-step-9.md#d190-the-casts-the-server-refuses-captures-to-make) The casts the server refuses, captures to make (2026-10-30)
- [D191](decisions/d176-d208-step-9.md#d191-9c-measured-again-on-38-fights-the-owner-played-for-it) 9c measured again on 38 fights the owner played for it (2026-10-30)
- [D192](decisions/d176-d208-step-9.md#d192-the-fights-ai-and-the-fighting-activity-as-the-owner-asks-them) The fight's AI and the fighting activity, as the owner asks them (2026-10-30)
- [D193](decisions/d176-d208-step-9.md#d193-what-the-server-announces-of-a-fight-named-and-kept-mapping-format-10) What the server announces of a fight, named and kept: mapping format 10 (2026-10-30)
- [D194](decisions/d176-d208-step-9.md#d194-a-critical-hit-is-the-casts-field-8) A critical hit is the cast's field 8 (2026-10-30)
- [D195](decisions/d176-d208-step-9.md#d195-the-effects-engine-bubbles-formulas-measured-on-dofus-3) The effects' engine, Bubble's formulas measured on Dofus 3 (2026-10-30)
- [D196](decisions/d176-d208-step-9.md#d196-the-bench-of-the-effects-what-it-cannot-measure-and-the-captures-to-make) The bench of the effects, what it cannot measure, and the captures to make (2026-10-30)
- [D197](decisions/d176-d208-step-9.md#d197-9e-as-the-owner-asks-it) 9e as the owner asks it (2026-10-30)
- [D198](decisions/d176-d208-step-9.md#d198-a-fight-judged-too-risky-a-fight-lost-and-who-plays-the-players-fights) A fight judged too risky, a fight lost, and who plays the player's fights (2026-10-30)
- [D199](decisions/d176-d208-step-9.md#d199-the-characters-spells-mapping-format-11) The character's spells, mapping format 11 (2026-10-30)
- [D200](decisions/d176-d208-step-9.md#d200-foresee-for-the-search) `Foresee` for the search (2026-10-30)
- [D201](decisions/d176-d208-step-9.md#d201-the-ai-an-imagined-fight-a-judge-a-beam-search-and-the-enemies-reply) The AI: an imagined fight, a judge, a beam search and the enemies' reply (2026-10-30)
- [D202](decisions/d176-d208-step-9.md#d202-the-bot-fights-placement-turns-the-players-hand-and-harvesting) The bot fights: placement, turns, the player's hand, and harvesting (2026-10-30)
- [D203](decisions/d176-d208-step-9.md#d203-9e-measured-on-the-fights-the-owner-started-and-what-they-taught) 9e measured on the fights the owner started, and what they taught (2026-10-30)
- [D204](decisions/d176-d208-step-9.md#d204-the-repositorys-public-face-its-community-files-and-the-tool-manifest-in-config) The repository's public face, its community files and the tool manifest in `.config/` (2026-10-30)
- [D205](decisions/d176-d208-step-9.md#d205-9e2-as-the-owner-asks-it) 9e2 as the owner asks it (2026-10-30)
- [D206](decisions/d176-d208-step-9.md#d206-the-turns-and-the-fights-replayed-scriptsfightscs---turns-and---live) The turns and the fights replayed: `scripts/fights.cs --turns` and `--live` (2026-10-30)
- [D207](decisions/d176-d208-step-9.md#d207-a-critical-hit-weighed-by-its-chance) A critical hit weighed by its chance (2026-10-30)
- [D208](decisions/d176-d208-step-9.md#d208-line-of-sight-in-the-threat-and-the-approach) Line of sight in the threat and the approach (2026-10-30)
- [D209](decisions/d209-d252-step-9.md#d209-buffs-and-states-worth-what-they-do-over-the-rounds-they-last) Buffs and states worth what they do over the rounds they last (2026-10-30)
- [D210](decisions/d209-d252-step-9.md#d210-positions-over-two-turns-the-next-turn-searched) Positions over two turns: the next turn searched (2026-10-30)
- [D211](decisions/d209-d252-step-9.md#d211-the-enemies-reply-beyond-the-greedy-measured-no-gain) The enemies' reply beyond the greedy, measured: no gain (2026-10-30)
- [D212](decisions/d209-d252-step-9.md#d212-the-judges-weights-measured-on-69-fights) The judge's weights, measured on 69 fights (2026-10-30)
- [D213](decisions/d209-d252-step-9.md#d213-the-switch-the-ai-fights-kept-for-each-character-and-the-fight-played-end-to-end-in-the-tests) The switch "The AI fights" kept for each character, and the fight played end to end in the tests (2026-10-30)
- [D214](decisions/d209-d252-step-9.md#d214-the-owners-fights-for-d196-and-q16-measured) The owner's fights for D196 and Q16, measured (2026-10-30)
- [D215](decisions/d209-d252-step-9.md#d215-the-weapons-blow-in-a-session-of-its-own) The weapon's blow in a session of its own (2026-10-30)
- [D216](decisions/d209-d252-step-9.md#d216-9e2-measured-on-the-fights-the-ai-played-and-what-the-owner-saw) 9e2 measured on the fights the AI played, and what the owner saw (2026-10-30)
- [D217](decisions/d209-d252-step-9.md#d217-9e3-as-the-owner-asks-it) 9e3 as the owner asks it (2026-10-30)
- [D218](decisions/d209-d252-step-9.md#d218-the-weapons-blow-measured-on-the-captures) The weapon's blow, measured on the captures (2026-10-30)
- [D219](decisions/d209-d252-step-9.md#d219-the-packs-format-6-the-weapons) The pack's format 6, the weapons (2026-10-30)
- [D220](decisions/d209-d252-step-9.md#d220-the-weapons-blow-as-a-level-of-spell-0) The weapon's blow as a level of spell 0 (2026-10-30)
- [D221](decisions/d209-d252-step-9.md#d221-the-bot-strikes-with-its-sword-mapping-format-12) The bot strikes with its sword: mapping format 12 (2026-10-30)
- [D222](decisions/d209-d252-step-9.md#d222-9e3-measured-with-scriptsfightscs) 9e3 measured with `scripts/fights.cs` (2026-10-30)
- [D223](decisions/d209-d252-step-9.md#d223-9f-as-the-owner-asks-it) 9f as the owner asks it (2026-10-30)
- [D224](decisions/d209-d252-step-9.md#d224-the-zobals-masks-measured) The Zobal's masks, measured (2026-10-30)
- [D225](decisions/d209-d252-step-9.md#d225-a-buffs-end-mapping-format-13) A buff's end: mapping format 13 (2026-10-30)
- [D226](decisions/d209-d252-step-9.md#d226-a-caster-outside-the-zone-c-alone) A caster outside the zone: `C` alone (2026-10-30)
- [D227](decisions/d209-d252-step-9.md#d227-the-spells-effects-cast-imagined-and-a-spells-buffs-taken-back) The spells effects cast, imagined, and a spell's buffs taken back (2026-10-30)
- [D228](decisions/d209-d252-step-9.md#d228-the-judge-reads-the-criteria-on-states) The judge reads the criteria on states (2026-10-30)
- [D229](decisions/d209-d252-step-9.md#d229-9fs-states-measured-with-scriptsfightscs) 9f's states measured with `scripts/fights.cs` (2026-10-30)
- [D230](decisions/d209-d252-step-9.md#d230-the-fights-profiles-before-the-next-mechanism-and-the-ais-fights-a-harvest-profiles-option) The fights' profiles before the next mechanism, and the AI's fights a harvest profile's option (2026-10-30)
- [D231](decisions/d209-d252-step-9.md#d231-the-owners-logo-as-the-apps-icon) The owner's logo as the app's icon (2026-10-30)
- [D232](decisions/d209-d252-step-9.md#d232-9g-as-the-owner-asks-it) 9g as the owner asks it (2026-10-30)
- [D233](decisions/d209-d252-step-9.md#d233-a-fights-strategy-a-profile-of-its-own-that-each-character-plays) A fight's strategy, a profile of its own that each character plays (2026-10-30)
- [D234](decisions/d209-d252-step-9.md#d234-a-strategys-options) A strategy's options (2026-10-30)
- [D235](decisions/d209-d252-step-9.md#d235-the-fights-out-of-an-activity-a-strategys-option-the-switch-gone-from-the-card-and-the-database) The fights out of an activity, a strategy's option; the switch gone from the card and the database (2026-10-30)
- [D236](decisions/d209-d252-step-9.md#d236-the-fighting-activitys-options-and-the-monsters-icons-at-64-pixels) The fighting activity's options, and the monsters' icons at 64 pixels (2026-10-30)
- [D237](decisions/d209-d252-step-9.md#d237-the-breeds-spells-icons-in-the-pack-format-7) The breeds' spells' icons in the pack, format 7 (2026-10-30)
- [D238](decisions/d209-d252-step-9.md#d238-the-monsters-icons-and-the-sub-areas-they-live-in-format-7) The monsters' icons and the sub-areas they live in, format 7 (2026-10-30)
- [D239](decisions/d209-d252-step-9.md#d239-the-monster-groups-on-a-map-mapping-format-14) The monster groups on a map, mapping format 14 (2026-10-30)
- [D240](decisions/d209-d252-step-9.md#d240-the-strategies-made) The strategies made (2026-10-30)
- [D241](decisions/d209-d252-step-9.md#d241-the-fighting-activity-made-to-measure-in-game) The fighting activity made, to measure in game (2026-10-30)
- [D242](decisions/d209-d252-step-9.md#d242-9g-measured-with-scriptsfightscs) 9g measured with `scripts/fights.cs` (2026-10-30)
- [D243](decisions/d209-d252-step-9.md#d243-9g2-as-the-owner-asks-it) 9g2 as the owner asks it (2026-10-30)
- [D244](decisions/d209-d252-step-9.md#d244-the-strategies-removed-one-ai-for-every-character) The strategies removed, one AI for every character (2026-10-30)
- [D245](decisions/d209-d252-step-9.md#d245-what-d241-left-made-sub-areas-suggested-by-the-monsters-isolated-maps-and-the-place-chosen-on-maps-shown) What D241 left, made: sub-areas suggested by the monsters, isolated maps, and the place chosen on maps shown (2026-10-30)
- [D246](decisions/d209-d252-step-9.md#d246-the-fight-profiles-editor-as-the-harvests-and-the-monsters-picker-filtered-by-a-drop-down) The fight profile's editor as the harvest's, and the monsters' picker filtered by a drop-down (2026-10-30)
- [D247](decisions/d209-d252-step-9.md#d247-the-loots-icons-in-the-pack-and-families-of-monsters-chosen) The loot's icons in the pack, and families of monsters chosen (2026-10-30)
- [D248](decisions/d209-d252-step-9.md#d248-every-interactive-transition-of-the-world-graph-followed-a-refused-teleport-remembered-and-npcs-ways-not-yet) Every interactive transition of the world graph followed, a refused teleport remembered, and NPCs' ways not yet (2026-10-30)
- [D249](decisions/d209-d252-step-9.md#d249-a-group-judged-over-the-fights-course-and-only-the-hopeless-turned-away) A group judged over the fight's course, and only the hopeless turned away (2026-10-30)
- [D250](decisions/d209-d252-step-9.md#d250-9g2-measured-in-game-and-what-it-leaves) 9g2 measured in game, and what it leaves (2026-10-30)
- [D251](decisions/d209-d252-step-9.md#d251-a-banker-who-opens-no-bank-where-he-stands-refused-there) A banker who opens no bank where he stands, refused there (2026-10-30)
- [D252](decisions/d209-d252-step-9.md#d252-a-turns-end-the-client-marks-as-its-own-not-the-players-hand) A turn's end the client marks as its own, not the player's hand (2026-10-30)
- [D253](decisions/d253-d292-step-9.md#d253-the-ways-through-an-npcs-dialog-measured-known-by-their-ids-incarnams-portal-first) The ways through an NPC's dialog, measured, known by their ids, Incarnam's portal first (2026-11-01)
- [D254](decisions/d253-d292-step-9.md#d254-9g3-measured-in-game-and-what-it-leaves) 9g3 measured in game, and what it leaves (2026-11-01)
- [D255](decisions/d253-d292-step-9.md#d255-a-change-of-the-fighters-states-kept-in-the-turns-search) A change of the fighter's states kept in the turn's search (2026-11-01)
- [D256](decisions/d253-d292-step-9.md#d256-9g4-measured-without-the-game-and-what-the-owner-met) 9g4 measured, without the game, and what the owner met (2026-11-01)
- [D257](decisions/d253-d292-step-9.md#d257-the-food-the-inventory-holds-eaten-when-the-profile-chose-none-pack-format-8) The food the inventory holds eaten when the profile chose none: pack format 8 (2026-11-02)
- [D258](decisions/d253-d292-step-9.md#d258-a-character-with-nothing-to-eat-regenerates-back-to-its-share-alone) A character with nothing to eat regenerates back to its share alone (2026-11-02)
- [D259](decisions/d253-d292-step-9.md#d259-a-hunts-journey-stops-on-the-first-map-of-its-zone-holding-a-group-it-attacks) A hunt's journey stops on the first map of its zone holding a group it attacks (2026-11-02)
- [D260](decisions/d253-d292-step-9.md#d260-no-walk-ends-on-the-cell-the-previous-walk-ended-on-which-the-client-does-not-follow) No walk ends on the cell the previous walk ended on, which the client does not follow (2026-11-02)
- [D261](decisions/d253-d292-step-9.md#d261-9g5-measured-in-game-and-what-it-leaves) 9g5 measured in game, and what it leaves (2026-11-02)
- [D262](decisions/d253-d292-step-9.md#d262-an-element-another-harvested-grows-back-from-when-a-bot-first-saw-it-so-and-harvesting-measured-in-game) An element another harvested grows back from when a bot first saw it so, and harvesting measured in game (2026-11-02)
- [D263](decisions/d253-d292-step-9.md#d263-a-group-judged-from-the-characteristics-the-server-gives-out-of-fights-before-the-first-fight-too) A group judged from the characteristics the server gives out of fights, before the first fight too (2026-11-02)
- [D264](decisions/d253-d292-step-9.md#d264-the-harvests-automatic-place-counts-no-isolated-map-the-tour-leaves-out) The harvest's automatic place counts no isolated map the tour leaves out (2026-11-02)
- [D265](decisions/d253-d292-step-9.md#d265-every-items-icon-in-the-pack-at-32-pixels-the-owners-choice) Every item's icon in the pack, at 32 pixels, the owner's choice (2026-11-02)
- [D266](decisions/d253-d292-step-9.md#d266-9g6-measured-in-game-the-boat-to-sarakech-and-what-it-leaves) 9g6 measured in game, the boat to Sarakech, and what it leaves (2026-11-02)
- [D267](decisions/d253-d292-step-9.md#d267-after-a-lost-fight-the-hunt-turns-away-a-group-no-less-risky-the-owners-choice) After a lost fight, the hunt turns away a group no less risky, the owner's choice (2026-11-02)
- [D268](decisions/d253-d292-step-9.md#d268-9f2-as-the-owner-asks-it) 9f2 as the owner asks it (2026-11-02)
- [D269](decisions/d253-d292-step-9.md#d269-summons-measured-characteristics-turns-and-control) Summons measured: characteristics, turns and control (2026-11-02)
- [D270](decisions/d253-d292-step-9.md#d270-summons-in-the-ai-brought-judged-and-replying) Summons in the AI: brought, judged and replying (2026-11-02)
- [D271](decisions/d253-d292-step-9.md#d271-the-bot-plays-the-summons-the-client-controls) The bot plays the summons the client controls (2026-11-02)
- [D272](decisions/d253-d292-step-9.md#d272-heals-and-critical-damage-out-of-fights-not-taken) Heals and critical damage out of fights, not taken (2026-11-02)
- [D273](decisions/d253-d292-step-9.md#d273-a-fight-begun-between-two-steps-is-the-activitys) A fight begun between two steps is the activity's (2026-11-02)
- [D274](decisions/d253-d292-step-9.md#d274-9f3-as-the-owner-asks-it) 9f3 as the owner asks it (2026-11-02)
- [D275](decisions/d253-d292-step-9.md#d275-every-fights-placement-cells-kept-with-the-map-fought-on-and-the-one-left) Every fight's placement cells kept, with the map fought on and the one left (2026-11-02)
- [D276](decisions/d253-d292-step-9.md#d276-the-roublards-bombs-measured) The Roublard's bombs, measured (2026-11-02)
- [D277](decisions/d253-d292-step-9.md#d277-the-bombs-in-the-ai-set-off-walled-every-move-counted-judged) The bombs in the AI: set off, walled, every move counted, judged (2026-11-02)
- [D278](decisions/d253-d292-step-9.md#d278-9f4-as-the-owner-asks-it) 9f4 as the owner asks it (2026-11-02)
- [D279](decisions/d253-d292-step-9.md#d279-a-summons-spells-kept-apart-and-the-marks-read-mapping-format-15) A summon's spells kept apart, and the marks read: mapping format 15 (2026-11-02)
- [D280](decisions/d253-d292-step-9.md#d280-glyphs-walls-and-the-marks-as-a-turn-begins-and-ends) Glyphs, walls and the marks as a turn begins and ends (2026-11-02)
- [D281](decisions/d253-d292-step-9.md#d281-the-bombs-promise-counted-once-and-a-kill-now-worth-more) The bombs' promise counted once, and a kill now worth more (2026-11-02)
- [D282](decisions/d253-d292-step-9.md#d282-a-bombs-starting-spell-and-the-buffs-a-fighter-gave-taken-back-as-it-dies) A bomb's starting spell, and the buffs a fighter gave taken back as it dies (2026-11-02)
- [D283](decisions/d253-d292-step-9.md#d283-pushes-and-pulls-as-the-server-moves-them) Pushes and pulls as the server moves them (2026-11-02)
- [D284](decisions/d253-d292-step-9.md#d284-9f4-measured-with-scriptsfightscs) 9f4 measured with `scripts/fights.cs` (2026-11-02)
- [D285](decisions/d253-d292-step-9.md#d285-the-owners-second-roublard-capture-walls-struck-once-botte-harmless-the-search-kept) The owner's second Roublard capture: walls struck once, Botte harmless, the search kept (2026-11-02)
- [D286](decisions/d253-d292-step-9.md#d286-walls-and-glyphs-worth-the-rounds-they-strike-the-owners-observation) Walls and glyphs worth the rounds they strike, the owner's observation (2026-11-02)
- [D287](decisions/d253-d292-step-9.md#d287-9f5-as-the-owner-asks-it) 9f5 as the owner asks it (2026-11-02)
- [D288](decisions/d253-d292-step-9.md#d288-walls-and-glyphs-weighed-over-the-rounds-they-strike) Walls and glyphs weighed over the rounds they strike (2026-11-02)
- [D289](decisions/d253-d292-step-9.md#d289-pushes-along-a-diagonal-of-the-coordinates-a-step-for-two-cells-between-free-corners) Pushes along a diagonal of the coordinates, a step for two cells, between free corners (2026-11-02)
- [D290](decisions/d253-d292-step-9.md#d290-a-walls-spell-on-its-own-side-read-struck-in-the-reply-not-feared-by-the-judge) A wall's spell on its own side: read, struck in the reply, not feared by the judge (2026-11-02)
- [D291](decisions/d253-d292-step-9.md#d291-9f5-measured-and-what-it-leaves) 9f5 measured, and what it leaves (2026-11-02)
- [D292](decisions/d253-d292-step-9.md#d292-after-9f5-the-breeds-set-aside-the-apps-working-rethought-around-one-editor-blueprints-in-it-then-its-interface-recast) After 9f5, the breeds set aside: the app's working rethought around one editor, blueprints in it, then its interface recast (2026-11-02)

## Step 7 (D293 to D358)

- [D293](decisions/d293-d358-step-7.md#d293-release-040-before-the-studio) Release 0.4.0, before the studio (2026-11-02)
- [D294](decisions/d293-d358-step-7.md#d294-what-session-7as-research-found) What session 7a's research found (2026-11-02)
- [D295](decisions/d293-d358-step-7.md#d295-the-app-becomes-one-studio-as-a-game-engine-or-an-ide) The app becomes one studio, as a game engine or an IDE (2026-11-02)
- [D296](decisions/d293-d358-step-7.md#d296-the-project-assets-in-a-folder-one-file-each-referenced-by-a-stable-id) The project: assets in a folder, one file each, referenced by a stable id (2026-11-02)
- [D297](decisions/d293-d358-step-7.md#d297-a-behavior-priority-states-over-short-actions-run-as-one-activity-resumed-from-the-state) A behavior: priority states over short actions, run as one activity, resumed from the state (2026-11-02)
- [D298](decisions/d293-d358-step-7.md#d298-nodes-declared-by-the-modules-sentences-of-typed-values-their-conditions-events-and-variables) Nodes declared by the modules, sentences of typed values; their conditions, events and variables (2026-11-02)
- [D299](decisions/d293-d358-step-7.md#d299-routes-and-zones-built-on-the-worlds-map) Routes and zones, built on the world's map (2026-11-02)
- [D300](decisions/d293-d358-step-7.md#d300-profiles-variants-of-a-behavior) Profiles, variants of a behavior (2026-11-02)
- [D301](decisions/d293-d358-step-7.md#d301-fights-the-one-ai-with-preferences) Fights: the one AI, with preferences (2026-11-02)
- [D302](decisions/d293-d358-step-7.md#d302-debugging-live-step-by-step-the-history-a-simulation) Debugging: live, step by step, the history, a simulation (2026-11-02)
- [D303](decisions/d293-d358-step-7.md#d303-sharing-files-behaviors-given-a-gallery-later) Sharing: files, behaviors given, a gallery later (2026-11-02)
- [D304](decisions/d293-d358-step-7.md#d304-starting-from-zero-profiles-presets-and-runs-dropped) Starting from zero: profiles, presets and runs dropped (2026-11-02)
- [D305](decisions/d293-d358-step-7.md#d305-the-studios-look-a-dark-creation-tool-shadui-themed-again-intellijs-icons-inter-a-canvas-of-d3bs-own) The studio's look: a dark creation tool, ShadUI themed again, IntelliJ's icons, Inter, a canvas of D3B's own (2026-11-02)
- [D306](decisions/d293-d358-step-7.md#d306-step-7-in-sessions-the-studio-before-the-breeds) Step 7 in sessions, the studio before the breeds (2026-11-02)
- [D307](decisions/d293-d358-step-7.md#d307-a-behavior-drawn-as-its-states-in-a-stack-the-chosen-states-actions-on-a-canvas) A behavior drawn as its states in a stack, the chosen state's actions on a canvas (2026-11-02)
- [D308](decisions/d293-d358-step-7.md#d308-the-worlds-map-drawn-from-the-clients-own-images) The world's map drawn from the client's own images (2026-11-02)
- [D309](decisions/d293-d358-step-7.md#d309-the-characters-as-a-partys-frames-each-opened-in-a-tab-of-its-own) The characters as a party's frames, each opened in a tab of its own (2026-11-02)
- [D310](decisions/d293-d358-step-7.md#d310-the-studio-clean-never-a-catch-all) The studio clean, never a catch-all (2026-11-02)
- [D311](decisions/d293-d358-step-7.md#d311-the-blueprints-as-beautiful-and-as-practical-as-unreals) The blueprints as beautiful and as practical as Unreal's (2026-11-02)
- [D312](decisions/d293-d358-step-7.md#d312-the-migration-from-zero-confirmed-with-a-copy-of-the-database-first) The migration from zero, confirmed, with a copy of the database first (2026-11-02)
- [D313](decisions/d293-d358-step-7.md#d313-wires-of-execution-and-of-typed-data-as-unreals) Wires of execution and of typed data, as Unreal's (2026-11-02)
- [D314](decisions/d293-d358-step-7.md#d314-the-project-in-d3bbot-a-file-per-asset-its-name-the-files-a-stable-id-given-assets-made-from-the-modules) The project in `D3B.Bot`: a file per asset, its name the file's, a stable id, given assets made from the modules (2026-11-02)
- [D315](decisions/d293-d358-step-7.md#d315-how-a-behavior-runs-from-the-first-action-at-each-step-a-state-played-to-its-end-done-urgent-states-and-rules-on-the-bots-loop) How a behavior runs: from the first action at each step, a state played to its end done, urgent states and rules on the bot's loop (2026-11-02)
- [D316](decisions/d293-d358-step-7.md#d316-nodes-declared-in-c-their-values-an-activitys-settings-the-conditions-and-events-the-state-already-reads-variables-of-three-scopes) Nodes declared in C#, their values an activity's settings; the conditions and events the state already reads; variables of three scopes (2026-11-02)
- [D317](decisions/d293-d358-step-7.md#d317-profiles-as-variants-and-the-apps-profile-editor-until-the-studios) Profiles as variants, and the app's profile editor until the studio's (2026-11-02)
- [D318](decisions/d293-d358-step-7.md#d318-7b-what-it-leaves) 7b, what it leaves (2026-11-02)
- [D319](decisions/d293-d358-step-7.md#d319-7c-as-the-owner-asks-it-the-shell-designed-again-from-zero-mockup-by-mockup-then-coded-in-three-sessions) 7c as the owner asks it: the shell designed again from zero, mockup by mockup, then coded in three sessions (2026-11-02)
- [D320](decisions/d293-d358-step-7.md#d320-the-studios-look-unreals-made-modern-dark-only) The studio's look: Unreal's, made modern, dark only (2026-11-02)
- [D321](decisions/d293-d358-step-7.md#d321-all-docked-characters-over-assets-the-editor-over-the-bottom-panel-the-inspector-on-the-right) All docked: characters over assets, the editor over the bottom panel, the inspector on the right (2026-11-02)
- [D322](decisions/d293-d358-step-7.md#d322-one-title-row-menus-search-the-chosen-characters-run-block-settings) One title row: menus, search, the chosen character's run block, settings (2026-11-02)
- [D323](decisions/d293-d358-step-7.md#d323-the-characters-as-cards-portrait-state-activity-life-and-pods) The characters as cards: portrait, state, activity, life and pods (2026-11-02)
- [D324](decisions/d293-d358-step-7.md#d324-the-inspector-plain-sections-under-a-rule-faint-labels-filled-fields) The inspector, plain: sections under a rule, faint labels, filled fields (2026-11-02)
- [D325](decisions/d293-d358-step-7.md#d325-a-characters-tab-its-map-live-and-a-column-of-what-it-does) A character's tab: its map live and a column of what it does (2026-11-02)
- [D326](decisions/d293-d358-step-7.md#d326-the-assets-a-tree-of-the-projects-folders-then-the-given-ones) The assets: a tree of the project's folders, then the given ones (2026-11-02)
- [D327](decisions/d293-d358-step-7.md#d327-the-bottom-panel-journal-trace-problems-protocol-and-statistics) The bottom panel: journal, trace, problems, protocol and statistics (2026-11-02)
- [D328](decisions/d293-d358-step-7.md#d328-search-everywhere-ctrl-k) Search everywhere, Ctrl K (2026-11-02)
- [D329](decisions/d293-d358-step-7.md#d329-the-settings-a-tab-of-the-editor) The settings, a tab of the editor (2026-11-02)
- [D330](decisions/d293-d358-step-7.md#d330-a-profiles-editor-a-tab-its-settings-on-a-grid-its-preview-beside) A profile's editor, a tab: its settings on a grid, its preview beside (2026-11-02)
- [D331](decisions/d293-d358-step-7.md#d331-icons-intellijs-checked-file-by-file-the-games-own-for-the-game) Icons: IntelliJ's, checked file by file; the game's own for the game (2026-11-02)
- [D332](decisions/d293-d358-step-7.md#d332-7c-what-it-leaves) 7c, what it leaves (2026-11-02)
- [D333](decisions/d293-d358-step-7.md#d333-until-7c3-and-7c4-each-page-in-its-place-in-the-docks) Until 7c3 and 7c4, each page in its place in the docks (2026-11-02)
- [D334](decisions/d293-d358-step-7.md#d334-what-the-search-finds-in-7c2) What the search finds in 7c2 (2026-11-02)
- [D335](decisions/d293-d358-step-7.md#d335-the-studios-scale-the-mockups) The studio's scale, the mockup's (2026-11-02)
- [D336](decisions/d293-d358-step-7.md#d336-the-games-icons-the-clients-own) The game's icons, the client's own (2026-11-02)
- [D337](decisions/d293-d358-step-7.md#d337-intellijs-icons-taken-by-a-tool-drawn-by-the-apps-own-control) IntelliJ's icons taken by a tool, drawn by the app's own control (2026-11-02)
- [D338](decisions/d293-d358-step-7.md#d338-the-run-blocks-buttons-and-a-pause-from-the-app) The run block's buttons, and a pause from the app (2026-11-02)
- [D339](decisions/d293-d358-step-7.md#d339-the-windows-one-row-the-docks-remembered-the-status-bar) The window's one row, the docks remembered, the status bar (2026-11-02)
- [D340](decisions/d293-d358-step-7.md#d340-7c2-what-it-leaves) 7c2, what it leaves (2026-11-02)
- [D341](decisions/d293-d358-step-7.md#d341-7c3-as-the-owner-asks-it) 7c3 as the owner asks it (2026-11-02)
- [D342](decisions/d293-d358-step-7.md#d342-a-characters-breed-and-sex-from-the-list-mapping-format-16-and-a-migration) A character's breed and sex, from the list: mapping format 16, and a migration (2026-11-02)
- [D343](decisions/d293-d358-step-7.md#d343-the-classes-heads-and-the-clients-glyphs-in-the-pack-format-9) The classes' heads and the client's glyphs in the pack: format 9 (2026-11-02)
- [D344](decisions/d293-d358-step-7.md#d344-the-characters-panel) The characters' panel (2026-11-02)
- [D345](decisions/d293-d358-step-7.md#d345-the-inspector) The inspector (2026-11-02)
- [D346](decisions/d293-d358-step-7.md#d346-a-characters-tab) A character's tab (2026-11-02)
- [D347](decisions/d293-d358-step-7.md#d347-motions-and-no-flash-on-hover) Motions, and no flash on hover (2026-11-02)
- [D348](decisions/d293-d358-step-7.md#d348-7c3-what-it-leaves) 7c3, what it leaves (2026-11-02)
- [D349](decisions/d293-d358-step-7.md#d349-7c4-as-the-owner-asks-it-the-trace-recorded-by-the-runtime) 7c4 as the owner asks it, the trace recorded by the runtime (2026-11-03)
- [D350](decisions/d293-d358-step-7.md#d350-the-trace-a-line-per-decision-of-a-behaviors-run) The trace, a line per decision of a behavior's run (2026-11-03)
- [D351](decisions/d293-d358-step-7.md#d351-the-assets-tree-and-the-projects-folders) The assets' tree and the project's folders (2026-11-03)
- [D352](decisions/d293-d358-step-7.md#d352-a-profiles-editor-a-tab-of-its-own) A profile's editor, a tab of its own (2026-11-03)
- [D353](decisions/d293-d358-step-7.md#d353-the-bottom-panel-drawn-again) The bottom panel drawn again (2026-11-03)
- [D354](decisions/d293-d358-step-7.md#d354-the-settings-a-tab-in-sections) The settings, a tab in sections (2026-11-03)
- [D355](decisions/d293-d358-step-7.md#d355-the-pages-of-d101-and-d162-removed-lucides-icons-with-them) The pages of D101 and D162 removed, Lucide's icons with them (2026-11-03)
- [D356](decisions/d293-d358-step-7.md#d356-7c4-what-it-leaves) 7c4, what it leaves (2026-11-03)
- [D357](decisions/d293-d358-step-7.md#d357-what-the-owners-first-look-at-7c4-found) What the owner's first look at 7c4 found (2026-11-03)
- [D358](decisions/d293-d358-step-7.md#d358-the-assets-workspace-as-an-ides) The assets' workspace, as an IDE's (2026-11-03)

## Step 3 (D359 to D365)

- [D359](decisions/d359-d365-step-3.md#d359-release-050-after-7c4) Release 0.5.0, after 7c4 (2026-11-03)
- [D360](decisions/d359-d365-step-3.md#d360-client-371317-measured-against-371212) Client 3.7.13.17, measured against 3.7.12.12 (2026-11-03)
- [D361](decisions/d359-d365-step-3.md#d361-a-builds-game-mapped-from-the-previous-builds-pins-q10-settled) A build's game mapped from the previous build's pins: Q10 settled (2026-11-03)
- [D362](decisions/d359-d365-step-3.md#d362-the-graphics-learned-kept-from-build-to-build-q14-settled) The graphics learned kept from build to build: Q14 settled (2026-11-03)
- [D363](decisions/d359-d365-step-3.md#d363-a-walk-awaited-on-the-tests-clock) A walk awaited on the test's clock (2026-11-03)
- [D364](decisions/d359-d365-step-3.md#d364-371317s-captures-read-again-d60-measured-again) 3.7.13.17's captures read again, D60 measured again (2026-11-03)
- [D365](decisions/d359-d365-step-3.md#d365-the-release-client-371317) The release client-3.7.13.17 (2026-11-03)

## Step 9 (D366 to D368)

- [D366](decisions/d366-d368-step-9.md#d366-the-clients-inactive-state-measured-and-the-journal-saying-it) The client's inactive state, measured, and the journal saying it (2026-11-03)
- [D367](decisions/d366-d368-step-9.md#d367-the-clients-own-turns-ends-withheld-while-the-ai-plays-as-billbot-does) The client's own turns' ends withheld while the AI plays, as BillBot does (2026-11-03)
- [D368](decisions/d366-d368-step-9.md#d368-the-fights-pace-08-s-and-051-published-with-what-d367-left-unmeasured) The fight's pace 0.8 s, and 0.5.1 published with what D367 left unmeasured (2026-11-03)

## Step 7 (D369 to D389)

- [D369](decisions/d369-d389-step-7.md#d369-the-worlds-images-in-the-pack-jpeg-75-every-zoom-the-client-has-a-tile-an-entry) The world's images in the pack: JPEG 75, every zoom the client has, a tile an entry (2026-11-05)
- [D370](decisions/d369-d389-step-7.md#d370-7d-in-three-sessions-the-pack-and-the-mockup-first-the-view-and-the-routes-then-the-rest) 7d in three sessions: the pack and the mockup first, the view and the routes, then the rest (2026-11-05)
- [D371](decisions/d369-d389-step-7.md#d371-the-route-editors-mockup-validated-the-path-and-its-stations-as-pills) The route editor's mockup, validated; the path and its stations as pills (2026-11-05)
- [D372](decisions/d369-d389-step-7.md#d372-the-worlds-map-as-a-control-of-its-own-tiles-on-demand-a-bounded-cache-the-coarsest-zoom-always-under) The world's map as a control of its own: tiles on demand, a bounded cache, the coarsest zoom always under (2026-11-05)
- [D373](decisions/d369-d389-step-7.md#d373-routes-anchors-that-are-the-truth-the-worlds-path-between-them-and-the-layer-of-each-map) Routes: anchors that are the truth, the world's path between them, and the layer of each map (2026-11-05)
- [D374](decisions/d369-d389-step-7.md#d374-why-348-maps-stand-outside-their-image-and-what-7d2-leaves) Why 348 maps stand outside their image, and what 7d2 leaves (2026-11-05)
- [D375](decisions/d369-d389-step-7.md#d375-the-route-editor-after-the-owners-first-look-a-line-and-no-pills-two-levels-of-marks-a-start-that-reads-undo-and-the-keys) The route editor after the owner's first look: a line and no pills, two levels of marks, a start that reads, undo and the keys (2026-11-05)
- [D376](decisions/d369-d389-step-7.md#d376-7d-in-four-sessions-the-actions-and-the-run-first-then-recording-and-the-zones) 7d in four sessions: the actions and the run first, then recording and the zones (2026-11-05)
- [D377](decisions/d369-d389-step-7.md#d377-7d3-in-two-sessions-the-actions-and-the-runs-course-first-then-the-actions-that-talk-to-the-game) 7d3 in two sessions: the actions and the run's course first, then the actions that talk to the game (2026-11-05)
- [D378](decisions/d369-d389-step-7.md#d378-a-routes-actions-are-nodes-of-the-catalog-edited-with-the-settings-own-editor) A route's actions are nodes of the catalog, edited with the settings' own editor (2026-11-05)
- [D379](decisions/d369-d389-step-7.md#d379-running-a-route-the-characters-own-path-a-cursor-a-node) Running a route: the character's own path, a cursor, a node (2026-11-05)
- [D380](decisions/d369-d389-step-7.md#d380-an-actions-values-are-tokens-of-its-sentence) An action's values are tokens of its sentence (2026-11-05)
- [D381](decisions/d369-d389-step-7.md#d381-actions-are-added-from-a--that-opens-a-dialog) Actions are added from a "+" that opens a dialog (2026-11-05)
- [D382](decisions/d369-d389-step-7.md#d382-7d3b-learns-the-npcs-the-actions-that-talk-to-the-game-are-7d3c) 7d3b learns the NPCs; the actions that talk to the game are 7d3c (2026-11-05)
- [D383](decisions/d369-d389-step-7.md#d383-the-npcs-of-the-maps-and-their-dialogs-learned-from-the-game-by-ids-and-the-way-they-face) The NPCs of the maps and their dialogs, learned from the game by ids, and the way they face (2026-11-05)
- [D384](decisions/d369-d389-step-7.md#d384-the-routes-actions-that-talk-to-the-game-play-and-an-npc-is-picked-among-what-the-bots-learned) The route's actions that talk to the game play, and an NPC is picked among what the bots learned (2026-11-06)
- [D385](decisions/d369-d389-step-7.md#d385-the-npcs-names-and-the-dialogs-texts-are-in-the-pack-format-11) The NPCs' names and the dialogs' texts are in the pack, format 11 (2026-11-06)
- [D386](decisions/d369-d389-step-7.md#d386-a-profile-plays-a-list-of-steps-routes-and-actions-in-order-made-in-the-app) A profile plays a list of steps: routes and actions in order, made in the app (2026-11-06)
- [D387](decisions/d369-d389-step-7.md#d387-the-fighting-activity-attacks-every-group-the-settings-allow-with-no-judgement-of-its-strength) The fighting activity attacks every group the settings allow, with no judgement of its strength (2026-11-06)
- [D388](decisions/d369-d389-step-7.md#d388-the-fighting-tour-leaves-out-the-interiors-of-a-sub-area-that-also-holds-maps-on-a-world-map) The fighting tour leaves out the interiors of a sub-area that also holds maps on a world map (2026-11-06)
- [D389](decisions/d369-d389-step-7.md#d389-the-harvests-given-profiles-open-the-resource-bags) The harvest's given profiles open the resource bags (2026-11-06)

## Step 9 (D390 to D392)

- [D390](decisions/d390-d392-step-9.md#d390-the-ai-weighs-next-turns-damage-a-little-more-so-that-it-flees-less) The AI weighs next turn's damage a little more, so that it flees less (2026-11-06)
- [D391](decisions/d390-d392-step-9.md#d391-the-automatic-profiles-go-through-several-sub-areas-in-turn-and-the-harvest-varies-its-professions) The automatic profiles go through several sub-areas in turn, and the harvest varies its professions (2026-11-08)
- [D392](decisions/d390-d392-step-9.md#d392-the-harvests-journey-stops-on-a-crossed-map-where-a-wanted-element-is-offered-and-the-harvest-rotates-through-ten-sub-areas) The harvest's journey stops on a crossed map where a wanted element is offered, and the harvest rotates through ten sub-areas (2026-11-08)

## Architecture (D393)

- [D393](decisions/d393-architecture.md#d393-the-studios-model-leaves-d3bbot-for-a-project-of-its-own-d3bstudio) The studio's model leaves `D3B.Bot` for a project of its own, `D3B.Studio` (2026-11-08)

## Step 7, 7d4 (D394)

- [D394](decisions/d394-step-7-7d4.md#d394-7d4-split-what-needs-no-client-now-undo-keys-dragging-zones-recording-and-simplify-after-the-measure-in-game) 7d4 split: what needs no client now (undo, keys, dragging, zones), recording and Simplify after the measure in game (2026-11-09)

## Step 7, the redesign (D395)

- [D395](decisions/d395-d397-step-7-the-redesign.md#d395-7r-the-studios-redesign-what-hurts-today-and-the-studio-seen-as-a-game-engine) 7r, the studio's redesign: what hurts today, and the studio seen as a game engine (2026-11-09)
- [D396](decisions/d395-d397-step-7-the-redesign.md#d396-the-studio-as-a-game-engine-and-a-routes-steps-set-in-a-sequencer-the-owners-choices) The studio as a game engine, and a route's steps set in a sequencer: the owner's choices (2026-11-09)
- [D397](decisions/d395-d397-step-7-the-redesign.md#d397-7r2-the-engines-frame-the-world-viewport-the-outliner-the-details-panel-and-play-in-editor) 7r2, the engine's frame: the world viewport, the Outliner, the Details panel and Play in Editor (2026-11-09)

## Claude Code setup (D398)

- [D398](decisions/d398-claude-code-setup.md#d398-the-projects-claude-code-setup-is-versioned-with-hooks-in-c-and-tests) The project's Claude Code setup is versioned, with hooks in C# and tests (2026-11-09)

## Tidy-up (D399 to D401)

- [D399](decisions/d399-d401-tidy-up.md#d399-d3bbot-stays-one-project-and-is-cut-by-files-and-the-six-files-over-900-lines-are-split) D3B.Bot stays one project and is cut by files, and the six files over 900 lines are split (2026-11-09)
  - `D3B.Bot.Harvesting` and `D3B.Bot.Fighting` as projects of their own: a new surface to decide for about 4,700 lines.
  - Layers by namespace guarded by an architecture test: the owner chose the files only.
- [D400](decisions/d399-d401-tidy-up.md#d400-benchmarkdotnet-is-dropped) BenchmarkDotNet is dropped (2026-11-09)
  - Removing the fight scripts too: they measure the AI's quality, not its speed.
- [D401](decisions/d399-d401-tidy-up.md#d401-decisionsmd-is-cut-into-files-by-group-of-the-index) `decisions.md` is cut into files by group of the index (2026-11-09)
  - Condensing old entries: it breaks the append-only rule and loses the reason.

## Banking settings (D405)

- [D405](decisions/d405-banking-settings.md#d405-banking-one-general-share-of-pods-and-zaap-rule-a-profiles-own-on-request-and-a-journey-that-outlives-a-refused-zaap) Banking: one general share of pods and zaap rule, a profile's own on request, and a journey that outlives a refused zaap (2026-11-09)

## The home page (D406)

- [D406](decisions/d406-the-home-page.md#d406-the-studio-opens-on-a-home-page-the-world-appears-when-a-route-or-a-zone-is-selected) The studio opens on a home page; the world appears when a route or a zone is selected (2026-11-09)
  - Opening on the last tab left, and a "recent" list kept in `app.json`: the first brings the world back at once, the second is a file to keep for what the file system says.

## Step 9, the players' reports (D407)

- [D407](decisions/d407-d408-step-9-the-players-reports.md#d407-the-fight-ai-after-the-players-reports-a-refused-cast-is-weighed-anew-a-life-steal-is-no-longer-cast-for-a-heal-that-is-not-needed) The fight AI after the players' reports: a refused cast is weighed anew, a life steal is no longer cast for a heal that is not needed (2026-11-09)
- [D408](decisions/d407-d408-step-9-the-players-reports.md#d408-a-summon-is-a-last-resort-but-for-the-breeds-made-of-their-summons) A summon is a last resort, but for the breeds made of their summons (2026-11-09)

## Bug reports (D409 to D411)

- [D409](decisions/d409-d411-bug-reports.md#d409-the-game-connection-is-recorded-by-default-with-a-retention) The game connection is recorded by default, with a retention (2026-11-09)
- [D410](decisions/d409-d411-bug-reports.md#d410-a-bug-report-is-a-d3breport-bundle-sent-by-a-private-discord-webhook) A bug report is a `.d3breport` bundle, sent by a private Discord webhook (2026-11-09)
- [D411](decisions/d409-d411-bug-reports.md#d411-a-report-carries-an-optional-message-of-the-players-own) A report carries an optional message of the player's own (2026-11-09)

## The home page's tab (D412)

- [D412](decisions/d412-the-home-pages-tab.md#d412-the-home-page-closes-like-the-other-tabs-and-the-window-menu-opens-it-again) The home page closes like the other tabs, and the Window menu opens it again (2026-11-09)
  - Keeping it pinned and only widening the label's padding: the owner asked for a cross.

## Step 7, hints on the world map (D414)

- [D414](decisions/d414-step-7-hints-on-the-world-map.md#d414-the-world-map-draws-the-clients-hints-its-own-table-and-pictures-in-the-pack-format-12-a-layer-of-its-own-with-a-tick-for-each-category) The world map draws the client's hints: its own table and pictures in the pack, format 12, a layer of its own with a tick for each category (2026-11-09)
  - Only the table with D3B's own vector icons.
  - Six kinds on and three off by default, with kinds of our own.
  - A hint as a selection of the world: left for later, to leave 7r3's ground alone.

## Step 7, 7r3, the Sequencer (D413)

- [D413](decisions/d413-step-7-7r3-the-sequencer.md#d413-7r3-the-sequencer-a-routes-steps-as-clips-on-tracks-set-by-gestures-keys-and-the-details) 7r3, the Sequencer: a route's steps as clips on tracks, set by gestures, keys and the Details (2026-11-09)
  - Storing the clips in the route's file.
  - Rebuilding the steps' actions from the clips at each edit.
  - Dropping the actions' cards of a step from the Details.
  - A track for each of the eight kinds at all times.

## Step 9, the harvest's interiors (D416)

- [D416](decisions/d416-step-9-the-harvests-interiors.md#d416-the-harvests-tour-goes-into-an-interior-of-a-town-or-a-field-only-when-something-says-it-holds-a-resource) The harvest's tour goes into an interior of a town or a field only when something says it holds a resource (2026-11-09)
  - The interiors left out always, a setting "Go into the buildings", the journeys avoiding interiors.

## Step 9, the harvest's tour and the levels (D417)

- [D417](decisions/d417-step-9-the-harvests-levels.md#d417-the-harvests-tour-weighs-the-characters-level-a-graphic-that-offers-only-what-it-cannot-harvest-makes-no-map-probable) The harvest's tour weighs the character's level: a graphic that offers only what it cannot harvest makes no map probable (2026-11-09)
  - An outdoor map left out for the level, the growing back weighed by the level, the levels given once when the tour is made.

## Step 7, 7r5, the profile's editor (D415)

- [D415](decisions/d415-step-7-7r5-the-profile-editor.md#d415-7r5-a-profile-names-a-zone-as-its-place-a-third-value-of-the-place-and-a-zone-setting-read-through-a-seam) 7r5, a profile names a zone as its place: a third value of the place and a zone setting, read through a seam (2026-11-09)
  - The zone replacing the area and the sub-areas, stored profiles converted into zone files.
  - Painting a new zone from the profile's tab.
  - Folding nothing, or folding Deposit and the heal's share too.
  - One pull request for the zones and the editor.
- [D418](decisions/d415-step-7-7r5-the-profile-editor.md#d418-7r5b-the-profile-as-an-asset-editor-four-categories-an-advanced-fold-counted-by-what-differs-the-worlds-map-beside-the-form) 7r5b, the profile as an asset editor: four categories, an Advanced fold counted by what differs, the world's map beside the form (2026-11-10)
  - The fold state kept in the profile or in app.json.
  - A pick on the press instead of the click.
  - The tour drawn over the map in place of the thumbnail.

## Step 7, 7r4, following a run (D419)

- [D419](decisions/d419-step-7-7r4-following-a-run.md#d419-7r4-following-a-run-the-run-publishes-where-it-is-in-routeruns-the-app-reads-it-and-a-column-lit-is-the-playhead) 7r4, following a run: the run publishes where it is in `RouteRuns`, the app reads it, and a column lit is the playhead (2026-11-10)
  - A route's position in `ActivityReport`, the journal's events as the way the app learns it, a marker moved between columns by an estimate, only the chosen character followed, a journal line at each lap.

## Step 9, the Pandawa's drink (D420)

- [D420](decisions/d420-step-9-the-pandawas-drink.md#d420-a-state-the-fighter-already-held-is-no-gain-when-a-cast-puts-it-back-the-pandawa-no-longer-swaps-sobre-and-saoul-instead-of-striking) A state the fighter already held is no gain when a cast puts it back: the Pandawa no longer swaps Sobre and Saoul instead of striking (2026-11-10)

## Step 7, 7r6, repeats and sequences (D421)

- [D421](decisions/d421-step-7-7r6-repeats-and-sequences.md#d421-7r6-a-step-of-a-list-repeats-once-a-number-of-times-until-a-condition-and-plays-another-list-the-repeat-is-on-the-step) 7r6, a step of a list repeats (once, a number of times, until a condition) and plays another list: the repeat is on the step (2026-11-10)
  - Laps and "until" on the route's asset, a container "Repeat" step, a fixed list of "until" kinds, a played list expanded at compile time, the condition read between two steps of a route, a new kind of setting for the condition.
## Bug reports, BR1 (D422)

- [D422](decisions/d422-bug-reports-br1-keys.md#d422-two-settings-for-the-captures-the-game-is-recorded-unless-turned-off-every-connection-only-when-asked-the-retention-is-applied-whether-or-not-the-game-is-recorded) Two settings for the captures: the game is recorded unless turned off, every connection only when asked; the retention is applied whether or not the game is recorded (2026-11-10)

## Bug reports, the cleaner (D423)

- [D423](decisions/d423-bug-reports-cleaner.md#d423-a-reports-capture-is-cleaned-by-value-the-ticket-and-the-clients-opaque-strings-by-shape-the-accounts-characters-names-wherever-they-occur) A report's capture is cleaned by value: the ticket and the client's opaque strings by shape, the account's characters' names wherever they occur (2026-11-10)

## Step 9, the harvest and the zones of a map (D424, D425)

- [D424](decisions/d424-step-9-the-harvests-zones.md#d424-a-resource-is-used-only-from-a-cell-beside-it-in-the-characters-zone-one-in-another-zone-of-its-map-is-out-of-reach-and-the-tour-will-go-there-through-the-world-graph) A resource is used only from a cell beside it in the character's zone; one in another zone of its map is out of reach, and the tour will go there through the world graph (2026-11-10)
  - The far cell kept for an element beside another zone, going to the other zone in the same change, skipping silently.
- [D425](decisions/d424-step-9-the-harvests-zones.md#d425-the-harvests-tour-goes-into-another-zone-of-a-map-as-one-more-place-of-its-tour-once-per-tour-by-a-goal-of-vertices-its-sets-stay-keyed-by-map) The harvest's tour goes into another zone of a map as one more place of its tour, once per tour, by a goal of vertices; its sets stay keyed by map (2026-11-10)
  - Keying every set of the tour by vertex, visiting the other zone before the map is done, wanting every zone an element borders, going whatever the way costs.

## Client 3.7.14.18 (D427, D428)

- [D427](decisions/d427-d428-client-3-7-14-18.md#d427-client-371418-measured-against-371317) Client 3.7.14.18, measured against 3.7.13.17 (2026-11-17)
- [D428](decisions/d427-d428-client-3-7-14-18.md#d428-371418-mapped-from-371317s-pins-without-its-own-captures-and-released) 3.7.14.18 mapped from 3.7.13.17's pins without its own captures, and released (2026-11-17)
  - Capturing first, as D364 did; 3.7.13.17's mapping file renamed for the new build.

## Step 10, groups (D429 to D435)

- [D429](decisions/d429-d435-step-10-groups.md#d429-step-10-starts-now-on-the-c-version-in-four-sessions) Step 10 starts now, on the C# version, in four sessions (2026-11-18)
  - Coding after step 7's measure in game, six sessions, the fights first with a party made by hand.
- [D430](decisions/d429-d435-step-10-groups.md#d430-a-follower-plays-a-follow-profile-which-names-its-master) A follower plays a "Follow" profile, which names its master (2026-11-18)
  - A group as a record of the accounts page, a group as an asset of the studio.
- [D431](decisions/d429-d435-step-10-groups.md#d431-the-party-is-the-bots-the-games-own-following-off-a-fight-joined-by-request) The party is the bot's, the game's own following off, a fight joined by request (2026-11-18)
  - The party made by hand, no party in game, the game joining the follower by itself.
- [D432](decisions/d429-d435-step-10-groups.md#d432-the-group-moves-in-a-chain-and-waits-for-anyone-missing) The group moves in a chain, and waits for anyone missing (2026-11-18)
  - Waiting only before an attack, each by its own path, going on without the missing, a follower leading.
- [D433](decisions/d429-d435-step-10-groups.md#d433-a-full-member-takes-the-whole-group-to-the-masters-bank) A full member takes the whole group to the master's bank (2026-11-18)
  - Each at its own bank, the kamas given too, a setting of the profile.
- [D434](decisions/d429-d435-step-10-groups.md#d434-the-ai-unchanged-in-a-group-then-measured) The AI unchanged in a group, then measured (2026-11-18)
  - A shared target from the first version.
- [D435](decisions/d429-d435-step-10-groups.md#d435-10a-the-partys-protocol-read-from-the-owners-captures-mapping-format-18) 10a, the party's protocol read from the owner's captures, mapping format 18 (2026-11-18)
  - The two options named from the references' order, every party message mapped.

## Bug reports, the profile's zaaps (D436)

- [D436](decisions/d436-bug-reports-zaaps.md#d436-a-profile-that-takes-no-zaap-takes-none-on-any-of-its-journeys-to-its-place-back-and-to-the-bank) A profile that takes no zaap takes none on any of its journeys, to its place, back and to the bank (2026-11-18)
  - D405's meaning kept, two flags, "never" as a number of maps.

## Step 10, 10b: the party by the bot and the chain (D437)

- [D437](decisions/d437-step-10-the-chain.md#d437-10b-the-party-by-the-bot-the-follow-activity-and-the-group-moving-in-a-chain) 10b, the party by the bot, the "Follow" activity, and the group moving in a chain (2026-11-18)
  - The wait in each activity, replaying the master's exact transition, the master's own haven bag, the party not waited for.

## Fights, the challenges' bonus (D438)

- [D438](decisions/d438-fights-challenges-bonus.md#d438-a-profile-chooses-the-challenges-bonus-of-its-fights-the-drops-by-default-mapping-format-19) A profile chooses the challenges' bonus of its fights, the drops by default, mapping format 19 (2026-11-18)
  - Two values, one box, another default, under "Advanced" or a section of its own, a general setting.

## Bug reports, the harvest on the way (D439)

- [D439](decisions/d439-harvest-on-the-way.md#d439-the-harvests-journey-stops-on-any-map-it-crosses-where-the-server-offers-a-resource-of-the-profile-of-the-zone-or-not) The harvest's journey stops on any map it crosses where the server offers a resource of the profile, of the zone or not (2026-11-18)

## Step 10, 10c1: the group's fights (D440)

- [D440](decisions/d440-step-10-the-groups-fights.md#d440-10c1-a-follower-joins-its-masters-fight-by-request-the-master-ready-once-its-followers-are-placed) 10c1, a follower joins its master's fight by request, the master ready once its followers are placed (2026-11-18)
  - The master ready once placed, a follower setting its team's options, the fights shown on a map read.

## Step 10, 10c2: the group's life before an attack (D441)

- [D441](decisions/d441-step-10-the-groups-life.md#d441-10c2-the-master-attacks-once-each-followers-life-is-over-its-profiles-share-each-follower-healing-by-itself) 10c2, the master attacks once each follower's life is over its profile's share, each follower healing by itself (2026-11-18)
  - Regenerating only, heal settings in "Follow", a fixed share or the follower's own, the life waited for before the attack alone.

## Step 10, 10c3: the group's bank (D442)

- [D442](decisions/d442-step-10-the-groups-bank.md#d442-10c3-a-full-member-takes-the-group-to-the-bank-each-follower-gives-the-master-what-it-carries-by-an-exchange-mapping-format-20) 10c3, a full member takes the group to the bank, each follower gives the master what it carries by an exchange, mapping format 20 (2026-11-18)
  - All the master does not wear, two openings of the bank, the step counted from requests, one transfer of every stack.

## Fights, the companions (D443)

- [D443](decisions/d443-fights-companions.md#d443-the-bot-plays-the-characters-companion-such-as-the-astrub-knight-with-the-spells-the-server-lists-for-it-mapping-format-21) The bot plays the character's companion, such as the Astrub Knight, with the spells the server lists for it, mapping format 21 (2026-11-18)
  - Ending its turn at once, measuring first, a companion table in the pack.

## The harvest, the mines (D444)

- [D444](decisions/d444-harvest-the-mines.md#d444-the-harvests-tour-goes-into-an-interior-while-its-sub-area-lists-a-resource-no-map-of-it-showed-so-that-it-finds-the-mines) The harvest's tour goes into an interior while its sub-area lists a resource no map of it showed, so that it finds the mines (2026-11-18)
  - The interiors whenever the zone wants ore, learned graphics shipped with the app, D416 kept as it is.

## Groups, the followers paused in a fight (D445)

- [D445](decisions/d445-groups-followers-not-ready.md#d445-a-readiness-taken-back-while-the-character-is-not-ready-is-the-clients-own-not-the-players) A readiness taken back while the character is not ready is the client's own, not the player's (2026-11-18)
  - Every readiness of a follower ignored, a follower resumed by itself after its fight.

## Groups, the challenges' bonus of the followers (D446)

- [D446](decisions/d446-groups-challenges-bonus.md#d446-a-follower-chooses-the-challenges-bonus-its-masters-profile-chooses) A follower chooses the challenges' bonus its master's profile chooses (2026-11-18)
  - A bonus of its own in "Follow".

## Groups, following in real time (D447)

- [D447](decisions/d447-groups-following-in-real-time.md#d447-the-followers-copy-their-masters-walks-as-they-come-and-the-master-no-longer-waits-after-each-map-change) The followers copy their master's walks as they come, and the master no longer waits after each map change (2026-11-19)
  - Leaving together, walking to the exit only, copying only the exits, a follower that missed a walk waiting.

## Groups, the followers into their master's fight (D448)

- [D448](decisions/d448-groups-followers-into-the-fight.md#d448-the-master-waits-for-its-followers-on-the-map-its-fight-began-from-and-they-walk-beside-it) The master waits for its followers on the map its fight began from, and they walk beside it (2026-11-19)
  - The same cell as the master, the step aside added alone.

## Groups, joining from where they stand (D449)

- [D449](decisions/d449-groups-join-where-they-stand.md#d449-a-follower-does-not-copy-its-masters-walk-to-a-group-of-monsters-and-no-two-followers-walk-to-one-cell) A follower does not copy its master's walk to a group of monsters, and no two followers walk to one cell (2026-11-19)
  - Cutting a copied walk short as the fight is told.

## Fights, the placement's answer (D450)

- [D450](decisions/d450-fights-placement-answer.md#d450-the-ai-waits-a-second-at-most-for-its-placements-answer) The AI waits a second at most for its placement's answer (2026-11-19)
  - Mapping the swap's event now, avoiding the allies' cells.

## Groups, the master's exit taken straight (D451)

- [D451](decisions/d451-groups-direct-exit.md#d451-to-its-masters-exit-a-follower-walks-to-an-exit-of-its-own-and-a-character-on-an-exit-leaves-from-it) To its master's exit a follower walks to an exit of its own, and a character on an exit leaves from it (2026-11-19)
  - Copying the walk to the master's very exit cell.

## Groups, the master emptied at the bank (D452)

- [D452](decisions/d452-group-bank-master-empties.md#d452-at-the-groups-bank-the-master-deposits-all-it-does-not-wear-first-and-again-when-an-exchange-does-not-conclude) At the group's bank the master deposits all it does not wear first, and again when an exchange does not conclude (2026-11-19)
  - The master depositing by its profile's rule.

## Groups, the master's door (D453)

- [D453](decisions/d453-groups-door.md#d453-a-follower-does-not-copy-its-masters-walk-to-a-door-or-a-cell-that-moves-it-and-takes-that-way-itself) A follower does not copy its master's walk to a door or a cell that moves it, and takes that way itself (2026-11-19)
  - Copying the walk to the door's cell itself.

## Fights, a little bolder in a group (D454)

- [D454](decisions/d454-group-fights-bolder.md#d454-beside-allies-of-its-group-the-ai-weighs-the-enemies-threat-less-and-its-next-blows-more) Beside allies of its group, the AI weighs the enemies' threat less and its next blows more (2026-11-19)
  - Waiting for 10d's measure, the prudent weights left out in a group.

## Groups, the windows the bot answered (D455)

- [D455](decisions/d455-groups-windows-left-open.md#d455-the-windows-of-an-invitation-or-an-exchange-the-bot-answered-stay-open-closed-by-hand) The windows of an invitation or an exchange the bot answered stay open, closed by hand (2026-11-19)
  - Withholding the event that opens them, injecting the event that closes them.

## Groups, one cell each (D456)

- [D456](decisions/d456-groups-one-cell-each.md#d456-no-two-bots-of-the-host-walk-to-one-cell-at-once-a-walk-the-server-holds-is-let-go-only-by-a-walk-and-a-follower-never-goes-round-while-its-master-is-one-map-away) No two bots of the host walk to one cell at once, a walk the server holds is let go only by a walk, and a follower never goes round while its master is one map away (2026-11-19)
  - The followers in a chain, the game's own following, the master's cells copied, a longer delay, waiting on a bot standing on the cell.

## Groups, the invitation at once and a stack the bank refuses (D457)

- [D457](decisions/d457-groups-invite-and-bank-refusal.md#d457-the-master-invites-its-followers-at-its-first-request-and-a-stack-the-bank-refuses-stops-not-the-groups-unloading) The master invites its followers at its first request, and a stack the bank refuses stops not the group's unloading (2026-11-19)
  - Waiting for the followers to accept before the first walk, leaving out a quest's objects by their type.

## Groups, the exchange's pace and the pods seen first (D458)

- [D458](decisions/d458-group-exchange-pace.md#d458-a-follower-says-it-is-ready-35-s-after-the-exchanges-last-change-and-an-activity-starting-plays-nothing-before-its-run-looks-at-the-pods) A follower says it is ready 3.5 s after the exchange's last change, and an activity starting plays nothing before its run looks at the pods (2026-11-19)
  - The stacks in one transfer, unmeasured; the moves with no display time; the master waiting longer for the readiness.

## Groups, the bank's rounds and the food (D459)

- [D459](decisions/d459-group-bank-rounds-and-food.md#d459-a-stack-an-exchange-leaves-is-offered-no-more-in-that-unloading-and-a-profile-banking-all-but-what-is-worn-banks-the-groups-food-too) A stack an exchange leaves is offered no more in that unloading, and a profile banking all but what is worn banks the group's food too (2026-11-19)
  - A new setting for the food; leaving out the refused stacks by their kind.

## Fights, a blow now worth more than a blow later (D460)

- [D460](decisions/d460-group-fights-strike-now.md#d460-the-ai-weighs-next-turns-damage-under-1-in-a-group-too-so-that-it-strikes-now-what-it-could-strike-later) The AI weighs next turn's damage under 1, in a group too, so that it strikes now what it could strike later (2026-11-19)
  - D454 undone whole, a rule by element, the approach clamped silently in the judge.

## Fights, the challenges read (D461)

- [D461](decisions/d461-fights-challenges-read.md#d461-the-bot-reads-its-fights-challenges-their-targets-their-results-and-the-proposals-mapping-format-22) The bot reads its fight's challenges, their targets, their results and the proposals, mapping format 22 (2026-11-19)
  - The challenges' table in the pack now, the mode and number events, a target with no fighter.

## Fights, the Forgelance's lances (D462)

- [D462](decisions/d462-forgelance-lances.md#d462-the-forgelances-summons-are-not-a-last-resort-its-lances-striking-as-they-are-thrown) The Forgelance's summons are not a last resort, its lances striking as they are thrown (2026-11-19)
  - A criterion by effect, the summon as a spell's only worth; D408 undone for every breed.

## Fights, the AI measured breed by breed (D463)

- [D463](decisions/d463-fights-by-breed.md#d463-scriptsfightscs---turns-and---live-print-their-main-counts-by-the-characters-breed) `scripts/fights.cs --turns` and `--live` print their main counts by the character's breed (2026-11-19)
  - The study's keys in the sorted counts, a breed's name from the pack, a switch to print the breeds.

## Groups, a follower the server holds (D464)

- [D464](decisions/d464-groups-held-follower.md#d464-a-refusal-out-of-a-fight-says-the-server-may-hold-a-walk-and-a-follower-whose-change-keeps-failing-from-one-cell-steps-aside) A refusal out of a fight says the server may hold a walk, and a follower whose change keeps failing from one cell steps aside (2026-11-19)
  - A confirmation told by its delay, a journey round, a master that stops waiting, every refusal held in a fight too.

## Fights, the Forgelance's lance in hand (D465)

- [D465](decisions/d465-forgelance-lance-state.md#d465-a-thrown-lance-is-out-of-the-forgelances-hand-until-its-next-turn-a-measured-rule-the-pack-does-not-hold) A thrown lance is out of the Forgelance's hand until its next turn, a measured rule the pack does not hold (2026-11-19)
  - A rule read from the pack, a summon's client-only state given to its summoner, the refusals left to D407.

## Fights, the easy challenges (D466)

- [D466](decisions/d466-fights-challenges-easy.md#d466-the-ai-plays-for-the-easy-challenges-those-of-its-own-turns-while-a-turn-costs-no-more-than-their-bonus-pays-pack-format-13) The AI plays for the easy challenges, those of its own turns, while a turn costs no more than their bonus pays, pack format 13 (2026-11-19)
  - Hardi and Barbare among them, keeping a challenge whatever it costs, the whole balance as the price, one search with a penalty, matching by number.

## Fights, the group's reply and its target (D467)

- [D467](decisions/d467-group-reply-target.md#d467-an-enemy-of-the-reply-holds-every-character-of-a-group-as-dear-as-the-fighter-playing-as-real-monsters-do) An enemy of the reply holds every character of a group as dear as the fighter playing, as real monsters do (2026-11-19)
  - The fighter playing's life counted once, a group or not; a weight by breed or the group's weights; the allies' spells and turns in the same change.

## Fights, an Eliotrope's portals (D468)

- [D468](decisions/d468-eliotrope-portals.md#d468-an-eliotropes-portals-in-the-ai-cast-through-by-every-character-of-the-bot) An Eliotrope's portals in the AI, cast through by every character of the bot (2026-11-19)
  - Capturing by hand first, casting only through the portals set, every exact set of portals kept, a worth of an open way.

## Docs, the README for players (D469)

- [D469](decisions/d469-readme-for-players.md#d469-the-readme-written-for-players-building-and-maintaining-d3b-in-docsdevelopmentmd) The README written for players, building and maintaining D3B in docs/development.md (2026-11-20)
  - Trimming the README in place, moving the commands out of it too.

## Fights, points below 0 and the approach within reach (D470, D472)

- [D470](decisions/d470-fights-phantom-points-and-nearing.md#d470-a-malus-of-ap-or-mp-takes-the-fighters-points-below-0-as-the-server-keeps-them-the-pandawa-no-longer-swaps-its-states-twice-for-a-point-it-never-had) A malus of AP or MP takes the fighter's points below 0, as the server keeps them: the Pandawa no longer swaps its states twice for a point it never had (2026-11-20)
  - Recording the malus as only what it took from the points.
- [D472](decisions/d470-fights-phantom-points-and-nearing.md#d472-within-its-spells-range-and-mp-out-of-their-sight-each-cell-nearer-an-enemy-still-counts-the-characters-walk-instead-of-passing-their-turns) Within its spells' range and MP, out of their sight, each cell nearer an enemy still counts: the characters walk instead of passing their turns (2026-11-20)
  - Ending a tie on the walk nearest the enemies; the reply's blows foreseen too high as the cause; a hundredth a cell; a larger share.

## Groups, the party kept and the fight's lock (D471)

- [D471](decisions/d471-groups-party-kept-and-lock.md#d471-a-party-holding-the-master-and-its-followers-is-kept-whoever-leads-it-a-master-with-followers-never-closes-its-fight-and-opens-one-the-server-kept-closed) A party holding the master and its followers is kept whoever leads it, a master with followers never closes its fight, and opens one the server kept closed (2026-11-20)
  - The lead passed to the master, unmapped; an invitation into a party another leads, unmeasured; a join sent again after a refusal.

## Fights, the Forgelance's Trident and the fork (D473)

- [D473](decisions/d473-forgelance-trident-fork.md#d473-a-fork-reaches-a-row-past-its-size-each-row-a-step-of-its-decrease) A fork reaches a row past its size, each row a step of its decrease (2026-11-20)
  - Correcting the pull towards a cell in this session; a fork of size n reaching n rows.

## Fights, the Pandawa's carrying (D474)

- [D474](decisions/d474-pandawa-carrying.md#d474-the-pandawas-carrying-in-the-ai-a-carry-and-its-throw-within-one-turn-as-the-references-model-it) The Pandawa's carrying in the AI, a carry and its throw within one turn, as the references model it (2026-11-20)
  - Keeping a carry from one turn to the next, capturing first, a worth of a fighter placed, allies only, Porteur's boosts, `K` counted half.

## Groups, a follower waits for its master's fight's options (D475)

- [D475](decisions/d475-follower-waits-fight-options.md#d475-a-follower-joins-its-masters-fight-once-the-server-listed-it-open) A follower joins its master's fight once the server listed it open (2026-11-20)
  - Waiting for the turns' order, sending the join again after a refusal.

## Groups, the game's own following for a subscribed group (D476)

- [D476](decisions/d476-groups-game-following.md#d476-a-group-the-game-can-follow-turns-its-options-on-and-the-bot-watches-each-follower) A group the game can follow turns its options on, and the bot watches each follower (2026-11-20)
  - The fights' options only, measuring first, the master's "follow me" alone.

## Groups, the game's following not paused and its readiness left to the server (D477)

- [D477](decisions/d477-game-following-pause-and-ready.md#d477-a-follower-the-game-moves-is-not-paused-by-its-clients-walks-and-leaves-its-readiness-to-the-server) A follower the game moves is not paused by its client's walks, and leaves its readiness to the server (2026-11-20)
  - Telling the client's walks by their delay, keeping the followers' readiness.

## Fights, the placement weighed by the turn order (D478)

- [D478](decisions/d478-placement-turn-order.md#d478-a-placement-weighs-only-the-threat-of-the-enemies-playing-before-the-characters-first-turn) A placement weighs only the threat of the enemies playing before the character's first turn (2026-11-20)
  - A fixed rule by the turn order, the group placed together.

## Fights, the turn's pace (D479)

- [D479](decisions/d479-fight-turn-pace.md#d479-the-fights-pace-03-s-and-no-pace-before-the-turns-end) The fight's pace 0.3 s, and no pace before the turn's end (2026-11-21)
  - 0.5 s, no pace at all, waiting less for the client's display.

## Fights, the server's cooldowns merged (D480)

- [D480](decisions/d480-cooldowns-merged.md#d480-the-cooldowns-the-server-gives-are-merged-spell-by-spell-not-replaced) The cooldowns the server gives are merged spell by spell, not replaced (2026-11-21)
  - A snapshot decreased at each turn, the refused spell left out by the refusals' cap.

## The harvest, fights lost in a row (D481)

- [D481](decisions/d481-harvest-losses-go-on.md#d481-fights-lost-in-a-row-no-longer-end-the-harvest-and-a-loss-is-followed-by-the-whole-life-regained) Fights lost in a row no longer end the harvest, and a loss is followed by the whole life regained (2026-11-21)
  - Leaving out the resource whose protector won, the profile's share after a loss, keeping the connection alive, closing it or the haven bag once an activity failed.

## Groups, the exchange's stacks in one request (D482)

- [D482](decisions/d482-group-exchange-one-request.md#d482-a-follower-moves-its-whole-stacks-into-the-exchange-in-one-request-a-part-of-a-stack-by-a-move) A follower moves its whole stacks into the exchange in one request, a part of a stack by a move (2026-11-21)
  - A move a stack with a shorter wait, the empty request not paired, every stack by the transfer.

## Fights, a carry and the states that forbid it (D483)

- [D483](decisions/d483-carry-states.md#d483-a-carry-takes-no-fighter-whose-state-keeps-it-from-being-moved-or-carried-nor-one-enraciné-or-under-pesanteur) A carry takes no fighter whose state keeps it from being moved or carried, nor one Enraciné or under Pesanteur (2026-11-21)
  - Bubble's reading alone, the cast refused in the rules, the flag "cannot be moved" alone.

## Activities, the wait for a busy character bounded (D484)

- [D484](decisions/d484-busy-wait-bounded.md#d484-a-character-busy-a-minute-at-a-steps-start-is-put-back-in-order-and-still-busy-a-minute-later-the-harvest-or-the-hunt-fails-saying-why) A character busy a minute at a step's start is put back in order, and still busy a minute later the harvest or the hunt fails saying why (2026-11-21)
  - Failing at the bound, putting in order without end, the harvest alone.

## Fights, the blows on the character's own allies (D485)

- [D485](decisions/d485-ally-strikes.md#d485-a-blow-on-an-ally-costs-what-it-takes-and-a-push-a-fighter-stops-strikes-that-fighter-too) A blow on an ally costs what it takes, and a push a fighter stops strikes that fighter too (2026-11-21)
  - The comfortable life for the character alone, the ally's loss from the turn's start, a rule in the search, the pushes left.

## Fights, a dispel keeps the buffs the server keeps (D486)

- [D486](decisions/d486-dispel-keeps-undispellable-buffs.md#d486-a-dispel-takes-only-the-buffs-the-server-ends-and-a-removal-naming-a-spell-that-spells-alone-the-pandawa-keeps-sobre-and-casts-in-its-element) A dispel takes only the buffs the server ends, and a removal naming a spell that spell's alone: the Pandawa keeps Sobre and casts in its element (2026-11-21)
  - Reading the buff's own field 9, the pack's dispellable, a rule for the Pandawa.

## Fights, a walk nearer far enemies after the reply (D487)

- [D487](decisions/d487-far-walk-wins-tie.md#d487-a-walk-nearer-the-enemies-that-the-judge-weighs-better-before-the-reply-wins-a-tie-within-a-point-after-it) A walk nearer the enemies that the judge weighs better before the reply wins a tie within a point after it (2026-11-21)
  - The approach added to next turn's gain, more walks weighed after the reply, any walk nearest winning a tie.

## Movement and the harvest, a map action's cell crossed (D488)

- [D488](decisions/d488-harvest-action-cells.md#d488-a-walk-goes-round-the-maps-action-cells-a-harvest-the-server-cuts-by-moving-the-character-leaves-its-map-gone-through-and-a-tour-going-round-ends) A walk goes round the map's action cells, a harvest the server cuts by moving the character leaves its map gone through, and a tour going round ends (2026-11-21)
  - Going round alone, a watchdog alone, stopping the activity, counting dry tours, never crossing with no fall back.

## Fights, the Forgelance's spells cast on its lance (D489)

- [D489](decisions/d489-forgelance-lance-reach.md#d489-the-lance-gives-each-spell-it-names-its-reach-and-a-summon-on-a-free-cell-asks-its-casters-conditions) The lance gives each spell it names its reach, and a summon on a free cell asks its caster's conditions (2026-11-21)
  - A rule naming the 13 spells, leaving 314 to the server's refusal.

## Groups, a follower left behind (D490)

- [D490](decisions/d490-groups-left-behind.md#d490-a-master-goes-on-without-a-follower-missing-five-minutes-but-fighting-or-healing-and-waits-for-it-again-once-it-is-back) A master goes on without a follower missing five minutes, but fighting or healing, and waits for it again once it is back (2026-11-21)
  - Failing the master's activity at the bound, keeping the endless wait, two or ten minutes.

## Fights, the effects the engine left out (D491)

- [D491](decisions/d491-unhandled-effects.md#d491-the-damage-received-reads-every-resistance-reduction-and-share-and-a-cast-moves-its-caster) The damage received reads every resistance, reduction and share, and a cast moves its caster (2026-11-21)
  - Every effect of the audit at once, a share ended with its buff's rounds.

## Fights, an Eliotrope's portals measured (D492)

- [D492](decisions/d492-eliotrope-portals-measured.md#d492-an-eliotropes-portals-measured-a-walk-or-a-push-that-ends-on-one-sent-through-it) An Eliotrope's portals measured, a walk or a push that ends on one sent through it (2026-11-21)
  - Bubble's share, a portal's cell out of the walks, reading the action 1181's payload first.

## Fights, the Huppermage's runes, the Sram's traps and the Féca's glyphs triggered (D493)

- [D493](decisions/d493-runes-traps-glyph-triggers.md#d493-a-huppermages-runes-set-and-triggered-a-trap-set-off-by-whoever-comes-onto-it-and-a-fécas-glyphs-triggered) A Huppermage's runes set and triggered, a trap set off by whoever comes onto it, and a Féca's glyphs triggered (2026-11-21)
  - The elemental spell's own rune, a trap set off by a walk passing over it.

## Fights, a state set on an enemy worth its next turn alone (D494)

- [D494](decisions/d494-enemy-states-next-turn.md#d494-a-state-set-on-an-enemy-adds-to-the-sides-blows-on-the-next-turn-alone-so-that-a-huppermage-strikes-in-its-element) A state set on an enemy adds to the side's blows on the next turn alone, so that a Huppermage strikes in its element (2026-11-22)
  - Naming the elemental states, reading which spells take a state off, one round for every buff on an enemy.

## The harvest, a mine's floors and a resource grown back (D495)

- [D495](decisions/d495-harvest-floors-and-regrowth.md#d495-a-resource-is-used-from-its-own-floor-a-ledge-cut-from-the-characters-is-come-onto-from-the-side-it-touches-and-a-journey-stays-where-a-resource-grew-back) A resource is used from its own floor, a ledge cut from the character's is come onto from the side it touches, and a journey stays where a resource grew back (2026-11-22)
  - A floor gap above 40 taken, splitting the world graph's vertices by floor, staying for any element offered.

## Fights, a share of the damage received dispelled with its spell (D496)

- [D496](decisions/d496-damage-share-dispelled.md#d496-a-share-or-a-reduction-of-the-damage-received-is-held-by-its-spell-so-that-a-dispel-takes-it-back-the-pandawa-no-longer-drinks-three-times-for-one) A share or a reduction of the damage received is held by its spell, so that a dispel takes it back: the Pandawa no longer drinks three times for one (2026-11-22)
  - A limit of Bombance's casts a turn.

## Fights, more challenges (D497)

- [D497](decisions/d497-fights-challenges-more.md#d497-the-ai-plays-for-eleven-more-challenges-cast-by-cast-at-three-quarters-of-what-keeps-the-reward-a-minute-the-same) The AI plays for eleven more challenges, cast by cast, at three quarters of what keeps the reward a minute the same (2026-11-22)
  - The owner's other challenges, Hardi left out, the kills' order judged on the turn's end, one element for the team.

## Fights, the Forgelance's lance left on the map (D498)

- [D498](decisions/d498-forgelance-lance-stays.md#d498-the-forgelances-lance-stays-on-the-map-until-it-dies-or-a-cast-kills-it-then-is-back-in-its-hand-the-forgelance-no-longer-waits-for-a-lance-that-never-comes-back) The Forgelance's lance stays on the map until it dies or a cast kills it, then is back in its hand: the Forgelance no longer waits for a lance that never comes back (2026-11-22)
  - A rule by spell or by element; D465 kept and the recall left to the server.

## Zaaps, the discovery of those the character does not know (D499)

- [D499](decisions/d499-zaap-discovery.md#d499-an-activity-walks-to-every-zaap-within-reach-the-character-does-not-know-the-nearest-first-and-ends-once-none-is-left) An activity walks to every zaap within reach the character does not know, the nearest first, and ends once none is left (2026-11-22)
  - A tour computed once, a route asset generated, an entry of the list editor's menu, the unactivated zaaps sought.

## Fights, the summons' control left as the player set it (D500)

- [D500](decisions/d500-summon-control-left.md#d500-the-ai-never-casts-a-spell-every-character-has-that-switches-its-summons-control-no-more-maîtrise-des-invocations-for-nothing) The AI never casts a spell every character has that switches its summons' control: no more "Maîtrise des invocations" for nothing (2026-11-22)
  - A worthless cast left out of the search, the mastery searched by the summoners alone, a list of spell ids.

## Hunting, the Minor Uchronic Elixir kept up (D501)

- [D501](decisions/d501-anomaly-fragment-farming.md#d501-a-hunt-profile-may-keep-a-minor-uchronic-elixir-going-drinking-one-when-its-effect-is-missing-or-ends) A hunt profile may keep a Minor Uchronic Elixir going, drinking one when its effect is missing or ends (2026-11-22)
  - A contributor's pull request; the alterations in the readable protocol, mapping format 25, three failed uses in a row ending the tries.

## The client's directory, chosen or found by the launcher (D502)

- [D502](decisions/d502-client-directory.md#d502-the-client-is-found-in-the-folder-the-player-chose-else-where-the-launcher-installed-it-else-its-default-folder) The client is found in the folder the player chose, else where the launcher installed it, else its default folder (2026-11-24)
  - A player's suggestion; the launcher's `release.json` measured, a setting in Files, the same order for the tools.

## Fights, the Chaferfu a last resort and Saoul's share dispelled (D503)

- [D503](decisions/d503-chaferfu-and-saoul.md#d503-a-summon-every-character-has-is-a-last-resort-whatever-the-breed-saouls-share-is-dispelled-with-it-and-the-states-a-turn-began-with-stay-held-through-the-turn) A summon every character has is a last resort whatever the breed, Saoul's share is dispelled with it, and the states a turn began with stay held through the turn (2026-11-24)
  - The owner's report; the Forgelance's Chaferfu and the Pandawa's Bombance measured on the 24 November captures, a probe planning again after each action as the bot does.

## The journal kept on disk (D504)

- [D504](decisions/d504-journal-on-disk.md#d504-each-bots-journal-and-the-hosts-problems-are-written-to-disk-for-seven-days-and-250-mb-saved-from-the-studio-and-joined-to-a-report-for-their-last-24-hours) Each bot's journal and the host's problems are written to disk for seven days and 250 MB, saved from the studio and joined to a report for their last 24 hours (2026-11-24)
  - A recorder reading the rings every second, a file a source and a UTC day, the owner's retention and report span, `%USERPROFILE%` hidden.

## A fighting profile's numbers (D505)

- [D505](decisions/d505-run-fight-figures.md#d505-a-run-counts-its-characters-fights-won-and-lost-its-deaths-and-the-experience-and-kamas-they-gave) A run counts its character's fights won and lost, its deaths, and the experience and kamas they gave (2026-11-24)
  - U3 of the next release; kept in the run's record, on the card and in the Statistics by hour of the runs that fought.

## The harvest's audit, the rotation's order and resources grown back on the way (D506, D507)

- [D506](decisions/d506-harvest-audit.md#d506-the-harvests-rotation-goes-to-the-nearest-zone-of-the-round-not-yet-gone-to-the-zone-just-left-last) The harvest's rotation goes to the nearest zone of the round not yet gone to, the zone just left last (2026-11-24)
  - The nearest ready zone alone, a loop computed once, a draw weighed by the distance.
- [D507](decisions/d506-harvest-audit.md#d507-a-journey-stops-on-a-map-the-tour-went-through-already-where-a-resource-grew-back-unless-the-tour-left-that-element-there) A journey stops on a map the tour went through already where a resource grew back, unless the tour left that element there (2026-11-24)
  - Stopping for any element offered, only for elements grown back since the visit.

## Fights, the AI's searches off the thread pool (D508)

- [D508](decisions/d508-fight-searches-off-the-pool.md#d508-the-fight-ai-searches-on-threads-of-its-own-half-the-cores-and-below-the-normal-priority-never-on-the-thread-pool) The fight AI searches on threads of its own, half the cores and below the normal priority, never on the thread pool (2026-11-24)
  - A player's report of Kestrel's heartbeat late; measured with N bots planning at once on 4 cores, the pool's wait 10 s at most on the pool, 79 ms on the threads.

## Fights, the search's budget a limit, an unanswered cast and the weapon in a state that prevents fighting (D509 to D511)

- [D509](decisions/d509-d511-search-budget-and-refusals.md#d509-the-searchs-budget-bounds-the-whole-search-its-passes-and-its-reply-the-first-cast-and-two-ends-always-weighed) The search's budget bounds the whole search, its passes and its reply, the first cast and two ends always weighed (2026-11-24)
  - F1; replayed on the 24 November captures, the searches 1,083 ms at most instead of 2,253, the sum judged 0.07% lower.
- [D510](decisions/d509-d511-search-budget-and-refusals.md#d510-a-cast-the-server-leaves-unanswered-is-left-out-for-the-turn-and-for-the-fight-once-a-second-turn-left-it-unanswered) A cast the server leaves unanswered is left out for the turn, and for the fight once a second turn left it unanswered (2026-11-24)
  - F2; none of the 18,330 casts of 22 to 24 November went unanswered, a safeguard.
- [D511](decisions/d509-d511-search-budget-and-refusals.md#d511-a-weapons-blow-is-not-struck-in-a-state-that-prevents-fighting) A weapon's blow is not struck in a state that prevents fighting (2026-11-24)
  - The owner's ask, the audit; 74 blows refused with text 372, each in state 1 or 42, which the pack flags preventsFight.

## The studio made simpler (D513 to D516)

- [D513](decisions/d513-studio-navigation.md#d513-the-studio-opens-on-an-activity-bar-and-one-side-bar-the-bottom-panel-and-the-details-hidden-until-needed-the-protocol-an-advanced-tool) The studio opens on an activity bar and one side bar, the bottom panel and the Details hidden until needed, the protocol an advanced tool (2026-11-24)
  - Stage 1 of 4 of the redesign after `next`, taken by the lead under the owner's delegation; Characters, Library and World as views, Advanced tools.
- [D514](decisions/d514-character-card-and-home.md#d514-a-characters-tab-chooses-its-profile-and-plays-it-and-the-home-page-is-the-characters-cards) A character's tab chooses its profile and plays it, and the home page is the characters' cards (2026-11-25)
  - Stage 2 of 4 of the redesign, taken by the lead under the owner's delegation; the tab's header, the home page's cards.
- [D515](decisions/d515-library-by-kind.md#d515-the-library-groups-the-project-by-kind-its-folders-on-demand-and-the-profiles-header-speaks-of-settings) The Library groups the project by kind, its folders on demand, and the profile's header speaks of settings (2026-11-25)
  - Stage 3 of 4 of the redesign, taken by the lead under the owner's delegation; the World's view makes a route or a zone.
- [D516](decisions/d516-settings-and-statistics.md#d516-the-statistics-are-a-tab-of-their-own-and-the-settings-read-general-bots-files-advanced-and-about) The statistics are a tab of their own, and the settings read General, Bots, Files, Advanced and About (2026-11-25)
  - Stage 4 of 4 of the redesign, taken by the lead under the owner's delegation; the panels' styles shared in `Themes/Panels.axaml`.

## Movement and banking, a walk refused with its confirmation (D517)

- [D517](decisions/d517-miner-refused-door.md#d517-a-walk-refused-after-it-was-shown-has-not-arrived-and-a-banks-journey-whose-way-failed-is-not-travelled-not-without-a-bank) A walk refused after it was shown has not arrived, and a bank's journey whose way failed is not travelled, not without a bank (2026-11-25)
  - Every journey failed once a way failed on it, a refused walk's way tried again, ending no run on a failed unloading.

## Activities, never idle until the server's kick (D518, D519)

- [D518](decisions/d518-d519-never-idle.md#d518-a-journey-or-a-banking-that-fails-climbs-a-bounded-recovery-ladder-before-it-ends-the-activity-tried-again-twice-the-character-put-in-order-a-step-aside-out-of-its-map-by-the-nearest-exit) A journey or a banking that fails climbs a bounded recovery ladder before it ends the activity: tried again twice, the character put in order, a step aside, out of its map by the nearest exit (2026-11-25)
  - The owner's ask after four kicks in a week (D481, D484, D488, the miner's door); a ladder per case, the map's information asked again (D60).
- [D519](decisions/d518-d519-never-idle.md#d519-a-run-that-fails-or-makes-no-progress-for-ten-minutes-puts-the-character-back-in-order-and-restarts-its-activity-after-30-s-doubling-to-8-min-six-times-an-hour-at-most-a-failure-no-restart-would-change-or-a-stop-still-ends-it) A run that fails, or makes no progress for ten minutes, puts the character back in order and restarts its activity, after 30 s doubling to 8 min, six times an hour at most; a failure no restart would change, or a stop, still ends it (2026-11-25)
  - A profile's setting, on for the harvest and the hunt; a restart from the host, at once, uncapped, or map changes taken for no progress.

## Dungeons, the first version (D520 to D529)

- [D520](decisions/d520-d523-dungeons.md#d520-a-dungeon-is-an-activity-of-its-own-that-reads-the-clients-dungeon-table-and-follows-the-server-from-room-to-room) A dungeon is an activity of its own that reads the client's dungeon table and follows the server from room to room (2026-11-25)
  - The owner's ask; the client's 187 dungeons and 12 captured connections in four dungeons; a route step and the table's order rejected.
- [D521](decisions/d520-d523-dungeons.md#d521-the-dungeon-is-made-for-a-character-alone-first-the-group-following-once-it-works) The dungeon is made for a character alone first, the group following once it works (2026-11-25)
  - The owner's choice; every captured run a party, each member talking to the keeper; a follower entering behind its master the fourth pull request.
- [D522](decisions/d520-d523-dungeons.md#d522-every-keyed-dungeon-of-the-table-the-keepers-and-the-exits-replies-chosen-by-their-french-text) Every keyed dungeon of the table, the keepers' and the exits' replies chosen by their French text (2026-11-25)
  - The owner's choice; no text id shared between keepers, the French text in the pack whatever the client's language; the four measured dungeons only rejected.
- [D523](decisions/d520-d523-dungeons.md#d523-a-dungeon-is-entered-with-the-keyring-when-the-keeper-offers-it-else-with-a-key-carried-else-the-activity-ends) A dungeon is entered with the keyring when the keeper offers it, else with a key carried, else the activity ends (2026-11-25)
  - The owner's choice; a key spent at each entry, the keyring none; keys from the bank next, bought or crafted with step 11.
- [D524](decisions/d520-d523-dungeons.md#d524-a-profile-lists-several-dungeons-entered-in-turn-those-the-character-cannot-enter-left-out) A profile lists several dungeons, entered in turn, those the character cannot enter left out (2026-11-25)
  - The owner's ask; the next of the list in turn after the one last entered, a keeper's refusal shutting a dungeon for the run alone.
- [D525](decisions/d520-d523-dungeons.md#d525-in-a-room-the-character-regenerates-fights-its-groups-and-lets-the-server-move-it-a-won-room-it-was-not-moved-from-is-left-on-foot-and-a-fight-lost-leaves-the-dungeon-for-the-run) In a room the character regenerates, fights its groups and lets the server move it; a won room it was not moved from is left on foot, and a fight lost leaves the dungeon for the run (2026-11-25)
  - The server's move measured 128 times; the rooms' order rejected, a loss answered as the hunt's rejected.
- [D526](decisions/d520-d523-dungeons.md#d526-the-exits-npc-lets-the-character-out-of-a-dungeon-the-runs-are-counted-and-its-loot-is-banked-outside-keys-kept) The exit's NPC lets the character out of a dungeon, the runs are counted, and its loot is banked outside, keys kept (2026-11-25)
  - The exit measured 28 times; the activity in the catalogue with its form; banking inside and depositing the keys rejected.
- [D527](decisions/d520-d523-dungeons.md#d527-out-of-a-dungeon-the-character-banks-once-its-weight-and-the-pods-the-next-run-is-likely-to-bring-reach-the-banking-share) Out of a dungeon the character banks once its weight and the pods the next run is likely to bring reach the banking share (2026-11-25)
  - The owner's ask; the most a run brought, per dungeon; an estimate from the drops and the average rejected.
- [D528](decisions/d520-d523-dungeons.md#d528-in-a-group-each-follower-enters-and-leaves-a-dungeon-by-itself-and-the-group-leaves-out-a-dungeon-a-follower-cannot-enter) In a group each follower enters and leaves a dungeon by itself, and the group leaves out a dungeon a follower cannot enter (2026-11-25)
  - As measured, each character at the keeper and the exit; the owner's choice, the dungeon left out of the group's turn; the group teleport rejected.
- [D529](decisions/d520-d523-dungeons.md#d529-a-group-in-dungeons-banks-together-outside-them-each-member-keeping-its-keys-and-the-keyring) A group in dungeons banks together outside them, each member keeping its keys and the keyring (2026-11-25)
  - The owner's choice, in this pull request; the group's bank by D527's estimate, the keys kept by every member.

## The studio in next's dark islands, layout and icons (D530 to D532)

- [D530](decisions/d530-next-dark-islands.md#d530-the-studio-takes-nexts-dark-style-vs-codes-dark-modern-as-discreet-islands-and-stays-dark-only) The studio takes next's dark style, VS Code's Dark Modern as discreet islands, and stays dark only (2026-11-25)
  - The owner's ask; a light theme made then removed at the owner's word; seen in the running app and in the headless drawings.
- [D531](decisions/d531-next-layout.md#d531-the-studio-takes-nexts-layout-a-thin-title-bar-with-the-layout-toggles-the-views-alone-in-the-activity-bar-tabs-on-the-frame-no-tab-at-start) The studio takes next's layout: a thin title bar with the layout toggles, the views alone in the activity bar, tabs on the frame, no tab at start (2026-11-25)
  - The owner's ask; the run block, the journal and statistics entries leave the bars; the character's column on the left.
- [D532](decisions/d532-lucide-icons.md#d532-the-studio-draws-lucides-icons-as-next-does-under-the-keys-its-intellij-icons-had) The studio draws Lucide's icons, as next does, under the keys its IntelliJ icons had (2026-11-25)
  - The owner's ask; Lucide 1.72.0, next's version, written by `scripts/icons.cs` with its ISC license checked.

## Hunting, wild mounts captured (D533)

- [D533](decisions/d533-mount-capture.md#d533-a-hunt-profile-may-capture-wild-mounts-the-main-wears-a-carried-capture-net-casts-its-taming-spell-on-a-mount-at-its-first-chance-and-hunts-on-without-capturing-once-it-has-none) A hunt profile may capture wild mounts: the main wears a carried capture net, casts its taming spell on a mount at its first chance, and hunts on without capturing once it has none (2026-11-25)
  - The owner's ask; nets and spells measured in the client, none in the captures; buying, crafting, every member capturing set aside.

## The studio's shell after next (D540 to D549, D560)

- [D540](decisions/d540-command-palette.md#d540-the-search-is-also-nexts-command-palette-ctrl-shift-p-types-its-sign-the-title-bars-command-centre-names-the-app-the-views-and-each-characters-play-pause-stop-and-forget-are-commands) The search is also next's command palette: Ctrl Shift P types its sign, the title bar's command centre names the app, the views and each character's play, pause, stop and forget are commands (2026-11-25)
  - Stage 1 of 4 of the shell after `next`, under the owner's delegation; begun by the redesign session, finished here.
- [D541](decisions/d541-status-bar.md#d541-the-status-bar-is-vs-codes-the-version-and-the-relay-on-the-left-the-client-and-its-data-the-bots-playing-the-problems-the-saving-and-the-update-on-the-right-each-item-opening-what-it-names) The status bar is VS Code's: the version and the relay on the left, the client and its data, the bots playing, the problems, the saving and the update on the right, each item opening what it names (2026-11-25)
  - Stage 2 of 4, under the owner's delegation; next's relay words and hover, the problems read at every refresh.
- [D542](decisions/d542-layout-kept.md#d542-the-editors-tabs-the-one-shown-and-the-bottom-panels-tab-come-back-after-a-restart-once-the-host-runs-a-tab-whose-subject-is-gone-falls-back-quietly) The editor's tabs, the one shown and the bottom panel's tab come back after a restart, once the host runs; a tab whose subject is gone falls back quietly (2026-11-25)
  - Stage 3 of 4, under the owner's delegation; next keeps nothing on disk, the studio already kept the sizes and parts (D513).
- [D543](decisions/d543-empty-states.md#d543-an-empty-view-guides-the-characters-view-says-how-to-connect-a-character-through-d3b-the-worlds-and-the-librarys-offer-to-make-a-route-a-zone-or-a-profile-the-statistics-every-session) An empty view guides: the Characters view says how to connect a character through D3B, the World's and the Library's offer to make a route, a zone or a profile, the statistics every session (2026-11-25)
  - Stage 4 of 4, under the owner's delegation; a block of its own, a search that finds nothing guiding nowhere.
- [D544](decisions/d544-details-tidied.md#d544-the-details-play-pause-and-stop-the-character-the-characters-view-plays-every-one-connected-and-lists-only-those-played-through-d3b-and-what-is-not-in-the-app-leaves-the-details) The Details play, pause and stop the character, the Characters view plays every one connected and lists only those played through D3B, and what is not in the app leaves the Details (2026-11-25)
  - The owner's asks while checking D540 to D543 in the running app.
- [D545](decisions/d545-slides-and-corners.md#d545-a-part-shown-or-hidden-slides-open-or-closed-in-200-ms-on-nexts-curve-and-the-windows-corners-are-rounded-on-windows-11) A part shown or hidden slides open or closed in 200 ms on next's curve, and the window's corners are rounded on Windows 11 (2026-11-25)
  - The owner's asks; a drag follows the pointer, Windows 10 keeps its corners.
- [D546](decisions/d546-status-icons-and-held-docks.md#d546-the-relay-and-the-version-join-the-status-bars-icons-on-the-right-and-a-parts-content-keeps-its-size-while-it-slides) The relay and the version join the status bar's icons on the right, and a part's content keeps its size while it slides (2026-11-25)
  - The owner's asks in the running app.
- [D547](decisions/d547-one-scale-and-wide-panel.md#d547-the-studios-radii-small-text-and-colours-follow-one-scale-and-the-bottom-panel-spans-to-the-windows-right-edge) The studio's radii, small text and colours follow one scale, and the bottom panel spans to the window's right edge (2026-11-25)
  - The owner's asks; radii 4, 6, 8, small text 11, the palette's keys; the same control styled twice left for later.
- [D548](decisions/d548-one-style-each.md#d548-the-profiles-field-is-a-select-of-its-own-a-fields-hover-is-opaque-and-the-titles-and-links-share-one-style-each) The profile's field is a select of its own, a field's hover is opaque, and the titles and links share one style each (2026-11-25)
  - The owner's asks; supersedes D544's guard; fields, pills and kind squares left for later.
- [D549](decisions/d549-fields-squares-badges-tools.md#d549-a-values-field-a-search-a-kinds-square-a-badge-and-a-toolbars-button-each-have-one-style-shared-by-every-view) A value's field, a search, a kind's square, a badge and a toolbar's button each have one style, shared by every view (2026-11-25)
  - The suite D548 left; the pills over the maps and the colours drawn in C# left for later.
- [D560](decisions/d560-glass-tiles-and-drawn-colours.md#d560-the-glass-over-a-map-a-tile-of-figures-and-the-colours-the-drawing-code-paints-with-each-have-one-source) The glass over a map, a tile of figures, and the colours the drawing code paints with each have one source (2026-11-25)
  - What D549 left; `StudioColors` kept equal to the palette's keys by a test. D550 to D559 are the bank transfer's.

## Banking, the transfer between the bank and the guild's chest (D550 to D552)

- [D550](decisions/d550-d552-bank-chest-transfer.md#d550-a-transfer-activity-moves-objects-between-the-bank-and-the-guilds-chest-either-way-chosen-by-category-type-and-item-in-rounds-its-pods-allow-then-says-what-it-moved) A transfer activity moves objects between the bank and the guild's chest, either way, chosen by category, type and item, in rounds its pods allow, then says what it moved (2026-11-25)
  - The owner's ask, everything or by category, type and item; no kamas (the chest holds none), the tab it opens on, a refusal put back in the source by default.
- [D551](decisions/d550-d552-bank-chest-transfer.md#d551-taking-out-of-a-storage-is-measured-a-stack-at-a-time-by-a-negative-move-the-storages-stacks-kept-in-the-state-the-refusals-told-by-the-clients-information-texts) Taking out of a storage is measured: a stack at a time by a negative move, the storage's stacks kept in the state, the refusals told by the client's information texts (2026-11-25)
  - Measured on 74 captures and the client's information texts; the list and all requests left, mapping format 26 unneeded.
- [D552](decisions/d550-d552-bank-chest-transfer.md#d552-the-types-of-items-are-a-settings-kind-of-their-own-and-the-categories-are-named-by-d3b-after-the-inventorys-tabs) The types of items are a setting's kind of their own, and the categories are named by D3B after the inventory's tabs (2026-11-25)
  - The inventory's tabs as categories, 229 named types picked from the pack.

## The studio's character map, zoomed (D553)

- [D553](decisions/d553-map-zoom.md#d553-a-characters-map-zooms-with-the-wheel-around-the-pointer-from-its-fit-to-four-times-it-a-drag-pans-it-once-zoomed-and-the-worlds-zoom-bar-says-and-steps-it) A character's map zooms with the wheel around the pointer from its fit to four times it, a drag pans it once zoomed, and the world's zoom bar says and steps it (2026-11-25)
  - The owner's ask, passed on by the shell's session; the world viewport's notch, step and bar, its 100 % the map's fit.
