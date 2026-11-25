using D3B.Bot.Inventory;
using D3B.Bot.Runtime;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Fights;

/// <summary>The fight AI's log messages, each naming its bot by its character's id.</summary>
internal static partial class FightLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plays its fight, {TurnsToWin:F1} turns to win against {TurnsToLose:F1} to lose, prudent {Prudent}")]
    public static partial void Judged(ILogger logger, long character, double turnsToWin, double turnsToLose, bool prudent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: chooses the challenges' bonus {Bonus}, {Outcome}")]
    public static partial void BonusChosen(ILogger logger, long character, ChallengeBonus bonus, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the fight's challenge {Challenge}, bonus {Bonus} %, {Targets} targets")]
    public static partial void Challenged(ILogger logger, long character, int challenge, int bonus, int targets);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: challenge {Challenge} {Outcome}")]
    public static partial void ChallengeEnded(ILogger logger, long character, int challenge, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves challenge {Challenge}, which would cost its turn more than its bonus pays")]
    public static partial void ChallengeLeft(ILogger logger, long character, int challenge);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: placed on cell {Cell}")]
    public static partial void Placed(ILogger logger, long character, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: plans {Actions} actions, worth {Score:F0} against {Standing:F0} ending now, {Weighed} weighed in {Milliseconds} ms")]
    public static partial void Planned(ILogger logger, long character, int actions, double score, double standing, int weighed, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: tames the wild mount on cell {Cell} for its capture")]
    public static partial void Taming(ILogger logger, long character, int cell);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: wears no capture net, so the fight's {Mounts} wild mounts are not captured")]
    public static partial void NoCaptureNet(ILogger logger, long character, int mounts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: wears item {Item} in the fight consumable slot for the group's capture")]
    public static partial void WoreForCapture(ILogger logger, long character, int item);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: could not wear item {Item} in the fight consumable slot, {Outcome}, so the group is fought without it")]
    public static partial void NotWornForCapture(ILogger logger, long character, int item, ObjectWearOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: carries no capture net that fits the group's wild mounts at Éleveur level {Breeder}, so none is captured")]
    public static partial void NoNetToWear(ILogger logger, long character, int breeder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: carries no soul stone that captures level {Level}, so the group's archmonster's soul is not captured")]
    public static partial void NoStoneToWear(ILogger logger, long character, int level);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: casts the soul capture against the fight's archmonster")]
    public static partial void SoulCapture(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: casts spell {Spell} on cell {Cell}, {Outcome}")]
    public static partial void Cast(ILogger logger, long character, int spell, int cell, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the server refused spell {Spell} on cell {Cell}, text {Text}")]
    public static partial void Refused(ILogger logger, long character, int spell, int cell, int text);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks to cell {Cell}, {Steps} steps, {Outcome}")]
    public static partial void Walked(ILogger logger, long character, int cell, int steps, string outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: ends its turn after {Actions} actions")]
    public static partial void TurnEnded(ILogger logger, long character, int actions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: the client did not show action {Action} within its delay")]
    public static partial void NotShown(ILogger logger, long character, TacticActionKind action);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: the fight's AI failed, the fight is left to the player")]
    public static partial void Failed(ILogger logger, long character, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: the fight's AI stopped, {Reason}")]
    public static partial void Stopped(ILogger logger, long character, Interruption reason);
}
