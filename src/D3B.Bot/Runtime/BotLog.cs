using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Data.World;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>The bots' log messages, each naming its bot by its character's id.</summary>
internal static partial class BotLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plays through game connection {ConnectionId}")]
    public static partial void Attached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: game connection {ConnectionId} closed")]
    public static partial void Detached(ILogger logger, long character, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the player sent {Request}, the bot pauses until it is resumed")]
    public static partial void PausedByPlayer(ILogger logger, long character, string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: resumed")]
    public static partial void Resumed(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: paused after a fight, until it is resumed")]
    public static partial void PausedAfterFight(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: paused from the app, until it is resumed")]
    public static partial void PausedFromApp(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: the client, inactive, sends the turns' ends by itself until the player clicks its OK")]
    public static partial void ClientInactive(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its behavior was interrupted, {Reason}")]
    public static partial void Interrupted(ILogger logger, long character, Interruption reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: a message could not be handled")]
    public static partial void HandlerFailed(ILogger logger, long character, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game connection {ConnectionId}: its account is not known, so no bot plays its character")]
    public static partial void AccountUnknown(ILogger logger, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks from cell {From} to cell {To}, {Steps} steps")]
    public static partial void Walking(ILogger logger, long character, int from, int to, int steps);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walk to cell {To} over, {Outcome}")]
    public static partial void Walked(ILogger logger, long character, int to, WalkOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves map {From} for map {To} by a {Transition}, from cell {Cell}")]
    public static partial void ChangingMap(ILogger logger, long character, long from, long to, WorldTransitionType transition, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: teleports from map {From} to the zaap of map {To}")]
    public static partial void Teleporting(ILogger logger, long character, long from, long to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves map {From} for map {To} through the dialog of NPC {Npc}")]
    public static partial void TalkingThrough(ILogger logger, long character, long from, long to, int npc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves the haven bag of {Owner}")]
    public static partial void LeavingHavenBag(ILogger logger, long character, long owner);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: map change over on map {Map}, {Outcome}")]
    public static partial void ChangedMap(ILogger logger, long character, long map, MapChangeOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: uses element {Element} with skill {Skill}, from cell {Cell}")]
    public static partial void Using(ILogger logger, long character, int element, int skill, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: use of element {Element} with skill {Skill} over, {Outcome}")]
    public static partial void Used(ILogger logger, long character, int element, int skill, UseOutcome outcome);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bot {Character}: element {Element} has no way to a cell beside it from zone {Zone} of its map")]
    public static partial void OutOfReach(ILogger logger, long character, int element, int zone);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bot {Character}: the tour will come onto map {Map} from side {Side}, by map {Neighbour}, for resources the part it stands in cannot walk to")]
    public static partial void LevelWanted(ILogger logger, long character, long map, int side, long neighbour);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bot {Character}: the tour leaves the part of map {Map} on side {Side} alone: no way of fewer than {Most} map changes leads to its neighbour")]
    public static partial void LevelLeftAlone(ILogger logger, long character, long map, int side, int most);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: came onto map {Map} from side {Side}, for resources the other part could not walk to")]
    public static partial void LevelEntered(ILogger logger, long character, long map, int side);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: could not come onto map {Map} from side {Side}; its part there is left for this tour")]
    public static partial void LevelNotEntered(ILogger logger, long character, long map, int side);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: stays on map {Map}, where a wanted resource grew back as it was leaving")]
    public static partial void StayedForRegrowth(ILogger logger, long character, long map);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bot {Character}: the tour will go to zone {Zone} of map {Map}, {Changes} map changes away, for a resource the zone it stands in cannot reach")]
    public static partial void ZoneWanted(ILogger logger, long character, long map, int zone, int changes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bot {Character}: the tour leaves zone {Zone} of map {Map} alone: no way of at most {Most} map changes leads there from the zone it stands in")]
    public static partial void ZoneLeftAlone(ILogger logger, long character, long map, int zone, int most);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: tour {Round} goes round, {Journeys} journeys in a row arrived with nothing harvested and no map gone through, the last on map {Map}: its other maps are left for this tour")]
    public static partial void TourGoneRound(ILogger logger, long character, int round, long map, int journeys);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: travels from map {From}, {Changes} map changes away")]
    public static partial void Travelling(ILogger logger, long character, long from, int changes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: journey over on map {Map} after {Changes} map changes, {Outcome}")]
    public static partial void Travelled(ILogger logger, long character, long map, int changes, TravelOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: no banker stands in the bank's inside {Map}, which the host's bots no longer seek")]
    public static partial void NoBanker(ILogger logger, long character, long map);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: talks to banker {Npc} on map {Map}")]
    public static partial void OpeningBank(ILogger logger, long character, long map, int npc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: opening the bank on map {Map} over, {Outcome}")]
    public static partial void OpenedBank(ILogger logger, long character, long map, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: {Taken} of {Stacks} stacks and {Kamas} kamas put in the bank, {Outcome}")]
    public static partial void Deposited(ILogger logger, long character, int stacks, int taken, long kamas, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: banking over on map {Map}, {Outcome}")]
    public static partial void Banked(ILogger logger, long character, long map, BankOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: a fight began, which interrupts its behavior")]
    public static partial void FightBegan(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} starts")]
    public static partial void ActivityStarted(ILogger logger, long character, string activity);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} interrupted, {Reason}; it will {Response}")]
    public static partial void ActivityInterrupted(ILogger logger, long character, string activity, Interruption reason, InterruptionResponse response);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} unloads the character, {Weight} of {MaxWeight} pods")]
    public static partial void ActivityUnloading(ILogger logger, long character, string activity, int weight, int maxWeight);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: activity {Activity} left the character full after unloading it")]
    public static partial void ActivityStillFull(ILogger logger, long character, string activity);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: busy, {Kind}, for a minute at a step's start; the character is put back in order")]
    public static partial void ActivityBusy(ILogger logger, long character, BusyKind kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: activity {Activity} failed")]
    public static partial void ActivityFailed(ILogger logger, long character, string activity, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: {Action} failed; before the next try, rung {Climbed} of {Height} of the recovery, {Rung}")]
    public static partial void Recovering(ILogger logger, long character, string action, RecoveryRung rung, int climbed, int height);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: activity {Activity} failed; the character is put back in order, and restart {Restart} of the last hour comes in {Delay}")]
    public static partial void ActivityRestarting(ILogger logger, long character, string activity, int restart, TimeSpan delay, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: activity {Activity} made no progress for {Delay}: no map changed, no fight, no pod gained or banked")]
    public static partial void ActivityStalled(ILogger logger, long character, string activity, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: map {Map} left by its nearest exit, {Outcome}")]
    public static partial void LeftByNearestExit(ILogger logger, long character, long map, MapChangeOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: activity {Activity} over after {Steps} steps, {Outcome}")]
    public static partial void ActivityOver(ILogger logger, long character, string activity, int steps, ActivityOutcome outcome);
}
