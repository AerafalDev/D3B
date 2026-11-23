//! The messages of the game connection the C# version reads, by family (ADR 0021): the frames its `.proto` files on
//! `main` declare (`src/D3B.Protocol/Game/`), by its readable names, which are ours, plus those `next` pinned beyond
//! them. Each names the Lua calls of `MoonBot` it backs, as the study of that API lists them by module, and the
//! checklist that measured it in game per build. Two of its families hold no frame of their own: actors (shapes that
//! maps and fights hold) and game (the envelope, which `protocol::envelope` reads).

/// A family of messages, as the C# version's `.proto` files group them.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(super) struct Family {
    pub(super) name: &'static str,
    pub(super) messages: &'static [Entry],
}

/// One message: its readable name, the Lua calls it backs, and per build the checklist (a file of `docs/checklists/`)
/// whose measure showed it read in game.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(super) struct Entry {
    pub(super) name: &'static str,
    pub(super) lua: &'static [&'static str],
    pub(super) measured: &'static [(&'static str, &'static str)],
}

const fn m(name: &'static str, lua: &'static [&'static str]) -> Entry {
    Entry {
        name,
        lua,
        measured: &[],
    }
}

/// Measured in game by `measured`'s checklists, each on its build.
const fn measured(
    name: &'static str,
    lua: &'static [&'static str],
    measured: &'static [(&'static str, &'static str)],
) -> Entry {
    Entry { name, lua, measured }
}

/// The protocol's checklist, items 1 to 3 on 3.7.13.17 (2026-11-12 and 13): the live view read in game, no frame not
/// trusted.
const PROTOCOL: &[(&str, &str)] = &[("3.7.13.17", "protocol.md")];
/// The live view's life, on 3.7.13.17 (2026-11-13): the regeneration shown, then ended.
const LIFE: &[(&str, &str)] = &[("3.7.13.17", "live-view-life.md")];

/// The catalogue, in the order of `docs/play.md`'s families, one message a line, as a table reads.
#[rustfmt::skip]
pub(super) const CATALOGUE: &[Family] = &[
    Family {
        name: "Movement",
        messages: &[
            m("MapMovementRequest", &["map.moveToCell"]),
            measured("MapMovementEvent", &["map.cellId"], PROTOCOL),
            m("MapMovementConfirmRequest", &["map.moveToCell"]),
            m("MapMovementConfirmResponse", &[]),
            m("MapMovementRefusedEvent", &["map.moveToCell"]),
            m("MapMovementCancelRequest", &[]),
            m("MapChangeRequest", &["map.changeMap", "map.moveToward"]),
        ],
    },
    Family {
        name: "Maps",
        messages: &[
            measured("MapCurrentEvent", &["map.currentMapId", "map.currentMap"], PROTOCOL),
            measured("MapInformationRequest", &[], PROTOCOL),
            measured(
                "MapInformationEvent",
                &["map.cellId", "map.npcs", "map.monsterGroups", "map.getInteractives"],
                PROTOCOL,
            ),
            measured("MapActorShownEvent", &["map.monsterGroups", "map.npcs"], PROTOCOL),
            measured("MapActorRemovedEvent", &["map.monsterGroups", "map.npcs"], PROTOCOL),
        ],
    },
    Family {
        name: "Interactives",
        messages: &[
            m("InteractiveUseRequest", &["map.useInteractive", "map.gather", "map.door"]),
            m("InteractiveUsedEvent", &["map.gather", "map.door"]),
            m("InteractiveUseErrorEvent", &["map.gather", "map.useInteractive"]),
            m("InteractiveUseEndedEvent", &["map.gather"]),
            measured("InteractiveElementUpdatedEvent", &["map.getInteractives", "map.getResources"], PROTOCOL),
            measured("StatedElementUpdatedEvent", &["map.getResources", "map.waitInteractiveState"], PROTOCOL),
        ],
    },
    Family {
        name: "NPCs",
        messages: &[
            m("NpcActionRequest", &["npc.npc", "npc.npcBank"]),
            m("NpcActionErrorEvent", &["npc.npc"]),
            m("NpcDialogCreationEvent", &["npc.isInDialog"]),
            m("NpcDialogQuestionEvent", &["npc.getQuestionId", "npc.getResponses"]),
            m("NpcDialogReplyRequest", &["npc.reply"]),
            m("DialogLeaveEvent", &["npc.isInDialog", "npc.leaveDialog"]),
        ],
    },
    Family {
        name: "Exchanges",
        messages: &[
            m("ExchangeStartedWithStorageEvent", &["npc.npcBank"]),
            m("ExchangeStartedWithTabbedStorageEvent", &["npc.npcBank"]),
            m("StorageContentEvent", &["exchange.storageItems", "exchange.storageKamas"]),
            m("StorageObjectsUpdateEvent", &["exchange.storageItems"]),
            m("StorageObjectUpdateEvent", &["exchange.storageItemQuantity"]),
            m("StorageObjectRemovedEvent", &["exchange.storageItems"]),
            m("StorageKamasUpdateEvent", &["exchange.storageKamas"]),
            m("ExchangeTransferFromInventoryRequest", &["exchange.putAllItems", "exchange.putAllItemsExcept"]),
            m("ExchangeObjectMoveRequest", &["exchange.putItem", "exchange.getItem"]),
            m("ExchangeKamasRequest", &["exchange.putKamas", "exchange.getKamas"]),
            m("DialogLeaveRequest", &["exchange.leave", "npc.leaveDialog"]),
            m("ExchangeLeaveEvent", &["exchange.leave"]),
            m("ExchangePlayerRequest", &["exchange.requestById"]),
            m("ExchangeRequestedEvent", &["exchange.waitForRequest"]),
            m("ExchangeAcceptRequest", &["exchange.accept"]),
            m("ExchangeStartedWithPlayerEvent", &["exchange.waitForRequest"]),
            m("ExchangeObjectsAddedEvent", &["exchange.theirItems"]),
            m("ExchangeReadyRequest", &["exchange.ready"]),
            m("ExchangeReadyEvent", &["exchange.waitForValidation"]),
        ],
    },
    Family {
        name: "Inventory",
        messages: &[
            m("InventoryContentEvent", &["inventory.list", "inventory.count"]),
            m("ObjectAddedEvent", &["inventory.list", "inventory.countItem"]),
            m("ObjectQuantityEvent", &["inventory.countItem"]),
            m("ObjectDeletedEvent", &["inventory.list"]),
            m("ObjectsDeletedEvent", &["inventory.list"]),
            m("ObjectMovementEvent", &["inventory.isEquipped"]),
            m("ObjectObtainedEvent", &["inventory.countItem"]),
            m("ObjectUseRequest", &["inventory.useItem", "inventory.useItemByGID"]),
            m("ObjectUseMultipleRequest", &["inventory.useMultipleItem"]),
            measured("InventoryWeightEvent", &["inventory.pods", "inventory.podsMax"], PROTOCOL),
            measured("KamasUpdateEvent", &["character.kamas"], PROTOCOL),
        ],
    },
    Family {
        name: "Jobs",
        messages: &[
            m("JobDescriptionEvent", &["job.list"]),
            m("JobExperiencesEvent", &["job.level", "job.xp"]),
        ],
    },
    Family {
        name: "Texts",
        messages: &[
            m("TextInformationEvent", &[]),
            m("AlterationsSnapshotEvent", &[]),
            m("AlterationAddedEvent", &[]),
        ],
    },
    Family {
        name: "Teleportation",
        messages: &[
            m("ZaapKnownListEvent", &["character.knownZaaps", "map.knownZaapAt"]),
            m("TeleportDestinationsEvent", &["map.getZaapDestinations"]),
            m("TeleportRequest", &["map.useZaap", "map.useZaapi", "map.zaapTo"]),
        ],
    },
    Family {
        name: "Haven bags",
        messages: &[m("HavenBagEnterRequest", &["character.havreSac"])],
    },
    Family {
        name: "Fights",
        messages: &[
            m("AttackMonsterRequest", &["map.fightGroup", "map.fight"]),
            m("FightStartingEvent", &["fight.isInFight"]),
            m("FightJoinRequest", &[]),
            m("FightJoinEvent", &["fight.isInFight"]),
            m("FightPlacementPossiblePositionsEvent", &["fight.getPlacementCells"]),
            m("FightPlacementPositionRequest", &["fight.placeOnCell"]),
            m("EntitiesDispositionEvent", &["fight.getFighterOnCell"]),
            m("FightOptionToggleRequest", &[]),
            m("FightOptionUpdateEvent", &[]),
            m("ChallengeListEvent", &["fight.getChallenges"]),
            m("ChallengeAddEvent", &["fight.getChallenges"]),
            m("ChallengeTargetsEvent", &["fight.getChallenges"]),
            m("ChallengeResultEvent", &["fight.getChallengeResult"]),
            m("ChallengeProposalRequest", &["fight.getChallenges"]),
            m("ChallengeProposalEvent", &["fight.getChallenges"]),
            m("ChallengeSelectionRequest", &["fight.selectChallenge"]),
            m("ChallengeBonusChoiceRequest", &["fight.challengeBonus"]),
            m("ChallengeBonusChoiceSelectedEvent", &["fight.challengeBonus"]),
            m("FightReadyRequest", &["fight.ready"]),
            m("FightHumanReadyStateEvent", &[]),
            m("FightStartEvent", &["fight.isInFight"]),
            m("FightFighterShowEvent", &["fight.getAllFighters"]),
            m("FightSynchronizeEvent", &["fight.getAllFighters"]),
            m("FightTurnListEvent", &["fight.getTurnOrder"]),
            m("FightNewRoundEvent", &["fight.getTurnNumber"]),
            m("FightTurnEvent", &["fight.getCurrentFighterId"]),
            m("FightTurnStartPlayingEvent", &["fight.isMyTurn"]),
            m("FightIsTurnReadyEvent", &[]),
            m("FightTurnReadyRequest", &[]),
            m("FightTurnEndEvent", &["fight.waitForTurn"]),
            m("FightTurnFinishRequest", &["fight.passTurn"]),
            m("FightRefreshCharacterStatsEvent", &["fight.myAP", "fight.myMP", "fight.myHP"]),
            m("RefreshCooldownEvent", &["fight.spellCooldown"]),
            m("FightEndEvent", &["fight.waitForFightEnd"]),
        ],
    },
    Family {
        name: "Fight actions",
        messages: &[
            m("GameActionFightCastRequest", &["fight.castSpell"]),
            m("SequenceStartEvent", &[]),
            m("SequenceEndEvent", &[]),
            m("GameActionAcknowledgementRequest", &[]),
            m("GameActionFightEvent", &["fight.getActionLog", "fight.myHP"]),
            m("FightEffectsEvent", &["fight.getBuffs"]),
            m("FightBuffEndEvent", &["fight.getBuffs"]),
        ],
    },
    Family {
        name: "Spells",
        messages: &[
            m("SpellsEvent", &["fight.getSpells"]),
            m("FightSpellsContextEvent", &["fight.getSpells"]),
        ],
    },
    Family {
        name: "Characters",
        messages: &[
            m("CharacterListRequest", &["character.list"]),
            m("CharacterListEvent", &["character.list", "character.count"]),
            m("CharacterSelectionRequest", &["character.switch"]),
            measured("CharacterSelectionEvent", &["character.name", "character.breed", "character.sex"], PROTOCOL),
        ],
    },
    Family {
        name: "Characteristics",
        messages: &[
            measured(
                "CharacterStatsEvent",
                &[
                    "character.hp",
                    "character.ap",
                    "character.mp",
                    "character.energy",
                    "character.xp",
                ],
                PROTOCOL,
            ),
            measured("LifeRegenStartEvent", &["character.hp"], LIFE),
            measured("LifeRegenEndEvent", &["character.hp", "character.hpMax"], LIFE),
            measured("CharacterLevelUpEvent", &["character.level"], PROTOCOL),
        ],
    },
    Family {
        name: "Context",
        messages: &[
            m("ContextCreationRequest", &[]),
            measured("ContextCreationEvent", &["fight.isInFight"], PROTOCOL),
            measured("ContextDestroyEvent", &["fight.isInFight"], PROTOCOL),
        ],
    },
    Family {
        name: "Party",
        messages: &[
            m("PartyInvitationRequest", &["map.inviteToGroup"]),
            m("PartyInvitationEvent", &["social.pending"]),
            m("PartyInvitationAcceptRequest", &["social.accept"]),
            m("PartyInvitationRefuseRequest", &["social.refuse"]),
            m("PartyJoinEvent", &["group.getMembers"]),
            m("PartyNewMemberEvent", &["group.getMembers"]),
            m("PartyMemberRemoveEvent", &["group.getMembers"]),
            m("PartyDeletedEvent", &["group.getMembers"]),
            m("PartyLeaveEvent", &["group.getMembers"]),
            m("PartyKickedByEvent", &["group.getMembers"]),
            m("PartyLeaveRequest", &[]),
            m("PartyKickRequest", &[]),
            m("PartyMemberInFightEvent", &["group.allOnSameMap"]),
            m("PartyAutoOptionOneOnRequest", &[]),
            m("PartyAutoOptionOneOffRequest", &[]),
            m("PartyAutoOptionTwoOnRequest", &[]),
            m("PartyAutoOptionTwoOffRequest", &[]),
            m("PartyFollowMeRequest", &[]),
            m("PartyFollowLeaderRequest", &[]),
            m("PartyFollowLeaderResponse", &[]),
            m("PartyStopFollowingRequest", &[]),
            m("PartyStopFollowingResponse", &[]),
            m("PartyMemberFollowsEvent", &[]),
            m("PartyMemberStopsFollowingEvent", &[]),
        ],
    },
];
