using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>The groups' log messages, each naming its bot by its character's id.</summary>
internal static partial class GroupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: waits for its followers {Missing} before it acts")]
    public static partial void Waiting(ILogger logger, long character, string missing);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its followers are with it, it acts")]
    public static partial void Gathered(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: goes on without its follower {Follower}, {Absence} for five minutes")]
    public static partial void LeftBehind(ILogger logger, long character, long follower, MemberAbsence absence);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: invites its follower {Follower} into its party")]
    public static partial void Inviting(ILogger logger, long character, long follower);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: leaves party {Party}, which another leads")]
    public static partial void Leaving(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: accepts its master's invitation into party {Party}")]
    public static partial void Accepting(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: refuses the invitation of {From} into party {Party}, which is not its master")]
    public static partial void Refusing(ILogger logger, long character, long from, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: turns off its automatic options in party {Party}")]
    public static partial void OptionsOff(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: turns on its automatic options in party {Party}, which the game follows")]
    public static partial void OptionsOn(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: asks to follow the leader of party {Party}")]
    public static partial void FollowingLeader(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: asks its followers to follow it in party {Party}")]
    public static partial void CallingFollowers(ILogger logger, long character, int party);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: walks with its master {Master} to cell {Cell}")]
    public static partial void Copying(ILogger logger, long character, long master, int cell);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: joins its master {Master}'s fight {Fight}")]
    public static partial void JoiningFight(ILogger logger, long character, long master, int fight);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: waits for its followers {Placing} to be placed before it is ready")]
    public static partial void WaitingPlacement(ILogger logger, long character, string placing);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its followers are placed")]
    public static partial void FollowersPlaced(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: takes its group to the bank")]
    public static partial void Banking(ILogger logger, long character);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: was given {Stacks} stacks by its follower {Follower}")]
    public static partial void Given(ILogger logger, long character, long follower, int stacks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: gave {Stacks} stacks to its follower {Follower}")]
    public static partial void Handed(ILogger logger, long character, long follower, int stacks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: asks its master {Master} for an exchange")]
    public static partial void AskingExchange(ILogger logger, long character, long master);
}
