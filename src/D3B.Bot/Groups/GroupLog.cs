using Microsoft.Extensions.Logging;

namespace D3B.Bot.Groups;

/// <summary>The groups' log messages, each naming its bot by its character's id.</summary>
internal static partial class GroupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: waits for its followers {Missing} before it acts")]
    public static partial void Waiting(ILogger logger, long character, string missing);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: its followers are with it, it acts")]
    public static partial void Gathered(ILogger logger, long character);

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
}
